using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public struct BlockMetadata : IComparable<BlockMetadata>
    {
        public readonly UInt256 BlockHash;
        public readonly UInt256 PreviousBlockHash;
        public readonly BigInteger Work;
        public readonly long? Height;
        public readonly BigInteger? TotalWork;
        public readonly bool? IsValid;
        private readonly bool notDefault;

        public BlockMetadata(UInt256 BlockHash, UInt256 PreviousBlockHash, BigInteger Work, long? Height, BigInteger? TotalWork, bool? IsValid)
        {
            this.BlockHash = BlockHash;
            this.PreviousBlockHash = PreviousBlockHash;
            this.Work = Work;
            this.Height = Height;
            this.TotalWork = TotalWork;
            this.IsValid = IsValid;
            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public static long SizeEstimator(BlockMetadata blockMetadata)   
        {
            return 210;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BlockMetadata))
                return false;

            var other = (BlockMetadata)obj;
            return other.BlockHash == this.BlockHash && other.PreviousBlockHash == this.PreviousBlockHash && other.Height == this.Height && other.Work == this.Work && other.TotalWork == this.TotalWork && other.IsValid == this.IsValid;
        }

        public override int GetHashCode()
        {
            return this.BlockHash.GetHashCode();
        }

        public int CompareTo(BlockMetadata other)
        {
            return this.BlockHash.CompareTo(other.BlockHash);
        }
    }
}
