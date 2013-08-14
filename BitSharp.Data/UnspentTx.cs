using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct UnspentTx
    {
        private readonly UInt256 _blockHash;
        private readonly UInt32 _txIndex;
        private readonly UInt256 _txHash;
        private readonly ImmutableBitArray _unspentOutputs;

        private readonly bool notDefault;
        private readonly int hashCode;

        public UnspentTx(UInt256 blockHash, UInt32 txIndex, UInt256 txHash, ImmutableBitArray unspentOutputs)
        {
            this._blockHash = blockHash;
            this._txIndex = txIndex;
            this._txHash = txHash;
            this._unspentOutputs = unspentOutputs;

            this.notDefault = true;
            this.hashCode = blockHash.GetHashCode() ^ txIndex.GetHashCode() ^ txHash.GetHashCode() ^ unspentOutputs.GetHashCode();
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public UInt256 BlockHash { get { return this._blockHash; } }

        public UInt32 TxIndex { get { return this._txIndex; } }

        public UInt256 TxHash { get { return this._txHash; } }

        public ImmutableBitArray UnspentOutputs { get { return this._unspentOutputs; } }

        public TxKey ToTxKey()
        {
            return new TxKey(this.BlockHash, this.TxIndex, this.TxHash);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UnspentTx))
                return false;

            return (UnspentTx)obj == this;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static bool operator ==(UnspentTx left, UnspentTx right)
        {
            return left.BlockHash == right.BlockHash && left.TxIndex == right.TxIndex && left.TxHash == right.TxHash && left.UnspentOutputs == right.UnspentOutputs;
        }

        public static bool operator !=(UnspentTx left, UnspentTx right)
        {
            return !(left == right);
        }
    }
}
