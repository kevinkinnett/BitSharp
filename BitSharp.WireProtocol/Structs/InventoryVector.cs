using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.WireProtocol
{
    public struct InventoryVector
    {
        public static readonly UInt32 TYPE_ERROR = 0;
        public static readonly UInt32 TYPE_MESSAGE_TRANSACTION = 1;
        public static readonly UInt32 TYPE_MESSAGE_BLOCK = 2;

        public readonly UInt32 Type;
        public readonly UInt256 Hash;

        public InventoryVector(UInt32 Type, UInt256 Hash)
        {
            this.Type = Type;
            this.Hash = Hash;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Type, Hash);
        }

        public InventoryVector With(UInt32? Type = null, UInt256? Hash = null)
        {
            return new InventoryVector
            (
                Type ?? this.Type,
                Hash ?? this.Hash
            );
        }

        public static InventoryVector FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static InventoryVector ReadRawBytes(WireReader reader)
        {
            return new InventoryVector
            (
                Type: reader.Read4Bytes(),
                Hash: reader.Read32Bytes()
            );
        }

        internal static byte[] ToRawBytes(UInt32 Type, UInt256 Hash)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Type, Hash);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt32 Type, UInt256 Hash)
        {
            writer.Write4Bytes(Type);
            writer.Write32Bytes(Hash);
        }
    }
}
