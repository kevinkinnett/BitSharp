using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.WireProtocol.ExtensionMethods
{
    public static class ReaderExtensionMethods
    {
        public static UInt64 ReadVarInt(this BinaryReader reader)
        {
            var value = reader.ReadByte();
            if (value < 0xFD)
                return value;
            else if (value == 0xFD)
                return reader.Read2Bytes();
            else if (value == 0xFE)
                return reader.Read4Bytes();
            else if (value == 0xFF)
                return reader.Read8Bytes();
            else
                throw new Exception();
        }

        public static string ReadVarString(this BinaryReader reader)
        {
            var rawBytes = reader.ReadVarBytes();
            return Encoding.ASCII.GetString(rawBytes);
        }

        public static byte[] ReadVarBytes(this BinaryReader reader)
        {
            var length = reader.ReadVarInt();
            return reader.ReadBytes(length.ToIntChecked());
        }

        public static string ReadFixedString(this BinaryReader reader, int length)
        {
            var encoded = reader.ReadBytes(length);
            // ignore trailing null bytes in a fixed length string
            var encodedTrimmed = encoded.TakeWhile(x => x != 0).ToArray();
            var decoded = Encoding.ASCII.GetString(encodedTrimmed);

            return decoded;
        }

        public static ImmutableArray<T> DecodeList<T>(this BinaryReader reader, Func<T> decode)
        {
            var length = reader.ReadVarInt().ToIntChecked();

            var list = new T[length];
            for (var i = 0; i < length; i++)
            {
                list[i] = decode();
            }

            return list.ToImmutableArray();
        }
    }
}
