using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common;

namespace BitSharp.WireProtocol
{
    public class WireReader
    {
        private readonly Stream stream;

        public WireReader(Stream stream)
        {
            this.stream = stream;
        }

        internal byte[] GetRange(long fromPosition, long toPosition)
        {
            //TODO
            return (this.stream as ByteArrayStream).GetRange(fromPosition, toPosition);
        }

        public long Position
        {
            get { return this.stream.Position; }
        }

        public bool ReadBool()
        {
            return (ReadOne(stream)) != 0;
        }

        public Byte Read1Byte()
        {
            return ReadOne(stream);
        }

        public UInt16 Read2Bytes()
        {
            return Bits.ToUInt16(ReadExactly(2));
        }

        public UInt16 Read2BytesBE()
        {
            return Bits.ToUInt16BE(ReadExactly(2));
        }

        public UInt32 Read4Bytes()
        {
            return Bits.ToUInt32(ReadExactly(4));
        }

        public UInt64 Read8Bytes()
        {
            return Bits.ToUInt64(ReadExactly(8));
        }

        public UInt256 Read32Bytes()
        {
            return Bits.ToUInt256(ReadExactly(32));
        }

        public UInt64 ReadVarInt()
        {
            var value = ReadOne(stream);
            if (value < 0xFD)
                return value;
            else if (value == 0xFD)
                return Read2Bytes();
            else if (value == 0xFE)
                return Read4Bytes();
            else if (value == 0xFF)
                return Read8Bytes();

            Debug.Assert(false);
            return UInt64.MaxValue;
        }

        public string ReadVarString()
        {
            var rawBytes = ReadVarBytes();
            return Encoding.ASCII.GetString(rawBytes);
        }

        public byte[] ReadVarBytes()
        {
            var length = ReadVarInt();
            return ReadExactly(length.ToIntChecked());
        }

        public string ReadFixedString(int length)
        {
            var encoded = ReadExactly(length);
            // ignore trailing null bytes in a fixed length string
            var encodedTrimmed = encoded.TakeWhile(x => x != 0).ToArray();
            var decoded = Encoding.ASCII.GetString(encodedTrimmed);

            return decoded;
        }

        public byte[] ReadRawBytes(int length)
        {
            return ReadExactly(length);
        }

        private byte ReadOne(Stream stream)
        {
            return ReadExactly(1)[0];
        }

        private byte[] ReadExactly(int length)
        {
            if (length == 0)
                return new byte[0];

            var buffer = new byte[length];

            var offset = 0;
            while (offset < length)
            {
                var read = stream.Read(buffer, offset, length - offset);
                
                if (read < 0)
                    break;

                offset += read;
            }

            if (offset != length)
                throw new Exception(string.Format("Expected to read {0} bytes but could only read {1}", length, offset));

            return buffer;
        }
    }
}
