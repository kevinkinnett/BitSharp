using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    public class UnitTestRules : MainnetRules
    {
        public static readonly UInt256 Target0 = UInt256.Parse("F000000000000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
        public static readonly UInt256 Target1 = UInt256.Parse("0F00000000000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
        public static readonly UInt256 Target2 = UInt256.Parse("00F0000000000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
        public static readonly UInt256 Target3 = UInt256.Parse("000F000000000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
        public static readonly UInt256 Target4 = UInt256.Parse("0000F00000000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);

        private UInt256 _highestTarget;
        private Block _genesisBlock;
        private ChainedBlock _genesisChainedBlock;
        private Data.Blockchain _genesisBlockchain;

        public UnitTestRules(CacheContext cacheContext)
            : base(cacheContext)
        {
            this._highestTarget = Target0;
        }

        public override UInt256 HighestTarget { get { return this._highestTarget; } }

        public override Block GenesisBlock { get { return this._genesisBlock; } }

        public override ChainedBlock GenesisChainedBlock { get { return this._genesisChainedBlock; } }

        public override Data.Blockchain GenesisBlockchain { get { return this._genesisBlockchain; } }

        public void SetGenesisBlock(Block genesisBlock)
        {
            this._genesisBlock = genesisBlock;

            this._genesisChainedBlock =
                new ChainedBlock
                (
                    blockHash: this._genesisBlock.Hash,
                    previousBlockHash: this._genesisBlock.Header.PreviousBlock,
                    height: 0,
                    totalWork: this._genesisBlock.Header.CalculateWork()
                );

            this._genesisBlockchain =
                new Data.Blockchain
                (
                    blockList: ImmutableList.Create(this._genesisChainedBlock),
                    blockListHashes: ImmutableHashSet.Create(this._genesisBlock.Hash),
                    utxo: ImmutableHashSet.Create<TxOutputKey>() // genesis block coinbase is not included in utxo, it is unspendable
                );
        }

        public void SetHighestTarget(UInt256 highestTarget)
        {
            this._highestTarget = highestTarget;
        }
    }
}
