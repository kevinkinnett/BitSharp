using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct TxKey
    {
        private readonly UInt256 _txHash;
        private readonly UInt256 _blockHash;
        private readonly UInt32 _txIndex;
        private readonly int hashCode;

        public TxKey(UInt256 txHash, UInt256 blockHash, UInt32 txIndex)
        {
            this._txHash = txHash;
            this._blockHash = blockHash;
            this._txIndex = txIndex;
            this.hashCode = txHash.GetHashCode() ^ blockHash.GetHashCode() ^ txIndex.GetHashCode();
        }

        public UInt256 TxHash { get { return this._txHash; } }

        public UInt256 BlockHash { get { return this._blockHash; } }

        public UInt32 TxIndex { get { return this._txIndex; } }

        public override bool Equals(object obj)
        {
            if (!(obj is TxKey))
                return false;

            var other = (TxKey)obj;
            return other == this;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static bool operator ==(TxKey left, TxKey right)
        {
            return left.TxHash == right.TxHash && left.BlockHash == right.BlockHash && left.TxIndex == right.TxIndex;
        }

        public static bool operator !=(TxKey left, TxKey right)
        {
            return !(left == right);
        }
    }
}
