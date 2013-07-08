using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class ByteArrayStream : Stream
    {
        private readonly byte[] data;
        private long position;

        public ByteArrayStream(byte[] data)
        {
            this.data = (byte[])data.Clone();
            this.position = 0;
        }

        public byte[] GetRange(long fromPosition, long toPosition)
        {
            var length = toPosition - fromPosition;
            if (length < 0)
                throw new ArgumentOutOfRangeException();

            var result = new byte[length];
            Buffer.BlockCopy(this.data, (int)fromPosition, result, 0, (int)length);
            
            return result;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length
        {
            get { return this.data.LongLength; }
        }

        public override long Position
        {
            get
            {
                return this.position;
            }
            set
            {
                this.position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int position;
            checked { position = (int)this.position; }
            
            // perform the seek before reading to make sure the offset is valid
            Seek(count, SeekOrigin.Current);

            // perform the read
            Buffer.BlockCopy(this.data, position, buffer, offset, count);
            
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = this.position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;

                case SeekOrigin.Current:
                    position += offset;
                    break;

                case SeekOrigin.End:
                    position = this.data.LongLength - 1 - offset;
                    break;
            }

            if (position < 0 || position > this.data.LongLength)
                throw new IOException();

            this.position = position;
            return position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
