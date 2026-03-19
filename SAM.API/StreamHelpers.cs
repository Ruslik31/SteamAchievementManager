/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAM.API
{
    internal static class StreamHelpers
    {
        public static string ReadNullTerminatedString(this Stream stream)
        {
            var bytes = new List<byte>();
            while (stream.Position < stream.Length)
            {
                int b = stream.ReadByte();
                if (b <= 0) break;
                bytes.Add((byte)b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static int ReadValueInt32(this Stream stream)
        {
            byte[] bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static uint ReadValueUInt32(this Stream stream)
        {
            byte[] bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static float ReadValueFloat32(this Stream stream)
        {
            byte[] bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            return BitConverter.ToSingle(bytes, 0);
        }

        public static ulong ReadValueUInt64(this Stream stream)
        {
            byte[] bytes = new byte[8];
            stream.Read(bytes, 0, 8);
            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}
