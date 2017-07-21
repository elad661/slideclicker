/*
Common.cs - common helper methods

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

using System.IO;

namespace slideclicker
{

    /// <summary>
    /// Common helper methods
    /// </summary>
    class Common
    {

        /// <summary>
        /// A helper method to efficiently and easily read from a Stream.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="expected_length">How much data to read, useful when reading from network streams. Set to -1 to read until the end. Defaults to -1.</param>
        /// <param name="buffer_size">Buffer/chunk size for reading from the stream. Defaults to 32K</param>
        /// <returns></returns>
        public static byte[] ReadAll(Stream stream, int expected_length=-1, int buffer_size = 32*1024)
        {
            byte[] buffer = new byte[buffer_size];
            using (MemoryStream memorystream = new MemoryStream())
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memorystream.Write(buffer, 0, read);

                    if (expected_length > -1 && memorystream.Length >= expected_length)
                        break;
                }
                return memorystream.ToArray();
            }
        }
    }
}