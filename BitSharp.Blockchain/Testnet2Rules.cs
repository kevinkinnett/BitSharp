using BitSharp.Blockchain.ExtensionMethods;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Script;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class Testnet2Rules : MainnetRules
    {
        private readonly Block _genesisBlock;
        private readonly ChainedBlock _genesisChainedBlock;
        private readonly Data.Blockchain _genesisBlockchain;

        public Testnet2Rules(CacheContext cacheContext)
            : base(cacheContext)
        {
            this._genesisBlock =
                new Block
                (
                    header: new BlockHeader
                    (
                        version: 1,
                        previousBlock: 0,
                        merkleRoot: UInt256.Parse("4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b", NumberStyles.HexNumber),
                        time: 1296688602,
                        bits: 0x207FFFFF,
                        nonce: 2
                    ),
                    transactions: ImmutableArray.Create
                    (
                        new Transaction
                        (
                            version: 1,
                            inputs: ImmutableArray.Create
                            (
                                new TxInput
                                (
                                    previousTxOutputKey: new TxOutputKey
                                    (
                                        txHash: 0,
                                        txOutputIndex: 0xFFFFFFFF
                                    ),
                                    scriptSignature: ImmutableArray.Create<byte>
                                    (
                                        0x04, 0xFF, 0xFF, 0x00, 0x1D, 0x01, 0x04, 0x45, 0x54, 0x68, 0x65, 0x20, 0x54, 0x69, 0x6D, 0x65,
                                        0x73, 0x20, 0x30, 0x33, 0x2F, 0x4A, 0x61, 0x6E, 0x2F, 0x32, 0x30, 0x30, 0x39, 0x20, 0x43, 0x68,
                                        0x61, 0x6E, 0x63, 0x65, 0x6C, 0x6C, 0x6F, 0x72, 0x20, 0x6F, 0x6E, 0x20, 0x62, 0x72, 0x69, 0x6E,
                                        0x6B, 0x20, 0x6F, 0x66, 0x20, 0x73, 0x65, 0x63, 0x6F, 0x6E, 0x64, 0x20, 0x62, 0x61, 0x69, 0x6C,
                                        0x6F, 0x75, 0x74, 0x20, 0x66, 0x6F, 0x72, 0x20, 0x62, 0x61, 0x6E, 0x6B, 0x73
                                    ),
                                    sequence: 0xFFFFFFFF
                                )
                            ),
                            outputs: ImmutableArray.Create
                            (
                                new TxOutput
                                (
                                    value: (UInt64)(50L * 100.MILLION()),
                                    scriptPublicKey: ImmutableArray.Create<byte>
                                    (
                                        0x41, 0x04, 0x67, 0x8A, 0xFD, 0xB0, 0xFE, 0x55, 0x48, 0x27, 0x19, 0x67, 0xF1, 0xA6, 0x71, 0x30,
                                        0xB7, 0x10, 0x5C, 0xD6, 0xA8, 0x28, 0xE0, 0x39, 0x09, 0xA6, 0x79, 0x62, 0xE0, 0xEA, 0x1F, 0x61,
                                        0xDE, 0xB6, 0x49, 0xF6, 0xBC, 0x3F, 0x4C, 0xEF, 0x38, 0xC4, 0xF3, 0x55, 0x04, 0xE5, 0x1E, 0xC1,
                                        0x12, 0xDE, 0x5C, 0x38, 0x4D, 0xF7, 0xBA, 0x0B, 0x8D, 0x57, 0x8A, 0x4C, 0x70, 0x2B, 0x6B, 0xF1,
                                        0x1D, 0x5F, 0xAC
                                    )
                                )
                            ),
                            lockTime: 0
                        )
                    )
                );

            Debug.Assert(_genesisBlock.Hash == UInt256.Parse("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", NumberStyles.HexNumber));

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

        public override Block GenesisBlock { get { return this._genesisBlock; } }

        public override ChainedBlock GenesisChainedBlock { get { return this._genesisChainedBlock; } }

        public override Data.Blockchain GenesisBlockchain { get { return this._genesisBlockchain; } }
    }
}
