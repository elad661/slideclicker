# logging.py - logging functionality for slideclicker
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
""" Logging functionality for slideclicker """

import logging

fomatstr = '%(asctime)s : %(name)s: %(levelname)s: %(message)s'
datefmt = "%Y-%m-%d %H:%M:%S"

logging.basicConfig(level=logging.INFO,
                    format=fomatstr,
                    datefmt=datefmt,
                    filename="slideclicker.log")

formatter = logging.Formatter(fomatstr, datefmt=datefmt)

console = logging.StreamHandler()
console.setLevel(logging.INFO)
console.setFormatter(formatter)
logging.getLogger('').addHandler(console)


def get(name):
    """ Alias for logging.getLogger(name) """
    return logging.getLogger(name)

