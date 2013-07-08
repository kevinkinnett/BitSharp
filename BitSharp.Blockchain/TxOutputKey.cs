using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public struct TxOutputKey : IComparable<TxOutputKey>
    {
        public readonly UInt256 previousTransactionHash;
        public readonly int previousOutputIndex;
        private readonly int _hashCode;

        public TxOutputKey(UInt256 previousTransactionHash, int previousOutputIndex)
        {
            this.previousTransactionHash = previousTransactionHash;
            this.previousOutputIndex = previousOutputIndex;
            _hashCode = this.previousTransactionHash.GetHashCode() ^ previousOutputIndex.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TxOutputKey))
                return false;

            var other = (TxOutputKey)obj;
            return other.previousTransactionHash == this.previousTransactionHash && other.previousOutputIndex == this.previousOutputIndex;
        }

        public override int GetHashCode()
        {
            return this._hashCode;
        }

        public int CompareTo(TxOutputKey other)
        {
            var hashCompare = this.previousTransactionHash.CompareTo(other.previousTransactionHash);
            if (hashCompare != 0)
                return hashCompare;
            else
                return this.previousOutputIndex.CompareTo(other.previousOutputIndex);
        }
    }
}
