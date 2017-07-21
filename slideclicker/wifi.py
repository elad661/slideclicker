# wifi.py - wifi (and http) releated functionality for slideclicker
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

""" A simple HTTP server that allows to get a screenshot over HTTP instead of bluetooth

WARNING WARNING WARNING
Do not use this module on open/untrusted wifi networks under any circumstances
It does not use any kind of encryption, and while hmac should protect against
a common script-kiddie, it's probably not safe enough, and doesn't really
protect against DoS attacks either.

The threat model is either someone who wants to spy on your slides,
or someone who wants to screw up your talk by jamming the clicker with a DoS
attack. While both of these are unlikely, it's better to be safe than sorry."""

import base64
import gi
import hashlib
import hmac
import json
import logging
import os
import secrets
import socket
import time
from datetime import datetime
from threading import Thread
from .screenshot import take_screenshot, bytesIO_len
from http.server import HTTPServer, BaseHTTPRequestHandler
gi.require_version('NM', '1.0')
from gi.repository import NM

logger = logging.getLogger("wifi")

def get_ssid(connection):
    """ Get the SSID for a libnm connection """
    return connection.get_connection().get_setting_wireless().get_ssid()\
                                                             .get_data()


def get_ip(connection):
    """ Get the IP address for a libnm connection """
    # Assuming IPv4 only, because supporting both v6 and v4 is too much
    # added complexity for this project.
    return connection.get_ip4_config().get_addresses()[0].get_address()


def is_open_wifi(connection):
    """ Check if a wifi connection is an open one """
    security = connection.get_connection().get_setting_wireless_security()
    return security.get_key_mgmt() not in ['wpa-none', 'wpa-psk', 'wpa-eap']


def get_wifi_info():
    """ Return active WiFi SSIDs and IP Addresses """
    ret = []
    nm = NM.Client.new()
    for connection in nm.get_active_connections():
        if connection.get_connection_type() == '802-11-wireless':
            if not is_open_wifi(connection):  # Don't offer open wifi networks
                ret.append({'ip': get_ip(connection),
                            'ssid': get_ssid(connection)})
    return ret


def upgrade_connection(fd, client_info):
    """ if on the same wifi - start the server, and tell the client we're up """
    found_connection = None
    client_info = json.loads(client_info)
    if not client_info['wifi']:
        logger.warn("wifi functionality is enabled, but this computer is not connected to any (secure) wifi network")
        return  # Client is not connected to any wifi network

    # Find out if we're connected to the same wifi network as the client
    for connection in get_wifi_info():
        if connection['ssid'].decode() == client_info['ssid']:
            found_connection = connection
            break   # We're on the same network, and this is it

    if found_connection is None:
        logger.warn("wifi functionality is enabled, but the client is not on the same network")
        # We don't need to send the client anything if we're not on the same network
        return

    # Okay, so we're on the same wifi
    # now we need to generate a key for use in HMAC, and send it to the client,
    # and then start a simple HTTP server that will send a screenshot to an
    # authenticated client

    key_bytes = secrets.token_bytes(32)  # this is what we'll use in the HMAC
    key_b64 = base64.b64encode(key_bytes).decode()  # this is what we'll send to the client

    # port 0 = bind to a random free port
    server = ServerThread((found_connection['ip'], 0), key_bytes)
    server.start()

    uri = 'http://%s:%s' % server.addr
    logger.info("Sending wifi upgrade handshake. Running on %s" % uri)
    response = json.dumps({'key': key_b64, 'uri': uri}).encode()
    padded_len = str(len(response)).zfill(4).encode()
    # response format: 'wifi', 4 bytes of response length, response
    os.write(fd, b'wifi' + padded_len + response)

    return server

# Python threading is obviously not the most efficient way to do this
# but since this program already uses the glib mainloop, I can't use an
# asyncio server, and implementing a GLib based server would require a lot more
# code and be generally ugly.
# Performance doesn't matter much as long as we can send the picture in
# a reasonable time without slowing pageup/pagedown handling too much


class ServerThread(Thread):
    def __init__(self, addr, key, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._daemonic = True
        self.key = key
        self.server = HTTPServer(addr, request_handler_factory(key))
        self.addr = self.server.socket.getsockname()

    def run(self):
        self.server.serve_forever()

    def stop(self):
        self.server.shutdown()

seen_nonces = set()


def request_handler_factory(hmac_key):
    """ A factory to create a RequestHandler that knows the hmac key """
    class RequestHandler(BaseHTTPRequestHandler):
        """ A simple HTTP request handler that validates the HMAC signature """

        def do_HEAD(self):
            self.send_response(200)
            self.end_headers()

        def do_GET(self):
            if 'X-Hmac' not in self.headers or 'X-Timestamp-nonce' not in self.headers:
                self.send_error(401, "Not Authorized", "Authentication failure")
                return
            signature = self.headers['X-Hmac'].strip()
            msg = self.headers['X-Timestamp-nonce'].strip()

            timestamp, nonce = msg.split(',')
            utcnow = datetime.utcnow().timestamp()
            if utcnow - float(timestamp) > 5:
                # protect against replay attacks - refuse stale requests
                self.send_error(401, "Not Authorized", "Authentication failure")
                return

            if nonce in seen_nonces:
                # protect against replay attacks - don't allow nonce reuse
                self.send_error(401, "Not Authorized", "Authentication failure")
                return

            valid_signature = base64.b64encode(hmac.new(hmac_key,
                                                        msg.encode(),
                                                        hashlib.sha256).digest())
            valid_signature = valid_signature.decode()

            if not hmac.compare_digest(signature, valid_signature):
                self.send_error(401, "Not Authorized", "Authentication failure")
                return

            seen_nonces.add(nonce)

            # if we got here, authenction succeeded - now we can
            # send the screenshot

            # sleep because sometimes the client is too fast
            # and asks for screenshots before slides had time to change
            time.sleep(0.3)
            with take_screenshot() as f:
                self.send_response(200)
                self.send_header("Content-Type", f.content_type)
                self.send_header("Content-Length", bytesIO_len(f))
                self.end_headers()
                self.wfile.write(f.read())

    return RequestHandler

