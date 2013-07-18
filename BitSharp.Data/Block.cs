using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.Data
{
    public struct Block
    {
        private readonly BlockHeader _header;
        private readonly ImmutableArray<Transaction> _transactions;
        private readonly long _sizeEstimate;
        private readonly bool notDefault;

        public Block(BlockHeader header, ImmutableArray<Transaction> transactions)
        {
            this._header = header;
            this._transactions = transactions;

            var sizeEstimate = BlockHeader.SizeEstimator(header);
            for (var i = 0; i < transactions.Length; i++)
            {
                for (var j = 0; j < transactions[i].Inputs.Length; j++)
                    sizeEstimate += transactions[i].Inputs[j].ScriptSignature.Length;

                for (var j = 0; j < transactions[i].Outputs.Length; j++)
                    sizeEstimate += transactions[i].Outputs[j].ScriptPublicKey.Length;
            }
            sizeEstimate = (long)(sizeEstimate * 1.5);

            this._sizeEstimate = sizeEstimate;

            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public UInt256 Hash { get { return this.Header.Hash; } }

        public BlockHeader Header { get { return this._header; } }

        public ImmutableArray<Transaction> Transactions { get { return this._transactions; } }

        public long SizeEstimate { get { return this._sizeEstimate; } }

        public Block With(BlockHeader? Header = null, ImmutableArray<Transaction>? Transactions = null)
        {
            return new Block
            (
                Header ?? this.Header,
                Transactions ?? this.Transactions
            );
        }

        //TODO for unit test, move elsewhere
        public Block WithAddedTransactions(params Transaction[] transactions)
        {
            return this.With(Transactions: this.Transactions.AddRange(transactions));
        }

        public static long SizeEstimator(Block block)
        {
            return block.SizeEstimate;
        }
    }

    //TODO move these elsewhere
    public static class BlockExtensions
    {
        public static UInt256 CalculateMerkleRoot(this ImmutableArray<Transaction> transactions)
        {
            ImmutableArray<ImmutableArray<byte>> merkleTree;
            return CalculateMerkleRoot(transactions, out merkleTree);
        }

        public static UInt256 CalculateMerkleRoot(this ImmutableArray<Transaction> transactions, out ImmutableArray<ImmutableArray<byte>> merkleTree)
        {
            var workingMerkleTree = new List<ImmutableArray<byte>>();

            var hashes = transactions.Select(tx => tx.Hash.ToByteArray().ToImmutableArray()).ToList();

            workingMerkleTree.AddRange(hashes);
            while (hashes.Count > 1)
            {
                workingMerkleTree.AddRange(hashes);

                // ensure row is even length
                if (hashes.Count % 2 != 0)
                    hashes.Add(hashes.Last());

                // pair up hashes in row ({1, 2, 3, 4} into {{1, 2}, {3, 4}}) and then hash the pairs
                // the result is the next row, which will be half the size of the current row
                hashes =
                    Enumerable.Range(0, hashes.Count / 2)
                    .Select(i => hashes[i * 2].AddRange(hashes[i * 2 + 1]))
                    //.AsParallel().AsOrdered().WithExecutionMode(ParallelExecutionMode.ForceParallelism).WithDegreeOfParallelism(10)
                    .Select(pair => Crypto.DoubleSHA256(pair.ToArray()).ToImmutableArray())
                    .ToList();
            }
            Debug.Assert(hashes.Count == 1);

            merkleTree = workingMerkleTree.ToImmutableArray();
            return new UInt256(hashes[0].ToArray());
        }
    }
}
