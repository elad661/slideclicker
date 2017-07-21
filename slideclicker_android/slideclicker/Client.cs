/*
Client.cs - slideclicker client abstraction

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
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Bluetooth;
using System.Threading;

namespace slideclicker
{

    /// <summary>
    /// This "Handler" handles sending UI updates to the UI thread.
    /// </summary>
    class UIUpdateHandler : Handler
    {
        ImageView imageview;
        TextView status;
        public enum MessageType { BITMAP, STRING };
        public UIUpdateHandler(ImageView imageview, TextView status)
        {
            this.imageview = imageview;
            this.status = status;

        }


        public override void HandleMessage(Message msg)
        {
            switch((MessageType)msg.What)
            {
                case (MessageType.BITMAP):
                    // Screenshot update
                    Bitmap bitmap = (Bitmap)msg.Obj;
                    imageview.SetImageBitmap(bitmap);
                    break;
                case (MessageType.STRING):
                    // Status text update
                    Console.WriteLine("status update: " + (String)msg.Obj);
                    status.Text = (String)msg.Obj;
                    break;
            }
        }

        
        /// <summary>
        /// A helper method to send a status update to the ui
        /// </summary>
        /// <param name="msg">The new text of the status view</param>
        public void UpdateStatus(string msg)
        {
            Console.WriteLine("Sending status update: " + msg);
            ObtainMessage((int)MessageType.STRING, msg).SendToTarget();
        }
    }

    class Client
    {
        private Context context;
        private BluetoothClient bt;
        private WifiClient wifi;
        private UIUpdateHandler handler;
        private int WifiTimeoutCount = 0; // Number of times we got a timeout from wifi.
        public bool HasWifi = false;
        public Client(Context context, ImageView imageview, TextView statusview)
        {
            this.context = context;
            handler = new UIUpdateHandler(imageview, statusview);
            bt = new BluetoothClient(context, handler, WifiCallback, StateChangedCallback);
        }


        /// <summary>
        /// This method is called as a callback by the bluetooth client when it changes state
        /// </summary>
        /// <param name="state"></param>
        private void StateChangedCallback(BluetoothClient.State state)
        {
            if (state == BluetoothClient.State.NOT_CONNECTED)
            {
                // Bluetooth client disconnected, make sure we discard the old wifi client
                HasWifi = false;
                wifi = null;
            }
        }


        /// <summary>
        /// This method is called as a callback by the bluetooth client when it gets the wifi negotiation handshake.
        /// </summary>
        /// <param name="handshake">The wifi negotiation handshake we recieved from the server</param>
        private void WifiCallback(byte[] handshake)
        {
            if (handshake == null)
            {
                HasWifi = false;
                wifi = null;
            } else
            {
                wifi = new WifiClient(handshake);
                HasWifi = true;
                if (bt.state == BluetoothClient.State.CONNECTED)
                {
                    handler.UpdateStatus("Connected (with wifi)");
                }
            }
            if (bt.state == BluetoothClient.State.CONNECTED)
                Screenshot(); // Ask for a screenshot, since the handshake is now complete
        }

        
        /// <summary>
        /// Connect to the server. This just calls BluetoothClient.Connect()
        /// </summary>
        /// <param name="device">The bluetooth device to connect to.</param>
        /// <returns></returns>
        public bool Connect(BluetoothDevice device)
        {
            return bt.Connect(device);
        }

        /// <summary>
        /// Disconnect from the server. This just calls BluetoothClient.Disconnect()
        /// </summary>
        /// <param name="forever">If set to true it means we never want to connect again</param>
        /// <returns></returns>
        public void Disconnect(bool forever = false)
        {
            bt.Disconnect(forever);
        }

        
        /// <summary>
        /// send the "up" command, to eventually emit a PageUp press
        /// </summary>
        public void Up() {
            bt.Send("up");
        }

        /// <summary>
        /// send the "dn" command, to eventually emit a PageDown press
        /// </summary>
        public void Down()
        {
            bt.Send("dn");
        }


        /// <summary>
        /// Get a screenshot, either via wifi or via bluetooth
        /// </summary>
        public async void Screenshot()
        {
            if (HasWifi)
            {
                // wifi connection is enabled, try getting a screenshot via wifi first.
                Task<byte[]> task = wifi.GetScreenshot();
                CancellationTokenSource CancelDelay = new CancellationTokenSource();
                CancellationTokenSource CancelFetch = new CancellationTokenSource();
                // Wait maximum 1.5s for the screenshot over wifi
                if (await Task.WhenAny(task, Task.Delay(1500, CancelDelay.Token)) == task)
                {
                    // GetScreenshot() returned before the timeout, let's check the result
                    byte[] picbuf = await task;
                    CancelDelay.Cancel(); // Cancel the timeout delay, it's no longer needed
                    if (picbuf == null)
                    {
                        // Wifi failed somehow, fallback to bluetooth and disable the wifi client
                        handler.UpdateStatus("Connected (wifi errors)");
                        HasWifi = false;
                        wifi = null;
                        bt.Send("sc");
                    }
                    else
                    {
                        // Successfully got a screenshot over wifi
                        Bitmap bitmap = BitmapFactory.DecodeByteArray(picbuf, 0, picbuf.Length);
                        Console.WriteLine("Bitmap (from wifi) ready, sending over to main thread");
                        handler.ObtainMessage((int)UIUpdateHandler.MessageType.BITMAP, bitmap).SendToTarget();
                    }
                }
                else
                {
                    // Timeout from wifi... fallback to bluetooth
                    WifiTimeoutCount++;
                    bt.Send("sc");
                    if (bt.state == BluetoothClient.State.CONNECTED)
                        handler.UpdateStatus("Connected (flakey wifi?)");
                    if (WifiTimeoutCount > 2)
                    {
                        // WiFi timed-out three times, and therefor is not to be trusted.
                        HasWifi = false;
                        wifi = null;
                        if (bt.state == BluetoothClient.State.CONNECTED)
                            handler.UpdateStatus("Connected (wifi disabled)");
                    }
                }
            } else
            {
                // No wifi, just ask for a screenshot over Bluetooth
                bt.Send("sc");
            }
        }
    }
}