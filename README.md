Elad's slideshow clicker
========================

This is slideclicker, it's a simple (and a bit hacky) slideshow clicker app for Linux and Android.

It uses uinput to simulate pageUp / pageDown key presses, so it works with any slideshow application.

The server side (which you need to run on your laptop) is written in Python, and the Android app is
written in C# using Xamarin (yes, that means you'll need Windows or Mac OS to compile the code. Sorry.)

Requirements
------------

 * python 3.6+
 * uinput (this means it only works on linux)
 * DBus
 * GObject Introspection for python
 * A bluetooth adapter
 * BlueZ 5
 * GNOME Shell (slideclicker uses the GNOME Shell for getting a screenshot of the current slide, to show on your phone as a presenter view. I did mention it's hacky)
 * A phone running Android 7.1+ (for the client. Theoretically could work with older versions, but I only had 7.1 to test with)
 * NetworkManager (if you want ot use the wifi functionality described below)
 * a bunch of python libraries listed in requirements.txt

What can it do?
---------------
Not much. It can emit pageUp and pageDown key presses, to move between slides, and show a screenshot of the current slide on your phone.

slideclicker communicates with your phone over bluetooth, but since bluetooth is slow the screenshots are in low quality.

If your phone and your laptop are on the same wifi network, slideclicker can use wifi for the screenshots, and then the quality
will be a little bit better. It'll still use bluetooth for the pageUp / pageDown key presses, and negotiate an "upgrade" to wifi and HTTP
for the screenshots - the phone will send the SSID its' connected to to the laptop, and if the laptop sees they're on the same SSID,
it'll start a new HTTP server on a random port and listen to screenshot requests from the phone. Requests will be signed using hmac
and a randomally generated key (that is generated on the laptop and sent over to the phone over bluetooth first), so it should be
reasonablly safe.

However! I do not recommend using this feature on untrusted or public networks. slideclicker will not start the HTTP server if you're
connected to an open wifi network as a precaution.

To enable the wifi feature pass --enable-wifi to main.py

Is it actually useful?
----------------------

For me? yes. For anyone else? probably not.

However, some code in slideclicker might be useful as an example. The following files might be of interest to you:

### On the server side:
 * slideclicker/bluetooth_server.py - Classes for implementing a bluetooth RFComm server using the BlueZ 5 DBus API and GLib.
 **Usable as-is for your python apps** (you don't need pybluez anymore).
 * slideclicker/bluetooth.py - Might be useful as an example implementation that makes use of the bluetooth_server.py library.
 * slideclicker/kbd_server.py - An example of a unix socket server using asyncio
 * slideclicker/wifi.py - Contains few functions that demonstrate usage of libnm in python

### On the client side (the phone app)
 * BluetoothClient.cs - An example of using Bluetooth RFCOMM on Xamarin. It's a bit messy, but might be easier to understand and more C#-ish than Xamarin's "bluetooth chat" example.
 * BluetoothDevicePicker.cs - Usable as-is, to launch the Android system bluetooth device selection screen instead of implementing one yourself.
 * WifiClient.cs - Contains a method to get the SSID for the current network, even if the phone is acting as a Wifi AP.

I still want to use this, how do I do that?
-------------------------------------------
Make sure bluetooth is turned on on both your laptop and your phone, make sure they are paired, and then

1. run `sudo modprobe uinput` to load the uinput module
2. run `sudo python3 slideclicker/kbd_server.py`
3. On a different terminal window, run `python3 main.py`
4. Start the android app, slect your laptop on the device list

That's not really a nice user experience, I admit. I might make it simpler in the future.

TODO (at some point, if I ever get around to it)
------------------------------------------------
* proper debug logging in the Android app instead of `Console.WriteLine()`
* a proper app icon for the Android app
* systemd service for the kbd_server
* An app launcher to launch the server
* maybe also using polkit to get premissions to load uinput and start the kbd_server
* some sort of indication that the server is running? like a persistent notification or a gnome-shell extension that adds a prominent icon.

License
-------
GPLv3+. See COPYING for the full license text.

Written by Elad Alfassa.
