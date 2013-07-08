using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace BitSharp.WireProtocol
{
    public struct Transaction
    {
        public readonly UInt32 Version;
        public readonly ImmutableArray<TransactionIn> Inputs;
        public readonly ImmutableArray<TransactionOut> Outputs;
        public readonly UInt32 LockTime;
        public readonly UInt256 Hash;
        private readonly bool notDefault;

        public Transaction(UInt32 Version, ImmutableArray<TransactionIn> Inputs, ImmutableArray<TransactionOut> Outputs, UInt32 LockTime, UInt256? Hash = null)
        {
            this.Version = Version;
            this.Inputs = Inputs;
            this.Outputs = Outputs;
            this.LockTime = LockTime;

            this.Hash = Hash ?? new UInt256(Crypto.DoubleSHA256(ToRawBytes(Version, Inputs, Outputs, LockTime)));

            this.notDefault = true;
        }

        public bool IsDefault { get { return !this.notDefault; } }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Version, Inputs, Outputs, LockTime);
        }

        public Transaction With(UInt32? Version = null, ImmutableArray<TransactionIn>? Inputs = null, ImmutableArray<TransactionOut>? Outputs = null, UInt32? LockTime = null)
        {
            return new Transaction
            (
                Version ?? this.Version,
                Inputs ?? this.Inputs,
                Outputs ?? this.Outputs,
                LockTime ?? this.LockTime
            );
        }

        public static Transaction FromRawBytes(byte[] bytes, UInt256? Hash = null)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static Transaction ReadRawBytes(WireReader reader)
        {
            return ReadRawBytes(reader, Hash: null);
        }

        internal static Transaction ReadRawBytes(WireReader reader, UInt256? Hash = null)
        {
            return new Transaction
            (
                Version: reader.Read4Bytes(),
                Inputs: WireEncoder.ReadList(reader, TransactionIn.ReadRawBytes),
                Outputs: WireEncoder.ReadList(reader, TransactionOut.ReadRawBytes),
                LockTime: reader.Read4Bytes(),
                Hash: Hash
            );
        }

        internal static byte[] ToRawBytes(UInt32 Version, ImmutableArray<TransactionIn> Inputs, ImmutableArray<TransactionOut> Outputs, UInt32 LockTime)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Version, Inputs, Outputs, LockTime);

            return stream.ToArray();
        }

        public static void WriteRawBytes(WireWriter writer, UInt32 Version, ImmutableArray<TransactionIn> Inputs, ImmutableArray<TransactionOut> Outputs, UInt32 LockTime)
        {
            writer.Write4Bytes(Version);
            writer.WriteVarInt((UInt64)Inputs.Length);
            foreach (var input in Inputs)
            {
                writer.WriteRawBytes(input.ToRawBytes());
            }
            writer.WriteVarInt((UInt64)Outputs.Length);
            foreach (var output in Outputs)
            {
                writer.WriteRawBytes(output.ToRawBytes());
            }
            writer.Write4Bytes(LockTime);
        }
    }
}
