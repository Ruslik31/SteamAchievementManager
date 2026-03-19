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
using System.Linq;

namespace SAM.API
{
    public class KeyValue
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public List<KeyValue> Children { get; set; }

        public KeyValue this[string name]
        {
            get
            {
                if (this.Children == null)
                {
                    return null;
                }

                return this.Children.FirstOrDefault(
                    c => string.Equals(c.Name, name, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public string AsString(string defaultValue)
        {
            if (this.Value == null)
            {
                return defaultValue;
            }

            return this.Value.ToString();
        }

        public int AsInteger(int defaultValue)
        {
            if (this.Value == null)
            {
                return defaultValue;
            }

            if (this.Value is int intValue)
            {
                return intValue;
            }

            if (int.TryParse(this.Value.ToString(), out int parsed) == true)
            {
                return parsed;
            }

            return defaultValue;
        }

        public float AsFloat(float defaultValue)
        {
            if (this.Value == null)
            {
                return defaultValue;
            }

            if (this.Value is float floatValue)
            {
                return floatValue;
            }

            if (float.TryParse(this.Value.ToString(), out float parsed) == true)
            {
                return parsed;
            }

            return defaultValue;
        }

        public static KeyValue LoadAsData(Stream stream)
        {
            var kv = new KeyValue();
            kv.ReadAsData(stream);
            return kv;
        }

        public static KeyValue LoadAsBinary(string path)
        {
            if (File.Exists(path) == false)
            {
                return null;
            }

            using (var stream = File.OpenRead(path))
            {
                var kv = new KeyValue();
                kv.ReadAsBinary(stream);
                return kv;
            }
        }

        public void ReadAsData(Stream stream)
        {
            this.Name = null;
            this.Value = null;
            this.Children = new List<KeyValue>();

            while (stream.Position < stream.Length)
            {
                var type = (KeyValueType)stream.ReadByte();
                if (type == KeyValueType.End)
                {
                    break;
                }

                var child = new KeyValue
                {
                    Name = stream.ReadNullTerminatedString(),
                };

                switch (type)
                {
                    case KeyValueType.None:
                    {
                        child.ReadAsData(stream);
                        break;
                    }

                    case KeyValueType.String:
                    {
                        child.Value = stream.ReadNullTerminatedString();
                        break;
                    }

                    case KeyValueType.Int32:
                    case KeyValueType.Color:
                    case KeyValueType.Pointer:
                    case KeyValueType.UInt32:
                    {
                        child.Value = stream.ReadValueInt32();
                        break;
                    }

                    case KeyValueType.Float32:
                    {
                        child.Value = stream.ReadValueFloat32();
                        break;
                    }

                    case KeyValueType.WideString:
                    {
                        child.Value = null;
                        break;
                    }

                    case KeyValueType.UInt64:
                    case KeyValueType.Int64:
                    {
                        child.Value = stream.ReadValueUInt64();
                        break;
                    }

                    case KeyValueType.Boolean:
                    {
                        child.Value = stream.ReadByte() != 0;
                        break;
                    }

                    case KeyValueType.End:
                    {
                        child.Value = null;
                        break;
                    }

                    default:
                    {
                        child.Value = null;
                        break;
                    }
                }

                this.Children.Add(child);
            }
        }

        public void ReadAsBinary(Stream stream)
        {
            this.Name = stream.ReadNullTerminatedString();
            this.Value = null;
            this.Children = new List<KeyValue>();

            while (stream.Position < stream.Length)
            {
                var type = (KeyValueType)stream.ReadByte();
                if (type == KeyValueType.End)
                {
                    break;
                }

                var child = new KeyValue
                {
                    Name = stream.ReadNullTerminatedString(),
                };

                switch (type)
                {
                    case KeyValueType.None:
                    {
                        child.ReadAsBinary(stream);
                        break;
                    }

                    case KeyValueType.String:
                    {
                        child.Value = stream.ReadNullTerminatedString();
                        break;
                    }

                    case KeyValueType.Int32:
                    case KeyValueType.Color:
                    case KeyValueType.Pointer:
                    case KeyValueType.UInt32:
                    {
                        child.Value = stream.ReadValueInt32();
                        break;
                    }

                    case KeyValueType.Float32:
                    {
                        child.Value = stream.ReadValueFloat32();
                        break;
                    }

                    case KeyValueType.WideString:
                    {
                        child.Value = null;
                        break;
                    }

                    case KeyValueType.UInt64:
                    case KeyValueType.Int64:
                    {
                        child.Value = stream.ReadValueUInt64();
                        break;
                    }

                    case KeyValueType.Boolean:
                    {
                        child.Value = stream.ReadByte() != 0;
                        break;
                    }

                    case KeyValueType.End:
                    {
                        child.Value = null;
                        break;
                    }

                    default:
                    {
                        child.Value = null;
                        break;
                    }
                }

                this.Children.Add(child);
            }
        }
    }
}
