/*
Slideclicker is a simple (yet hacky) slideshow clicker for Linux and Android

MainActivity.cs - slideclicker's UI code

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
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Bluetooth;
using Android.Graphics;
using System.Threading.Tasks;
using System.Threading;

namespace slideclicker
{
    [Activity(Label = "slideclicker",
              MainLauncher = true,
              Icon = "@drawable/icon",
              Theme = "@android:style/Theme.Material")]
    public class MainActivity : Activity, GestureDetector.IOnGestureListener
    {
        private GestureDetector _gestureDetector;
        private const int REQUEST_ENABLE_BT = 2;
        protected BluetoothAdapter BTAdapter;
        private Client client;
        private long LastClickTime = 0;
        private bool ScreenshotThreadRunning = false;
        private Vibrator vibrator;
        private TextView StatusTextView;
        private void Vibrate()
        {
            vibrator.Vibrate(15);
        }

        private void ScreenshotThread()
        {
            // Avoid asking for too many screenshots when next / prev are pressed in a rapid succession
            // This thread will run to make sure we do end up asking for a screenshot after the user is done mashing the button
            ScreenshotThreadRunning = true;
            while (SystemClock.ElapsedRealtime() - LastClickTime < 1000)
            {
                Thread.Sleep(500);
            }
            client.Screenshot();
            ScreenshotThreadRunning = false;
        }
        private void GetScreenshotIfNeeded()
        {
            if (!ScreenshotThreadRunning && SystemClock.ElapsedRealtime() - LastClickTime > 1000)
                client.Screenshot(); // More than a second passed since the last click, so let's ask for a screenshot
            else
            {
                // many clicks in a rapid succession, spawn a thread to wait for when the user is done clicking
                if (!ScreenshotThreadRunning)
                    new Thread(() => { ScreenshotThread(); }).Start();
            }
            LastClickTime = SystemClock.ElapsedRealtime();

        }

        private void NextSlide()
        {
            Vibrate();
            Console.WriteLine("Next!");
            client.Down();
            GetScreenshotIfNeeded();
        }
        private void PrevSlide()
        {
            Vibrate();
            Console.WriteLine("Previous");
            client.Up();
            GetScreenshotIfNeeded();
        }


        void ShowDeviceList()
        {
            BluetoothDevicePicker picker = new BluetoothDevicePicker(this);
            picker.PickDevice((BluetoothDevice pickedDevice) => {
                StatusTextView.Text = "Device selected";
                new Task(() => { while(!client.Connect(pickedDevice)); }).Start();
            });
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            _gestureDetector = new GestureDetector(this);
            SetContentView(Resource.Layout.Main);

            Button btn_next = FindViewById<Button>(Resource.Id.btn_next);
            Button btn_prev = FindViewById<Button>(Resource.Id.btn_previous);
            btn_next.Click += (object sender, EventArgs e) => { NextSlide(); };
            btn_prev.Click += (object sender, EventArgs e) => { PrevSlide(); };

            ImageView imageview = FindViewById<ImageView>(Resource.Id.imageView1);
            StatusTextView = FindViewById<TextView>(Resource.Id.status);

            client = new Client(this, imageview, StatusTextView);

            vibrator = (Vibrator)GetSystemService(VibratorService);

            BTAdapter = BluetoothAdapter.DefaultAdapter;
            // If BT is not on, request that it be enabled.
            if (!BTAdapter.IsEnabled)
            {
                Intent enableIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                StartActivityForResult(enableIntent, REQUEST_ENABLE_BT);
            }
            else
            {
                ShowDeviceList();
            }
        }

        protected override void OnDestroy()
        {
            client.Disconnect(true);
            base.OnDestroy();
        }

        // Bluetooth enabling request callback
        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (resultCode == Result.Ok)
            {
                // Bluetooth is now enabled, show device selection
                ShowDeviceList();
            }
            else
            {
                // User did not enable Bluetooth or an error occured
                Finish();
            }
        }

        /* gesture handling stuff */
        public bool OnDown(MotionEvent e)
        {
            return false;
        }
        public bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
            Console.WriteLine("Fling: x=" + velocityX + ", y=" + velocityY);
            if (velocityX < 0)
            {
                NextSlide();
            } else if (velocityX > 0)
            {
                PrevSlide();
            }
            return true;
        }
        public void OnLongPress(MotionEvent e) { }
        public bool OnScroll(MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
        {
            return false;
        }
        public void OnShowPress(MotionEvent e) { }
        public bool OnSingleTapUp(MotionEvent e)
        {
            return false;
        }
        public override bool OnTouchEvent(MotionEvent e)
        {
            _gestureDetector.OnTouchEvent(e);
            return false;
        }
    }
}

