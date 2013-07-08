using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.WireProtocol
{
    public struct TransactionIn
    {
        public readonly UInt256 PreviousTransactionHash;
        public readonly UInt32 PreviousTransactionIndex;
        public readonly ImmutableArray<byte> ScriptSignature;
        public readonly UInt32 Sequence;

        public TransactionIn(UInt256 PreviousTransactionHash, UInt32 PreviousTransactionIndex, ImmutableArray<byte> ScriptSignature, UInt32 Sequence)
        {
            this.PreviousTransactionHash = PreviousTransactionHash;
            this.PreviousTransactionIndex = PreviousTransactionIndex;
            this.ScriptSignature = ScriptSignature;
            this.Sequence = Sequence;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(PreviousTransactionHash, PreviousTransactionIndex, ScriptSignature, Sequence);
        }

        public TransactionIn With(UInt256? PreviousTransactionHash = null, UInt32? PreviousTransactionIndex = null, ImmutableArray<byte>? ScriptSignature = null, UInt32? Sequence = null)
        {
            return new TransactionIn
            (
                PreviousTransactionHash ?? this.PreviousTransactionHash,
                PreviousTransactionIndex ?? this.PreviousTransactionIndex,
                ScriptSignature ?? this.ScriptSignature,
                Sequence ?? this.Sequence
            );
        }

        public static TransactionIn FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static TransactionIn ReadRawBytes(WireReader reader)
        {
            return new TransactionIn
            (
                PreviousTransactionHash: reader.Read32Bytes(),
                PreviousTransactionIndex: reader.Read4Bytes(),
                ScriptSignature: reader.ReadVarBytes().ToImmutableArray(),
                Sequence: reader.Read4Bytes()
            );
        }

        internal static byte[] ToRawBytes(UInt256 PreviousTransactionHash, UInt32 PreviousTransactionIndex, ImmutableArray<byte> ScriptSignature, UInt32 Sequence)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, PreviousTransactionHash, PreviousTransactionIndex, ScriptSignature, Sequence);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt256 PreviousTransactionHash, UInt32 PreviousTransactionIndex, ImmutableArray<byte> ScriptSignature, UInt32 Sequence)
        {
            writer.Write32Bytes(PreviousTransactionHash);
            writer.Write4Bytes(PreviousTransactionIndex);
            writer.WriteVarBytes(ScriptSignature.ToArray());
            writer.Write4Bytes(Sequence);
        }
    }
}
