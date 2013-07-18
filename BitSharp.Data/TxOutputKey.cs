using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct TxOutputKey
    {
        private readonly UInt256 _txHash;
        private readonly UInt32 _txOutputIndex;
        private readonly int hashCode;

        public TxOutputKey(UInt256 txHash, UInt32 txOutputIndex)
        {
            this._txHash = txHash;
            this._txOutputIndex = txOutputIndex;
            this.hashCode = txHash.GetHashCode() ^ txOutputIndex.GetHashCode();
        }

        public UInt256 TxHash { get { return this._txHash; } }

        public UInt32 TxOutputIndex { get { return this._txOutputIndex; } }

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
            return left.TxHash == right.TxHash && left.TxOutputIndex == right.TxOutputIndex;
        }

        public static bool operator !=(TxOutputKey left, TxOutputKey right)
        {
            return !(left == right);
        }
    }
}
