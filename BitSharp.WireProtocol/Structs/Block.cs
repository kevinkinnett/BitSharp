using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.WireProtocol
{
    public struct Block
    {
        public readonly BlockHeader Header;
        public readonly ImmutableArray<Transaction> Transactions;
        public readonly long SizeEstimate;
        private readonly bool notDefault;

        public Block(BlockHeader Header, ImmutableArray<Transaction> Transactions)
        {
            this.Header = Header;
            this.Transactions = Transactions;

            var sizeEstimate = BlockHeader.SizeEstimator(Header);
            for (var i = 0; i < Transactions.Length; i++)
            {
                for (var j = 0; j < Transactions[i].Inputs.Length; j++)
                    sizeEstimate += Transactions[i].Inputs[j].ScriptSignature.Length;

                for (var j = 0; j < Transactions[i].Outputs.Length; j++)
                    sizeEstimate += Transactions[i].Outputs[j].ScriptPublicKey.Length;
            }
            sizeEstimate = (long)(sizeEstimate * 1.5);

            this.SizeEstimate = sizeEstimate;

            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public UInt256 Hash { get { return this.Header.Hash; } }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Header, Transactions, SizeEstimate);
        }

        public static long SizeEstimator(Block block)
        {
            return block.SizeEstimate;
        }

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

        public static Block FromRawBytes(byte[] bytes, UInt256? Hash = null)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()), Hash);
        }

        internal static Block ReadRawBytes(WireReader reader, UInt256? Hash = null)
        {
            return new Block
            (
                Header: BlockHeader.ReadRawBytes(reader, Hash),
                Transactions: WireEncoder.ReadList(reader, Transaction.ReadRawBytes)
            );
        }

        internal static byte[] ToRawBytes(BlockHeader Header, ImmutableArray<Transaction> Transactions, long SizeEstimate = 0)
        {
            var stream = new MemoryStream((int)SizeEstimate);
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Header, Transactions);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, BlockHeader Header, ImmutableArray<Transaction> Transactions)
        {
            writer.WriteRawBytes(Header.ToRawBytes());
            writer.WriteVarInt((UInt64)Transactions.Length);
            foreach (var tx in Transactions)
            {
                writer.WriteRawBytes(tx.ToRawBytes());
            }
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
