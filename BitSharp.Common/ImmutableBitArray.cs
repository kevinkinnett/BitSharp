using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class ImmutableBitArray : IEnumerable<bool>
    {
        private readonly BitArray bitArray;

        public ImmutableBitArray(int length, bool defaultValue)
        {
            this.bitArray = new BitArray(length, defaultValue);
        }

        public ImmutableBitArray(BitArray bitArray)
        {
            this.bitArray = (BitArray)bitArray.Clone();
        }

        private ImmutableBitArray(BitArray bitArray, bool clone)
        {
            this.bitArray = clone ? (BitArray)bitArray.Clone() : bitArray;
        }

        public bool this[int index]
        {
            get { return this.bitArray[index]; }
        }

        public int Length
        {
            get { return this.bitArray.Length; }
        }

        public ImmutableBitArray Set(int index, bool value)
        {
            var bitArray = (BitArray)this.bitArray.Clone();
            bitArray[index] = value;
            return new ImmutableBitArray(bitArray, clone: false);
        }

        public IEnumerator<bool> GetEnumerator()
        {
            for (var i = 0; i < this.bitArray.Length; i++)
                yield return this.bitArray[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.bitArray.GetEnumerator();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ImmutableBitArray))
                return false;

            return (ImmutableBitArray)obj == this;
        }

        public override int GetHashCode()
        {
            return this.bitArray.GetHashCode();
        }

        public static bool operator ==(ImmutableBitArray left, ImmutableBitArray right)
        {
            return left.SequenceEqual(right);
        }

        public static bool operator !=(ImmutableBitArray left, ImmutableBitArray right)
        {
            return !(left == right);
        }
    }
}
