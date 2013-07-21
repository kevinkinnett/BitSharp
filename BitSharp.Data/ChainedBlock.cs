using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct ChainedBlock
    {
        private readonly UInt256 _blockHash;
        private readonly UInt256 _previousBlockHash;
        private readonly int _height;
        private readonly BigInteger _totalWork;

        private readonly bool notDefault;
        private readonly int hashCode;

        public ChainedBlock(UInt256 blockHash, UInt256 previousBlockHash, int height, BigInteger totalWork)
        {
            this._blockHash = blockHash;
            this._previousBlockHash = previousBlockHash;
            this._height = height;
            this._totalWork = totalWork;

            this.notDefault = true;
            this.hashCode = blockHash.GetHashCode() ^ previousBlockHash.GetHashCode() ^ height.GetHashCode() ^ totalWork.GetHashCode();
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public UInt256 BlockHash { get { return this._blockHash; } }

        public UInt256 PreviousBlockHash { get { return this._previousBlockHash; } }

        public int Height { get { return this._height; } }

        public BigInteger TotalWork { get { return this._totalWork; } }

        public override bool Equals(object obj)
        {
            if (!(obj is ChainedBlock))
                return false;

            var other = (ChainedBlock)obj;
            return other == this;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static bool operator ==(ChainedBlock left, ChainedBlock right)
        {
            return left.BlockHash == right.BlockHash && left.PreviousBlockHash == right._blockHash && left.Height == right.Height && left.TotalWork == right.TotalWork;
        }

        public static bool operator !=(ChainedBlock left, ChainedBlock right)
        {
            return !(left == right);
        }

        public static long SizeEstimator(ChainedBlock chainedBlock)
        {
            return 100;
        }
    }
}
