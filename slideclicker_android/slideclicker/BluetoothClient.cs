/*
BluetoothClient.cs - bluetooth client implementation for slideclicker

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
using System.IO;
using System.Text;
using Android.Content;
using Android.Bluetooth;
using Java.Util;
using System.Threading;
using System.Threading.Tasks;
using Android.Graphics;
using System.Collections.Concurrent;

namespace slideclicker
{

    class BluetoothClient
    {
        public BluetoothDevice device;
        public enum State { NOT_CONNECTED, CONNECTING, CONNECTED };
        public State state = State.NOT_CONNECTED;
        public bool paused = false;
        private static UUID MY_UUID = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
        private UIUpdateHandler handler;
        private enum RecieveState { PINGPONG, PICTURE }
        private RecieveState _recieve_state = RecieveState.PINGPONG;
        private DateTime last_ping;
        private bool ping_running = false;
        private BluetoothSocket sock;
        private Stream InStream;
        private Stream OutStream;
        private Thread PingThread;
        private Thread SendThread;
        private Thread RecieveThread;
        private ConcurrentQueue<string> SendQueue;
        private AutoResetEvent SendPending = new AutoResetEvent(false);
        private AutoResetEvent StateChangedEvent = new AutoResetEvent(false);
        private Context context;
        private Action<byte[]> WifiCallback;
        private Action<State> StateChangedCallback;

        public BluetoothClient(Context context, UIUpdateHandler handler, Action<byte[]> WifiCallback, Action<State> StateChangedCallback)
        {
            this.handler = handler;
            this.context = context;
            this.WifiCallback = WifiCallback;
            this.StateChangedCallback = StateChangedCallback;
            SendQueue = new ConcurrentQueue<String>();
            RecieveThread = new Thread(Recieve);
            SendThread = new Thread(ProcessSendQueue);
        }


        /// <summary>
        /// Connect to a BluetoothDevice
        /// </summary>
        /// <param name="device">The device you want to connect to</param>
        /// <returns></returns>
        public bool Connect(BluetoothDevice device)
        {
            if (state != State.NOT_CONNECTED || device == null || paused)
                return true; // Don't try to connect again if the connection is open, don't try to connect to null devices, and don't try to connect when paused
            lock ("connectlock")
            {
                Console.WriteLine("Connecting to " + device);
                SetState(State.CONNECTING);
                this.device = device;
                sock = device.CreateRfcommSocketToServiceRecord(MY_UUID);
                try
                {
                    sock.Connect();
                }
                catch (Java.IO.IOException)
                {
                    SetState(State.NOT_CONNECTED);
                    handler.UpdateStatus("Connection failed");
                    Console.WriteLine("Crap, connection failed");
                    return false;
                }
                Console.WriteLine("Probably connected");
                OutStream = sock.OutputStream;
                InStream = sock.InputStream;
                SetState(State.CONNECTED);
                last_ping = DateTime.UtcNow;
                if (!RecieveThread.IsAlive)
                {
                    RecieveThread.Start();
                }
                // we don't use Send() here because we want it to be sent first
                Byte[] handshake = Encoding.UTF8.GetBytes(WifiClient.WifiHandshake(context));
                OutStream.Write(handshake, 0, handshake.Length);
                if (SendQueue.Count > 0)
                    Send(); // Poke the send thread so we can make sure the queue is proccessed
                StartPing();
                return true;
            }
        }


        /// <summary>
        /// Cleanly the connection and stop the ping thread.
        /// </summary>
        /// <param name="forever">If true, set "device" to null so no further connections would be possible without showing the device selection dialog again</param>
        public void Disconnect(bool forever = false)
        {
            if (state != State.NOT_CONNECTED)
            {
                handler.UpdateStatus("Disconnecting");
                if (ping_running)
                {
                    ping_running = false;
                    PingThread.Abort();
                }
                if (state == State.CONNECTED)
                    Send("di");
                last_ping = DateTime.UtcNow;
                SetState(State.NOT_CONNECTED);
                if (RecieveThread.IsAlive && forever)
                    RecieveThread.Abort();
                lock (InStream)
                {
                    InStream.Close();
                    sock.Close();
                }
                if (forever)
                    device = null;
            }
        }

        private void SetState(State value)
        {
            state = value;
            switch (value)
            {
                case State.CONNECTED:
                    handler.UpdateStatus("Connected");
                    break;
                case State.CONNECTING:
                    handler.UpdateStatus("Connecting...");
                    break;
                case State.NOT_CONNECTED:
                    handler.UpdateStatus("Not connected");
                    break;
            }
            StateChangedCallback(value);
            StateChangedEvent.Set();
        }

        void StartPing()
        {
            lock ("pinglock")
            {
                if (!ping_running)
                {
                    ping_running = true;
                    PingThread = new Thread(Ping);
                    PingThread.Start();
                }
            }
        }

        void Ping() // Make sure the connection stays alive, and reconnect it if it dies
        {
            TimeSpan nineSeconds = TimeSpan.FromSeconds(9);
            while (ping_running)
            {
                if (state == State.CONNECTED && _recieve_state == RecieveState.PINGPONG && DateTime.UtcNow.Subtract(last_ping) > nineSeconds)
                {
                    Send("pi");
                    Thread.Sleep(10000); // Ping every 10 seconds
                }
                else if (state == State.NOT_CONNECTED && device != null && !paused)
                {
                    Console.WriteLine("Not connected, trying to reconnect...");
                    Connect(this.device);
                    Thread.Sleep(1500);
                }
            }
        }
        void Recieve()
        {
            while (true)
            {
                try
                {
                    if (paused)
                        continue;
                    if (state != State.CONNECTED)
                    {
                        StateChangedEvent.WaitOne();
                        continue;
                    }
                    byte[] buf = new byte[4]; // 4 bytes for "pic:" or "pong"
                    int expected_size = 0;
                    if (_recieve_state == RecieveState.PINGPONG)
                    {
                        lock (InStream)
                        {
                            InStream.Read(buf, 0, buf.Length);
                        }
                        last_ping = DateTime.UtcNow;
                        String command = Encoding.ASCII.GetString(buf).Trim();
                        if (command.StartsWith("pic"))
                        {
                            buf = new byte[8]; // 8 bytes for image size
                            InStream.Read(buf, 0, buf.Length);
                            expected_size = Int32.Parse(Encoding.ASCII.GetString(buf).Trim());
                            Console.WriteLine("Expecting a picture, {0} bytes", expected_size);
                            _recieve_state = RecieveState.PICTURE;
                        }
                        else if (command.StartsWith("wifi"))
                        {
                            Console.WriteLine(command);
                            buf = new byte[4]; // 4 bytes for recieved handshake json length
                            InStream.Read(buf, 0, buf.Length);
                            Console.WriteLine(Encoding.ASCII.GetString(buf));
                            expected_size = Int32.Parse(Encoding.ASCII.GetString(buf).Trim());
                            buf = Common.ReadAll(InStream, expected_size);
                            WifiCallback(buf);
                        }
                        else if (command != "pong")
                        {
                            // Unexpected data in buffer...
                            // Recover by reconnecting?
                            Console.WriteLine("junk on the buffer :( '" + command + "'");
                            Disconnect();
                            Connect(this.device);
                        }
                    }
                    // This is a regular if, not an else if!
                    if (_recieve_state == RecieveState.PICTURE)
                    {
                        byte[] picbuf = Common.ReadAll(InStream, expected_size, 8192 * 2);
                        Console.WriteLine("Recieved picture? {0} bytes", picbuf.Length);
                        Bitmap bitmap = BitmapFactory.DecodeByteArray(picbuf, 0, picbuf.Length);
                        Console.WriteLine("Bitmap (hopefully) ready, sending over to main thread");
                        handler.ObtainMessage(0, bitmap).SendToTarget();
                        _recieve_state = RecieveState.PINGPONG;
                    }
                }
                catch (Java.IO.IOException e)
                {
                    Console.WriteLine("Recieving failed, connection might be lost");
                    Console.WriteLine(e);
                    SetState(State.NOT_CONNECTED);
                    Connect(device);
                }
            }


        }

        public void Send(string str="")
        {
            if (state != State.CONNECTED)
            {
                Console.WriteLine("Can't send anything when not connected, trying to connect");
                new Task(() => { while (!Connect(device)) { Console.WriteLine("retrying..."); } }).Start();
            }
            if (str.Length > 0)
                SendQueue.Enqueue(str);
            SendPending.Set();
            lock (SendThread) // So we don't get multiple send threads accidentally
            {
                if (state == State.CONNECTED && !SendThread.IsAlive)
                {
                    if (SendThread.ThreadState != ThreadState.Unstarted)
                        SendThread = new Thread(ProcessSendQueue);
                    SendThread.Start();
                }
            }
        }

        private void ProcessSendQueue()
        {
            while (state == State.CONNECTED && SendQueue.TryDequeue(out string str))
            {
                Byte[] bytes = Encoding.UTF8.GetBytes(str);
                try
                {
                    OutStream.Write(bytes, 0, bytes.Length);
                }
                catch (Java.IO.IOException e)
                {
                    Console.WriteLine("Sending failed, connection might be lost");
                    Console.WriteLine(e);
                    SetState(State.NOT_CONNECTED);
                    new Task(() => { Connect(this.device); }).Start();
                    return;
                }
                OutStream.Flush();
                Console.WriteLine("sent: " + str);
                last_ping = DateTime.UtcNow;
                if (SendQueue.Count == 0)
                    SendPending.WaitOne(TimeSpan.FromMinutes(1)); // wait for more data, up to one minute
            }
        }
    }
}