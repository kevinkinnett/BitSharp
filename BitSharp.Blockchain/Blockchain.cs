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
        private readonly ImmutableList<BlockMetadata> _blockList;
        private readonly ImmutableHashSet<TxOutputKey> _utxo;
        private readonly bool notDefault;

        public Blockchain(ImmutableList<BlockMetadata> blockList, ImmutableHashSet<TxOutputKey> utxo)
        {
            this._blockList = blockList;
            this._utxo = utxo;

            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public ImmutableList<BlockMetadata> BlockList { get { return this._blockList; } }

        public ImmutableHashSet<TxOutputKey> Utxo { get { return this._utxo; } }

        public int BlockCount { get { return this.BlockList.Count; } }

        public int Height { get { return this.BlockList.Count - 1; } }

        public BigInteger TotalWork { get { return this.RootBlock.TotalWork.Value; } }

        public BlockMetadata RootBlock { get { return this.BlockList[this.BlockList.Count - 1]; } }

        public UInt256 RootBlockHash { get { return this.RootBlock.BlockHash; } }
    }
}
