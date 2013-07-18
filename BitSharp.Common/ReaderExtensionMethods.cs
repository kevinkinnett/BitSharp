using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common.ExtensionMethods
{
    public static class ReaderExtensionMethods
    {
        public static bool ReadBool(this BinaryReader reader)
        {
            return reader.ReadByte() != 0;
        }

        public static UInt16 Read2Bytes(this BinaryReader reader)
        {
            return reader.ReadUInt16();
        }

        public static UInt16 Read2BytesBE(this BinaryReader reader)
        {
            using (var reverse = reader.ReverseRead(2))
                return reverse.Read2Bytes();
        }

        public static UInt32 Read4Bytes(this BinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        public static UInt64 Read8Bytes(this BinaryReader reader)
        {
            return reader.ReadUInt64();
        }

        public static UInt256 Read32Bytes(this BinaryReader reader)
        {
            return new UInt256(reader.ReadBytes(32));
        }

        private static BinaryReader ReverseRead(this BinaryReader reader, int length)
        {
            var bytes = reader.ReadBytes(length);
            Array.Reverse(bytes);
            return new BinaryReader(new MemoryStream(bytes));
        }
    }
}
