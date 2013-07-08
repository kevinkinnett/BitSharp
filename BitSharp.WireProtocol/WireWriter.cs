using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common;

namespace BitSharp.WireProtocol
{
    public class WireWriter
    {
        private readonly Stream stream;
        private readonly BinaryWriter writer;

        public WireWriter(Stream stream)
        {
            this.stream = stream;
            this.writer = new BinaryWriter(stream);
        }

        public long Position
        {
            get { return this.stream.Position; }
        }

        public void WriteBool(bool value)
        {
            WriteOne((byte)(value ? 1 : 0));
        }

        public void Write1Byte(Byte value)
        {
            this.writer.Write(value);
            //WriteOne(value);
        }

        public void Write2Bytes(UInt16 value)
        {
            this.writer.Write(value);
            //WriteExactly(2, Bits.GetBytes(value));
        }

        public void Write2BytesBE(UInt16 value)
        {
            WriteExactly(2, Bits.GetBytesBE(value));
        }

        public void Write4Bytes(UInt32 value)
        {
            this.writer.Write(value);
            //WriteExactly(4, Bits.GetBytes(value));
        }

        public void Write8Bytes(UInt64 value)
        {
            this.writer.Write(value);
            //WriteExactly(8, Bits.GetBytes(value));
        }

        public void Write32Bytes(UInt256 value)
        {
            this.writer.Write(value.ToByteArray());
            //WriteExactly(32, value.ToByteArray());
        }

        public void WriteVarInt(UInt64 value)
        {
            if (value < 0xFD)
            {
                Write1Byte((Byte)value);
            }
            else if (value <= 0xFFFF)
            {
                Write1Byte(0xFD);
                Write2Bytes((UInt16)value);
            }
            else if (value <= 0xFFFFFFFF)
            {
                Write1Byte(0xFE);
                Write4Bytes((UInt32)value);
            }
            else
            {
                Write1Byte(0xFF);
                Write8Bytes(value);
            }
        }

        public void WriteVarString(string value)
        {
            var encoded = Encoding.ASCII.GetBytes(value);
            WriteVarBytes(encoded);
        }

        public void WriteVarBytes(byte[] value)
        {
            WriteVarInt((UInt64)value.Length);
            WriteRawBytes(value.Length, value);
        }

        public void WriteFixedString(int length, string value)
        {
            if (value.Length < length)
                value = value.PadRight(length, '\0');
            if (value.Length != length)
                throw new ArgumentException();

            var encoded = Encoding.ASCII.GetBytes(value);
            WriteRawBytes(encoded.Length, encoded);
        }

        public void WriteRawBytes(int length, byte[] value)
        {
            WriteExactly(length, value);
        }

        //TODO
        public void WriteRawBytes(byte[] value)
        {
            this.writer.Write(value);
        }

        private void WriteOne(byte value)
        {
            stream.WriteByte(value);
        }

        private void WriteExactly(int length, byte[] bytes)
        {
            if (length != bytes.Length)
                throw new ArgumentException();

            this.writer.Write(bytes);
            //stream.Write(bytes, 0, bytes.Length);
        }
    }
}
