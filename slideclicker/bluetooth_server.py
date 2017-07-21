# bluetooth_server.py - a simple bluetooth RFCOMM server using the Bluez 5 DBus API
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

""" A bluetooth server using the BlueZ 5 DBus API """

# Note: I used dbus here and not pydbus like in screenshot.py because
# while pydbus is easier for simple clients, it gets messy once you want to
# export your own dbus object

from gi.repository import GLib
import os
import dbus
import dbus.service
import dbus.mainloop.glib
from gi.repository import GObject


class IOWatcher(object):
    """ Use this class to watch for IO on an fd, it calls a callback when
        there's new data waiting to be read.

        To use this class, subclass it and override `io_callback`
        to handle incoming data, and then call start() to start watching
        for new data. Make sure to call stop() when you're done to close
        the fd and remove the watch.
        """
    def __init__(self, fd, path):
        self.fd = fd
        self.watch_id = None
        self.hup_watch = None
        self.channel = None
        self.dbus_path = path

    def stop(self):
        """ Stop the watcher and close the file descriptor """
        if self.watch_id is not None:
            GLib.source_remove(self.watch_id)  # remove the watch
            self.watch_id = None

        if self.hup_watch is not None:
            GLib.source_remove(self.hup_watch)  # remove the watch
            self.hup_watch = None

        if self.channel is not None:
            self.channel.close()
            self.channel = None

        if self.fd is not None:
            try:
                os.close(self.fd)  # Close the file descriptor
            except OSError:
                pass  # Don't care if it's already closed
            self.fd = None

    def start(self):
        """ Set a GLib watch on the file descriptor """
        print("starting watcher on fd " + str(self.fd))
        channel = GLib.IOChannel.unix_new(self.fd)
        self.channel = channel

        self.watch_id = GLib.io_add_watch(channel,
                                          GLib.PRIORITY_DEFAULT,
                                          GLib.IO_IN,
                                          self._callback_wrapper)
        self.hup_watch = GLib.io_add_watch(channel,
                                           GLib.PRIORITY_DEFAULT,
                                           GLib.IO_HUP,
                                           self.hup_callback)
        print(self.watch_id)

    def _callback_wrapper(self, channel, cond):
        if self.watch_id is None:
            # Don't call user callback if we just closed this watcher
            return False

        fd = channel.unix_get_fd()
        return self.io_callback(fd, cond)

    def hup_callback(self, fd, cond):
        """ Called by glib when we get HUP on the fd """
        self.stop()

    def io_callback(self, fd, cond):
        """ Called from GLib when it detects input"""
        raise NotImplementedError("Not implemented. "
                                  "you need to override this in a subclass.")


class BluezProfile(dbus.service.Object):
    def __init__(self, watcher_class, conn=None, object_path=None, bus_name=None):
        super().__init__(conn, object_path, bus_name)
        self.fd = None
        self.watcher_class = watcher_class
        self.connections = {}

    @dbus.service.method("org.bluez.Profile1", in_signature="oha{sv}")
    def NewConnection(self, path, fd, properties):
        """ Called when a new connection is established """
        self.fd = fd.take()
        print("Connection established!")
        watcher = self.watcher_class(self.fd, path)
        watcher.start()
        self.connections[path] = watcher

    @dbus.service.method("org.bluez.Profile1", in_signature="o")
    def RequestDisconnection(self, path):
        """ Handle disconnection """
        connection = self.connections.pop(path)
        connection.stop()

    @dbus.service.method("org.bluez.Profile1")
    def Release(self):
        """ Called when the service daemon unregisters the profile """
        print("bye")
        for connection in self.connections.values():
            connection.stop()


def register_profile(dbus_path, uuid, watcher_class):
    """ Register a new Bluez Profile"""
    bus = dbus.SystemBus()
    manager = dbus.Interface(bus.get_object("org.bluez", "/org/bluez"),
                             "org.bluez.ProfileManager1")

    profile = BluezProfile(watcher_class, bus, dbus_path)

    manager.RegisterProfile(dbus_path, uuid, {"Role": "server",
                                              "RequireAuthentication": True,
                                              "RequireAuthorization": True,
                                              "AutoConnect": True})
    print("registered, waiting for connections")
    return profile
