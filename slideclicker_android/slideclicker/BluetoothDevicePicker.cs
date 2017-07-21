/*
BluetoothDevicePicker.cs - helper methods for launching the Android system bluetooth picker activity

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
using Android.Bluetooth;

namespace slideclicker
{

    /// <summary>
    /// A simple helper to launch the Android system bluetooth device picker activity.
    /// </summary>
    class BluetoothDevicePicker
    {
        const string LAUNCH = "android.bluetooth.devicepicker.action.LAUNCH";
        const string FILTER_DEVICE_SELECTED = "android.bluetooth.devicepicker.action.DEVICE_SELECTED";
        const string NEED_AUTH = "android.bluetooth.devicepicker.extra.NEED_AUTH";
        const string FILTER_TYPE = "android.bluetooth.devicepicker.extra.FILTER_TYPE";
        const string LAUNCH_PACKAGE = "android.bluetooth.devicepicker.extra.LAUNCH_PACKAGE";
        const string LAUNCH_CLASS = "android.bluetooth.devicepicker.extra.DEVICE_PICKER_LAUNCH_CLASS";
        const int FILTER_TYPE_ALL = 0;

        Context context;
        

        /// <summary>
        /// Construct a new BluetoothDevicePicker with the specified context
        /// </summary>
        /// <param name="context">An android context, for example - your main activity</param>
        public BluetoothDevicePicker(Context context)
        {
            this.context = context;
        }
 
        
        /// <summary>
        /// Launch the device picker.
        /// </summary>
        /// <param name="callback">A method to call once a device has been picked</param>
        public void PickDevice(Action<BluetoothDevice> callback)
        {
            DeviceSelectionReciever Reciever = new DeviceSelectionReciever()
            {
                callback = callback // I'd put the callback in the constructor, but I couldn't get it to work
            };
            context.RegisterReceiver(Reciever, new IntentFilter(FILTER_DEVICE_SELECTED));

            Intent intent = new Intent(LAUNCH);
            intent.PutExtra(NEED_AUTH, true);
            intent.PutExtra(FILTER_TYPE, FILTER_TYPE_ALL);
            intent.SetFlags(ActivityFlags.ExcludeFromRecents);
            context.StartActivity(intent);
        }

        [BroadcastReceiver]
        private class DeviceSelectionReciever : BroadcastReceiver
        {
            public Action<BluetoothDevice> callback;

            public override void OnReceive(Context context, Intent intent)
            {
                context.UnregisterReceiver(this);
                BluetoothDevice device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                callback(device);
            }
        }
    }
}