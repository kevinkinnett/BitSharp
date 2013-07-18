using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct BlockMetadata
    {
        private readonly UInt256 _blockHash;
        private readonly UInt256 _previousBlockHash;
        private readonly BigInteger _work;
        private readonly long? _height;
        private readonly BigInteger? _totalWork;
        private readonly bool? _isValid;
        private readonly bool notDefault;

        public BlockMetadata(UInt256 blockHash, UInt256 previousBlockHash, BigInteger work, long? height, BigInteger? totalWork, bool? isValid)
        {
            this._blockHash = blockHash;
            this._previousBlockHash = previousBlockHash;
            this._work = work;
            this._height = height;
            this._totalWork = totalWork;
            this._isValid = isValid;
            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public UInt256 BlockHash { get { return this._blockHash; } }

        public UInt256 PreviousBlockHash { get { return this._previousBlockHash; } }

        public BigInteger Work { get { return this._work; } }

        public long? Height { get { return this._height; } }

        public BigInteger? TotalWork { get { return this._totalWork; } }

        public bool? IsValid { get { return this._isValid; } }

        public static long SizeEstimator(BlockMetadata blockMetadata)
        {
            return 210;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BlockMetadata))
                return false;

            var other = (BlockMetadata)obj;
            return other == this;
        }

        public override int GetHashCode()
        {
            return this.BlockHash.GetHashCode();
        }

        public static bool operator ==(BlockMetadata left, BlockMetadata right)
        {
            return left.BlockHash == right.BlockHash && left.PreviousBlockHash == right.PreviousBlockHash && left.Height == right.Height && left.Work == right.Work && left.TotalWork == right.TotalWork && left.IsValid == right.IsValid;
        }

        public static bool operator !=(BlockMetadata left, BlockMetadata right)
        {
            return !(left == right);
        }
    }
}
