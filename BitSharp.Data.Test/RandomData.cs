using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    public struct RandomDataOptions
    {
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
    }
}
