using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public struct TxOutputKey
    {
        private readonly UInt256 _previousTransactionHash;
        private readonly int _previousOutputIndex;
        private readonly int hashCode;

        public TxOutputKey(UInt256 previousTransactionHash, int previousOutputIndex)
        {
            this._previousTransactionHash = previousTransactionHash;
            this._previousOutputIndex = previousOutputIndex;
            this.hashCode = previousTransactionHash.GetHashCode() ^ previousOutputIndex.GetHashCode();
        }

        public UInt256 PreviousTransactionHash { get { return this._previousTransactionHash; } }

        public int PreviousOutputIndex { get { return this._previousOutputIndex; } }

        public override bool Equals(object obj)
        {
            if (!(obj is TxOutputKey))
                return false;

            var other = (TxOutputKey)obj;
            return other == this;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static bool operator ==(TxOutputKey left, TxOutputKey right)
        {
            return left.PreviousTransactionHash == right.PreviousTransactionHash && left.PreviousOutputIndex == right.PreviousOutputIndex;
        }

        public static bool operator !=(TxOutputKey left, TxOutputKey right)
        {
            return !(left == right);
        }
    }
}
