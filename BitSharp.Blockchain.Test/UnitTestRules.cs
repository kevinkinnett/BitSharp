using BitSharp.Common;
using BitSharp.WireProtocol;
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
        private readonly UInt256 _highestTarget;
        private readonly UInt32 _highestTargetBits;
        private Block _genesisBlock;
        private BlockMetadata _genesisBlockMetadata;
        private Blockchain _genesisBlockchain;

        public UnitTestRules()
        {
            this._highestTarget = UInt256.Parse("00F0000000000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            this._highestTargetBits = TargetToBits(this._highestTarget);

            //TODO
            MainnetRules.BypassValidation = true;
        }

        public void SetGenesisBlock(Block genesisBlock)
        {
            this._genesisBlock = genesisBlock;

            this._genesisBlockMetadata =
                new BlockMetadata
                (
                    blockHash: this._genesisBlock.Hash,
                    previousBlockHash: this._genesisBlock.Header.PreviousBlock,
                    work: CalculateWork(this._genesisBlock.Header),
                    height: 0,
                    totalWork: CalculateWork(this._genesisBlock.Header),
                    isValid: true
                );

            this._genesisBlockchain =
                new Blockchain
                (
                    blockList: ImmutableList.Create(this._genesisBlockMetadata),
                    utxo: ImmutableHashSet.Create<TxOutputKey>() // genesis block coinbase is not included in utxo, it is unspendable
                );
        }

        public override UInt256 HighestTarget { get { return this._highestTarget; } }

        public override uint HighestTargetBits { get { return this._highestTargetBits; } }

        public override Block GenesisBlock { get { return this._genesisBlock; } }

        public override BlockMetadata GenesisBlockMetadata { get { return this._genesisBlockMetadata; } }

        public override Blockchain GenesisBlockchain { get { return this._genesisBlockchain; } }
    }
}
