using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public struct Blockchain
    {
        //TODO use block hash instead of block metadata
        private readonly ImmutableList<ChainedBlock> _blockList;
        private readonly ImmutableHashSet<UInt256> _blockListHashes;
        private readonly ImmutableHashSet<TxOutputKey> _utxo;

        private readonly bool notDefault;

        public Blockchain(ImmutableList<ChainedBlock> blockList, ImmutableHashSet<UInt256> blockListHashes, ImmutableHashSet<TxOutputKey> utxo)
        {
            Debug.Assert(blockList.Last().Height == blockList.Count - 1);

            this._blockList = blockList;
            this._blockListHashes = blockListHashes;
            this._utxo = utxo;

            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public ImmutableList<ChainedBlock> BlockList { get { return this._blockList; } }

        public ImmutableHashSet<UInt256> BlockListHashes { get { return this._blockListHashes; } }

        public ImmutableHashSet<TxOutputKey> Utxo { get { return this._utxo; } }

        public int BlockCount { get { return this.BlockList.Count; } }

        public int Height { get { return this.BlockList.Count - 1; } }

        public BigInteger TotalWork { get { return this.RootBlock.TotalWork; } }

        public ChainedBlock RootBlock { get { return this.BlockList[this.BlockList.Count - 1]; } }

        public UInt256 RootBlockHash { get { return this.RootBlock.BlockHash; } }

        public override bool Equals(object obj)
        {
            if (!(obj is Blockchain))
                return false;

            return (Blockchain)obj == this;
        }

        public static bool operator ==(Blockchain left, Blockchain right)
        {
            return left.BlockList.SequenceEqual(right.BlockList) && left.BlockListHashes.SetEquals(right.BlockListHashes) && left.Utxo.SetEquals(right.Utxo);
        }

        public static bool operator !=(Blockchain left, Blockchain right)
        {
            return !(left == right);
        }
    }
}
