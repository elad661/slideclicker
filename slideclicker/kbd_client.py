# kbd_client.py - a client for the keyboard server (you can find the server in kbd_server.py)
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


""" A simple client for the fake-keyboard server """

import socket
import os
from time import sleep
SOCKET_PATH = '/run/slideclicker_socket'


class KeyboardClient(object):
    def __init__(self):
        self.socket = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        if not os.path.exists(SOCKET_PATH):
            raise Exception("Socket doesn't exist, is the server running?")
        self.socket.connect(SOCKET_PATH)

    def _send(self, what):
        if what != b"up" and what != b"dn":
            raise Exception("Invalid command")
        print(what)
        self.socket.sendall(what)

    def pageup(self):
        self._send(b"up")

    def pagedown(self):
        self._send(b"dn")

    def close(self):
        self.socket.close()

if __name__ == "__main__":
    # temporary test
    print("Connecting")
    kbd = KeyboardClient()
    print("Sleeping so you can open evince")
    sleep(5)
    kbd.pagedown()
    sleep(1)
    kbd.pageup()
    sleep(2)
    kbd.pagedown()
    sleep(5)
    kbd.close()
