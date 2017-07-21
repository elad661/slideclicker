# screenshot.py - get a screenshot from the GNOME Shell DBus API and downscale if needed
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

""" Take a screenshot from gnome-shell, return it as a bytesIO object """

import base64
import os
import tempfile
from io import BytesIO
from PIL import Image
from pydbus import SessionBus

bus = SessionBus()
sc = bus.get("org.gnome.Shell.Screenshot")


def bytesIO_len(data):
    """ Get the length of a BytesIO object """
    ret = data.seek(0, 2)  # Seek to the end, keep the end position
    data.seek(0)  # Seek back to the start
    return ret


def thumbnail(file_path, max_size, jpeg_quality):
    """ return a thumbnail for a file with the given parameters """
    img = Image.open(file_path)
    img.thumbnail(max_size)
    # Attempt saving as both JPEG and PNG, return the smaller of the two
    dataJPG = BytesIO()
    dataJPG.content_type = "image/jpeg"
    dataPNG = BytesIO()
    dataPNG.content_type = "image/png"
    img.save(dataJPG, 'jpeg', progressive=True, optimize=True, quality=jpeg_quality)
    img.save(dataPNG, 'png', optimize=True)
    if bytesIO_len(dataPNG) >= bytesIO_len(dataJPG):
        return dataJPG
    else:
        return dataPNG


def take_screenshot(downscale=True, superlowres=False):
    """ Take a screenshot from gnome-shell, return it as a new BytesIO object.
    Result may be other PNG or JPEG, whatever is smaller for the current screenshot"""
    tmpfile = tempfile.mkstemp(prefix="slideclicker_", suffix=".png")
    os.close(tmpfile[0])  # we don't need the fd from mkstemp()
    try:
        # Take a screenshot, with no border, cursor, or flash
        result = sc.ScreenshotWindow(False, False, False, tmpfile[1])
        if not result[0]:
            raise Exception("Screenshot failed!")
        if not downscale:
            with open(tmpfile[1], 'rb') as f:
                data = BytesIO(f.read())
            return data
        else:
            # These numbers were selected for peformance reasons
            if superlowres:
                max_size = (256, 144)
                jpeg_quality = 60
            else:
                max_size = (512, 288)
                jpeg_quality = 60
            return thumbnail(tmpfile[1], max_size, jpeg_quality)
    finally:
        # Delete the temporary screenshot file, it's not needed anymore
        os.remove(tmpfile[1])
