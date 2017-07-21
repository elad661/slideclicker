#!/bin/python3
# slideclicker - a simple, versatile slideshow clicker
#
# Copyright (C) 2017 Elad Alfassa <elad@fedoraproject.org>
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <http://www.gnu.org/licenses/>.


import dbus
import argparse
import slideclicker.logging_config
import slideclicker.bluetooth
from gi.repository import GLib
from slideclicker.bluetooth import Watcher
from slideclicker.bluetooth_server import register_profile


BT_UUID = '00001101-0000-1000-8000-00805F9B34FB'
DBUS_PATH = '/com/eladalfassa/slideclicker/BluetoothProfile'


def main():
    parser = argparse.ArgumentParser(description="a simple slideshow clicker")
    parser.add_argument('--enable-wifi', action='store_true',
                        help='Enable sending the screenshots for the presenter'
                             ' view over wifi instead of bluetooth. DO NOT '
                             'enable this on public/untrusted networks')
    args = parser.parse_args()
    if args.enable_wifi:
        print("wifi functionality enabled")
        print("WARNING! Do not use this feature on public or otherwise untrusted networks")
        slideclicker.bluetooth.WIFI_UPGRADE_ENABLED = True

    slideclicker.logging_config.get("main").info("Starting...")

    dbus.mainloop.glib.DBusGMainLoop(set_as_default=True)
    profile = register_profile(DBUS_PATH, BT_UUID, Watcher)
    mainloop = GLib.MainLoop()
    try:
        mainloop.run()
    finally:
        profile.Release()  # make sure all file descriptors are closed


if __name__ == "__main__":
    main()
