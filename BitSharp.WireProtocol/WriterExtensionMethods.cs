using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.WireProtocol.ExtensionMethods
{
    public static class WriterExtensionMethods
    {
        public static void WriteVarInt(this BinaryWriter writer, UInt64 value)
        {
            if (value < 0xFD)
            {
                writer.Write1Byte((Byte)value);
            }
            else if (value <= 0xFFFF)
            {
                writer.Write1Byte(0xFD);
                writer.Write2Bytes((UInt16)value);
            }
            else if (value <= 0xFFFFFFFF)
            {
                writer.Write1Byte(0xFE);
                writer.Write4Bytes((UInt32)value);
            }
            else
            {
                writer.Write1Byte(0xFF);
                writer.Write8Bytes(value);
            }
        }

        public static void WriteVarString(this BinaryWriter writer, string value)
        {
            var encoded = Encoding.ASCII.GetBytes(value);
            writer.WriteVarBytes(encoded);
        }

        public static void WriteVarBytes(this BinaryWriter writer, byte[] value)
        {
            writer.WriteVarInt((UInt64)value.Length);
            writer.WriteBytes(value.Length, value);
        }

        public static void WriteFixedString(this BinaryWriter writer, int length, string value)
        {
            if (value.Length < length)
                value = value.PadRight(length, '\0');
            if (value.Length != length)
                throw new ArgumentException();

            var encoded = Encoding.ASCII.GetBytes(value);
            writer.WriteBytes(encoded.Length, encoded);
        }

        public static void EncodeList<T>(this BinaryWriter writer, ImmutableArray<T> list, Action<T> encode)
        {
            writer.WriteVarInt((UInt64)list.Length);
            for (var i = 0; i < list.Length; i++)
            {
                encode(list[i]);
            }
        }
    }
}
