using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public struct Blockchain
    {
        //TODO use block hash instead of block metadata
        public readonly ImmutableList<BlockMetadata> BlockList;
        public readonly ImmutableDictionary<TxOutputKey, object> Utxo;

        private readonly bool notDefault;
        private readonly int _hashCode;

        public Blockchain(ImmutableList<BlockMetadata> BlockList, ImmutableDictionary<TxOutputKey, object> Utxo)
        {
            //if (BlockList.Count == 0)
            //    throw new ArgumentOutOfRangeException();
            //if (BlockList.Where((data, i) => data.Height == null || data.Height != i || data.TotalWork == null).Any())
            //    throw new ArgumentOutOfRangeException();

            this.BlockList = BlockList;
            this.Utxo = Utxo;

            this.notDefault = true;

            //TODO seems expensive
            //this._hashCode = (int)((this.Height ^ (BigInteger)this.LatestBlockHash ^ this.TotalWork ^ MerkleTree.GetHashCode() ^ (BigInteger)MerkleRoot ^ this.BlockHashes.GetHashCode()) % int.MaxValue);
            //TODO this hash code is not complete
            this._hashCode = BlockList[BlockList.Count - 1].GetHashCode();
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public int BlockCount { get { return this.BlockList.Count; } }

        public int Height { get { return this.BlockList.Count - 1; } }

        public BigInteger TotalWork { get { return this.RootBlock.TotalWork.Value; } }

        public BlockMetadata RootBlock { get { return this.BlockList[this.BlockList.Count - 1]; } }

        public UInt256 RootBlockHash { get { return this.RootBlock.BlockHash; } }

        public override bool Equals(object obj)
        {
            if (!(obj is Blockchain))
                return false;

            var other = (Blockchain)obj;
            //TODO
            Debugger.Break();
            return other.BlockList.SequenceEqual(this.BlockList) && other.Utxo.SequenceEqual(this.Utxo);
        }

        public override int GetHashCode()
        {
            return this._hashCode;
        }
    }
}
