/*
WifiClient.cs - wifi/http client implementation and helper methods for slideclicker

Copyright (C) 2017 Elad Alfassa <elad@fedoraproject.org>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Json;
using Android.Net.Wifi;
using Android.Net;
using Java.Lang.Reflect;

namespace slideclicker
{
    class WifiClient
    {
        private Byte[] key;
        private string uri;
        public WifiClient(byte[] key, string uri)
        {
            this.key = key;
            this.uri = uri;
        }
        public WifiClient(byte[] jsonBytes)
        {
            string json = Encoding.UTF8.GetString(jsonBytes);
            JsonValue jsonDoc = JsonObject.Parse(json);
            key = Convert.FromBase64String(jsonDoc["key"]);
            uri = jsonDoc["uri"];
        }


        /// <summary>
        /// Get the WiFi network details in a simple json format
        /// </summary>
        /// <param name="context">An Android context, eg. the main activity</param>
        /// <returns>A string containing the WiFi network detail in a JSON format</returns>
        public static string WifiHandshake(Context context)
        {
            WifiManager wifiManager = (WifiManager)context.GetSystemService(Context.WifiService);
            ConnectivityManager connectivityManager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService);
            NetworkInfo networkInfo = connectivityManager.ActiveNetworkInfo;
            bool isRegularWifi = networkInfo.Type == ConnectivityType.Wifi;

            if (isRegularWifi)
            {
                int ip = wifiManager.ConnectionInfo.IpAddress;
                IPAddress address = new IPAddress(BitConverter.GetBytes(ip));
                String ipString = address.ToString();
                String SSID = wifiManager.ConnectionInfo.SSID;
                return "{\"wifi\": true, \"ssid\": " + SSID + ",\"ip\": \"" + ipString + "\"}";
            }
            else
            {
                // might be WiFi AP (hotspot). Let's check with this awful non-public API that might be removed in a later version
                Method isWifiApEnabled = wifiManager.Class.GetMethod("isWifiApEnabled");
                if ((Boolean)isWifiApEnabled.Invoke(wifiManager))
                {
                    // Yep. We're the AP, send over the SSID
                    Method getWifiApConfiguration = wifiManager.Class.GetMethod("getWifiApConfiguration");
                    WifiConfiguration config = (WifiConfiguration)getWifiApConfiguration.Invoke(wifiManager);
                    return "{\"wifi\": true, \"ssid\": \"" + config.Ssid + "\",\"ip\": \"unknown\"}";
                }
                else
                {
                    return "{\"wifi\": false}";
                }
                
            }
        }

        /// <summary>
        /// Sign a message using SHA256 hmac and the stored key, return the signature as base64.
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <returns>base64 representation of the resulting signature</returns>
        private string SignMessage(string message)
        {
            byte[] message_bytes = Encoding.UTF8.GetBytes(message);
            HMACSHA256 hmac = new HMACSHA256(key);
            return Convert.ToBase64String(hmac.ComputeHash(message_bytes));
        }


        /// <summary>
        /// Get a screenshot via wifi, asynchrounously
        /// </summary>
        /// <returns>The screenshot, as a byte[] array</returns>
        public async Task<byte[]>GetScreenshot()
        {
            try
            {
                Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string timestampAndNonce = unixTimestamp.ToString() + "," + Guid.NewGuid().ToString();
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.Headers.Add("X-Timestamp-nonce", timestampAndNonce);
                request.Headers.Add("X-Hmac", SignMessage(timestampAndNonce));
                using (WebResponse response = await request.GetResponseAsync())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        return Common.ReadAll(stream);
                    }
                }
            } catch (Exception e)
            {
                Console.WriteLine("wifi failure");
                Console.WriteLine(e);
                // wifi failed somehow
                return null;
            }
        }
    }
}