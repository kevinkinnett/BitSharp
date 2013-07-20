using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common.ExtensionMethods
{
    public static class WriterExtensionMethods
    {
        public static void WriteUInt32LE(this BinaryWriter writer, UInt32 value)
        {
            writer.Write(value);
        }

        public static void WriteBool(this BinaryWriter writer, bool value)
        {
            writer.Write((byte)(value ? 1 : 0));
        }

        public static void Write1Byte(this BinaryWriter writer, Byte value)
        {
            writer.Write(value);
        }

        public static void Write2Bytes(this BinaryWriter writer, UInt16 value)
        {
            writer.Write(value);
        }

        public static void Write2BytesBE(this BinaryWriter writer, UInt16 value)
        {
            writer.ReverseWrite(2, reverseWriter => reverseWriter.Write2Bytes(value));
        }

        public static void Write4Bytes(this BinaryWriter writer, UInt32 value)
        {
            writer.Write(value);
        }

        public static void WriteInt32(this BinaryWriter writer, Int32 value)
        {
            writer.Write(value);
        }

        public static void Write8Bytes(this BinaryWriter writer, UInt64 value)
        {
            writer.Write(value);
        }

        public static void Write32Bytes(this BinaryWriter writer, UInt256 value)
        {
            writer.Write(value.ToByteArray());
        }

        public static void WriteBytes(this BinaryWriter writer, byte[] value)
        {
            writer.Write(value);
        }

        public static void WriteBytes(this BinaryWriter writer, int length, byte[] value)
        {
            if (value.Length != length)
                throw new ArgumentException();

            writer.WriteBytes(value);
        }

        private static void ReverseWrite(this BinaryWriter writer, int length, Action<BinaryWriter> write)
        {
            var bytes = new byte[length];
            using (var reverseWriter = new BinaryWriter(new MemoryStream(bytes)))
            {
                write(reverseWriter);
                
                // verify that the correct amount of bytes were writtern
                if (reverseWriter.BaseStream.Position != length)
                    throw new InvalidOperationException();
            }
            Array.Reverse(bytes);
            
            writer.WriteBytes(bytes);
        }
    }
}
