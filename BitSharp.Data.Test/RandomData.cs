using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    public struct RandomDataOptions
    {
        public int? MinimumBlockCount { get; set; }
        public int? BlockCount { get; set; }
        public int? TransactionCount { get; set; }
        public int? TxInputCount { get; set; }
        public int? TxOutputCount { get; set; }
        public int? ScriptSignatureSize { get; set; }
        public int? ScriptPublicKeySize { get; set; }
    }

    public static class RandomData
    {
        private static readonly Random random = new Random();

        public static Block RandomBlock(RandomDataOptions options = default(RandomDataOptions))
        {
            return new Block
            (
                header: RandomBlockHeader(),
                transactions: Enumerable.Range(0, random.Next(options.TransactionCount ?? 100)).Select(x => RandomTransaction()).ToImmutableArray()
            );
        }

        public static BlockHeader RandomBlockHeader(RandomDataOptions options = default(RandomDataOptions))
        {
            return new BlockHeader
            (
                version: random.NextUInt32(),
                previousBlock: random.NextUInt256(),
                merkleRoot: random.NextUInt256(),
                time: random.NextUInt32(),
                bits: random.NextUInt32(),
                nonce: random.NextUInt32()
            );
        }

        public static Transaction RandomTransaction(RandomDataOptions options = default(RandomDataOptions))
        {
            return new Transaction
            (
                version: random.NextUInt32(),
                inputs: Enumerable.Range(0, random.Next(options.TxInputCount ?? 100)).Select(x => RandomTxInput()).ToImmutableArray(),
                outputs: Enumerable.Range(0, random.Next(options.TxOutputCount ?? 100)).Select(x => RandomTxOutput()).ToImmutableArray(),
                lockTime: random.NextUInt32()
            );
        }

        public static TxInput RandomTxInput(RandomDataOptions options = default(RandomDataOptions))
        {
            return new TxInput
            (
                previousTxOutputKey: new TxOutputKey
                (
                    txHash: random.NextUInt32(),
                    txOutputIndex: random.NextUInt32()
                ),
                scriptSignature: random.NextBytes(random.Next(options.ScriptSignatureSize ?? 100)).ToImmutableArray(),
                sequence: random.NextUInt32()
            );
        }

        public static TxOutput RandomTxOutput(RandomDataOptions options = default(RandomDataOptions))
        {
            return new TxOutput
            (
                value: random.NextUInt64(),
                scriptPublicKey: random.NextBytes(random.Next(options.ScriptPublicKeySize ?? 100)).ToImmutableArray()
            );
        }

        public static Blockchain RandomBlockchain(RandomDataOptions options = default(RandomDataOptions))
        {
            //TODO blockCount algorithm isn't exact
            var blockCount = random.Next(options.BlockCount ?? 100) + (options.MinimumBlockCount ?? 1);
            var blockList = new List<Block>(blockCount);
            var chainedBlockList = new List<ChainedBlock>(blockCount);

            var previousBlockHash = UInt256.Zero;
            var totalWork = new BigInteger(0);
            for (var i = 0; i < blockCount; i++)
            {
                var block = RandomData.RandomBlock(options);
                block = block.With(Header: block.Header.With(PreviousBlock: previousBlockHash));
                blockList.Add(block);

                previousBlockHash = block.Hash;
                totalWork += block.Header.CalculateWork();

                chainedBlockList.Add(new ChainedBlock(block.Hash, block.Header.PreviousBlock, i, totalWork));
            }

            var blockListHashes = blockList.Select(x => x.Hash).ToImmutableHashSet();
            var utxo = blockList.SelectMany(block =>
                block.Transactions.Select((tx, txIndex) =>
                    new UnspentTx(block.Hash, (UInt32)txIndex, tx.Hash, random.NextImmutableBitArray(options.TxOutputCount ?? 100))))
                .ToImmutableDictionary(unspentTx => unspentTx.TxHash, unspentTx => unspentTx);

            return new Blockchain
            (
                chainedBlockList.ToImmutableList(),
                blockListHashes,
                utxo
            );
        }

        public static BlockchainKey RandomBlockchainKey()
        {
            return new BlockchainKey
            (
                guid: Guid.NewGuid(),
                rootBlockHash: random.NextUInt256()
            );
        }

        public static BlockchainMetadata RandomBlockchainMetadata()
        {
            return new BlockchainMetadata
            (
                guid: Guid.NewGuid(),
                rootBlockHash: random.NextUInt256(),
                totalWork: random.NextUBigIntegerBytes(64)
            );
        }

        public static ChainedBlock RandomChainedBlock()
        {
            return new ChainedBlock
            (
                blockHash: random.NextUInt256(),
                previousBlockHash: random.NextUInt256(),
                height: Math.Abs(random.Next()),
                totalWork: random.NextUBigIntegerBytes(64)
            );
        }

        public static TxKey RandomTxKey()
        {
            return new TxKey
            (
                txHash: random.NextUInt256(),
                blockHash: random.NextUInt256(),
                txIndex: random.NextUInt32()
            );
        }

        public static UnspentTx RandomUnspentTx(RandomDataOptions options = default(RandomDataOptions))
        {
            return new UnspentTx
            (
                txHash: random.NextUInt256(),
                blockHash: random.NextUInt256(),
                txIndex: random.NextUInt32(),
                unspentOutputs: random.NextImmutableBitArray(random.Next(options.TxOutputCount ?? 100))
            );
        }

        public static TxOutputKey RandomTxOutputKey()
        {
            return new TxOutputKey
            (
                txHash: random.NextUInt256(),
                txOutputIndex: random.NextUInt32()
            );
        }

        public static ImmutableBitArray NextImmutableBitArray(this Random random, int length)
        {
            var bitArray = new BitArray(length);
            for (var i = 0; i < length; i++)
                bitArray[i] = random.NextBool();

            return bitArray.ToImmutableBitArray();
        }
    }
}
