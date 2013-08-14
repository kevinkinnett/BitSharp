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
        private readonly UInt256 _blockHash;
        private readonly UInt32 _txIndex;
        private readonly UInt256 _txHash;

        private readonly bool notDefault;
        private readonly int hashCode;

        public TxKey(UInt256 blockHash, UInt32 txIndex, UInt256 txHash)
        {
            this._blockHash = blockHash;
            this._txIndex = txIndex;
            this._txHash = txHash;

            this.notDefault = true;
            this.hashCode = blockHash.GetHashCode() ^ txIndex.GetHashCode() ^ txHash.GetHashCode();
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public UInt256 BlockHash { get { return this._blockHash; } }

        public UInt32 TxIndex { get { return this._txIndex; } }

        public UInt256 TxHash { get { return this._txHash; } }

        public override bool Equals(object obj)
        {
            if (!(obj is TxKey))
                return false;

            return (TxKey)obj == this;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static bool operator ==(TxKey left, TxKey right)
        {
            return left.BlockHash == right.BlockHash && left.TxIndex == right.TxIndex && left.TxHash == right.TxHash;
        }

        public static bool operator !=(TxKey left, TxKey right)
        {
            return !(left == right);
        }
    }
}
