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
        private readonly ImmutableDictionary<UInt256, UnspentTx> _utxo;

        private readonly bool notDefault;

        public Blockchain(ImmutableList<ChainedBlock> blockList, ImmutableHashSet<UInt256> blockListHashes, ImmutableDictionary<UInt256, UnspentTx> utxo)
        {
            //Debug.Assert(!blockList.Where((x, i) => x.Height != i).Any());

            this._blockList = blockList;
            this._blockListHashes = blockListHashes;
            this._utxo = utxo;

            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public ImmutableList<ChainedBlock> BlockList { get { return this._blockList; } }

        public ImmutableHashSet<UInt256> BlockListHashes { get { return this._blockListHashes; } }

        public ImmutableDictionary<UInt256, UnspentTx> Utxo { get { return this._utxo; } }

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
            return left.BlockList.SequenceEqual(right.BlockList) && left.BlockListHashes.SetEquals(right.BlockListHashes) && left.Utxo.SequenceEqual(right.Utxo, new UtxoComparer());
        }

        public static bool operator !=(Blockchain left, Blockchain right)
        {
            return !(left == right);
        }

        private class UtxoComparer : IEqualityComparer<KeyValuePair<UInt256, UnspentTx>>
        {
            public bool Equals(KeyValuePair<UInt256, UnspentTx> x, KeyValuePair<UInt256, UnspentTx> y)
            {
                return x.Key == y.Key && x.Value == y.Value;
            }

            public int GetHashCode(KeyValuePair<UInt256, UnspentTx> obj)
            {
                return obj.Key.GetHashCode() ^ obj.Value.GetHashCode();
            }
        }
    }
}
