# bluetooth.py - bluetooth server implementation for slideclicker
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

""" Bluetooth server implementation for slideclicker """

import os
import time
import json
import logging
from time import sleep
from gi.repository import GLib
from .kbd_client import KeyboardClient
from .bluetooth_server import IOWatcher
from .screenshot import take_screenshot
from .wifi import upgrade_connection

kbd = KeyboardClient()

logger = logging.getLogger("bluetooth")

WIFI_UPGRADE_ENABLED = False  # Set this to True to enable upgrading to Wifi


class Watcher(IOWatcher):
    def __init__(self, fd, path):
        super().__init__(fd, path)
        self.last_ping_time = time.time()
        self.got_hello = False
        self.http_server = None
        GLib.timeout_add_seconds(5, self.ping_checker)

    def ping_checker(self):
        """ Stop the connection if there's no activity for 11 seconds """
        if time.time() - self.last_ping_time > 11:
            logger.info("11 seconds with no activity, closing connection")
            self.stop()

    def send_screenshot(self, fd, hq=False):
        f = take_screenshot(superlowres=not hq)
        padded_len = str(len(f[0])).zfill(8)
        msg = 'pic:%s' % padded_len
        os.write(fd, msg.encode())
        os.write(fd, f[0])
        logger.debug("sent screenshot, %s bytes, hq=%s" % (len(f[0]), hq))

    def send_str(self, fd, s):
        os.write(fd, s.decode())

    def stop(self):
        logger.info("closing connection...")
        if self.http_server is not None:
            self.http_server.stop()
            self.http_server.join()
        super().stop()

    def hup_callback(self, fd, cond):
        logger.info("connection closed")  # overriding this just for the log
        super().hup_callback(fd, cond)

    def io_callback(self, fd, cond):
        logger.debug("io callback")
        self.last_ping_time = time.time()
        read_size = 2 if self.got_hello else 1024  # This is ugly
        command = os.read(fd, read_size)
        if not self.got_hello:
            # this is a hello command, it's the wifi upgrade handshake
            self.got_hello = True
            if WIFI_UPGRADE_ENABLED:
                self.http_server = upgrade_connection(fd, command)
            return True
        if command == b"up":
            kbd.pageup()
        elif command == b"dn":
            kbd.pagedown()
        elif command == b"pi":
            # got ping, sent pong
            os.write(fd, b'pong')
        elif command == b'di':
            # disconnect command recieved
            logger.info("client sent a disconnect command")
            self.stop()
        elif command == b'sc':
            # screenshot requested
            sleep(0.3)  # Sleep to give the slides time to change
            try:
                self.send_screenshot(fd)
            except Exception as e:
                logger.exception("Screenshot failed!")
        else:
            logger.error("did not understand command '%s'" % command)
        return True
