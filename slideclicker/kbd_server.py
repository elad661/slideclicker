# kbd_server.py - a simple service that can emit pageup / pagedown clicks via uinput
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

""" A fake-keyboard server that listens for events on a unix socket
    It has to run as root because uinput is root-only by default, but I don't
    want the bluetooth and screenshot code running as root, which is why this
    is a "service" and not done in the main process"""
import uinput
import asyncio
import os
import pwd
import grp
SOCKET_PATH = '/run/slideclicker_socket'


class FakeKeyboard(object):
    """ Our fake keyboard. It only has two keys: pageup and pagedown """
    def __init__(self):
        self.device = uinput.Device([uinput.KEY_PAGEDOWN, uinput.KEY_PAGEUP])

    def pageup(self):
        """ Emit a pageup keypress """
        print("pgup")
        self.device.emit_click(uinput.KEY_PAGEUP)

    def pagedown(self):
        """ Emit a pagedown keypress """
        print("pgdn")
        self.device.emit_click(uinput.KEY_PAGEDOWN)

    def close(self):
        self.device.destroy()


def drop_privilages():
    # Get the uid/gid from the name
    running_uid = pwd.getpwnam("nobody").pw_uid
    running_gid = grp.getgrnam("nobody").gr_gid

    # Remove group privileges
    os.setgroups([])

    # Try setting the new uid/gid
    os.setgid(running_gid)
    os.setuid(running_uid)

    # Set a safe umask
    os.umask(0o022)

kbd = None

async def handle_messages(reader, writer):
    """ Process messages from the client """
    running = True
    while running:
        command = await reader.read(2)
        print(command)
        if command == b"up":
            kbd.pageup()
        elif command == b"dn":
            kbd.pagedown()
        elif command == b"":
            print("client disconnected")
            running = False
            writer.close()
        else:
            writer.write(b"bad")
            await writer.drain()


def start_server():
    """ Start the server, and listen to requests until interrupted """
    global kbd
    kbd = FakeKeyboard()
    loop = asyncio.get_event_loop()
    coro = asyncio.start_unix_server(handle_messages, SOCKET_PATH, loop=loop)
    server = loop.run_until_complete(coro)
    os.chmod(SOCKET_PATH, 0o777)
    # drop_privilages()  # important!
    try:
        loop.run_forever()
    finally:
        server.close()
        loop.run_until_complete(server.wait_closed())
        loop.close()
        kbd.close()
        os.remove(SOCKET_PATH)
        print("bye")

if __name__ == "__main__":
    start_server()
