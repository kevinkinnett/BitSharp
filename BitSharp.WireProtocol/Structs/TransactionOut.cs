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
    public struct TransactionOut
    {
        public readonly UInt64 Value;
        public readonly ImmutableArray<byte> ScriptPublicKey;

        public TransactionOut(UInt64 Value, ImmutableArray<byte> ScriptPublicKey)
        {
            this.Value = Value;
            this.ScriptPublicKey = ScriptPublicKey;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Value, ScriptPublicKey);
        }

        public TransactionOut With(UInt64? Value = null, ImmutableArray<byte>? ScriptPublicKey = null)
        {
            return new TransactionOut
            (
                Value ?? this.Value,
                ScriptPublicKey ?? this.ScriptPublicKey
            );
        }

        public static TransactionOut FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static TransactionOut ReadRawBytes(WireReader reader)
        {
            return new TransactionOut
            (
                Value: reader.Read8Bytes(),
                ScriptPublicKey: reader.ReadVarBytes().ToImmutableArray()
            );
        }

        internal static byte[] ToRawBytes(UInt64 Value, ImmutableArray<byte> ScriptPublicKey)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Value, ScriptPublicKey);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt64 Value, ImmutableArray<byte> ScriptPublicKey)
        {
            writer.Write8Bytes(Value);
            writer.WriteVarBytes(ScriptPublicKey.ToArray());
        }
    }
}
