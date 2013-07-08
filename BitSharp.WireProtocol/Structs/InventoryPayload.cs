using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.WireProtocol
{
    public struct InventoryPayload
    {
        public readonly ImmutableArray<InventoryVector> InventoryVectors;

        public InventoryPayload(ImmutableArray<InventoryVector> InventoryVectors)
        {
            this.InventoryVectors = InventoryVectors;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(InventoryVectors);
        }

        public InventoryPayload With(ImmutableArray<InventoryVector>? InventoryVectors = null)
        {
            return new InventoryPayload
            (
                InventoryVectors ?? this.InventoryVectors
            );
        }

        public static InventoryPayload FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static InventoryPayload ReadRawBytes(WireReader reader)
        {
            return new InventoryPayload
            (
                InventoryVectors: WireEncoder.ReadList(reader, InventoryVector.ReadRawBytes)
            );
        }

        internal static byte[] ToRawBytes(ImmutableArray<InventoryVector> InventoryVectors)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, InventoryVectors);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, ImmutableArray<InventoryVector> InventoryVectors)
        {
            writer.WriteVarInt((UInt64)InventoryVectors.Length);
            foreach (var invVector in InventoryVectors)
            {
                writer.WriteRawBytes(invVector.ToRawBytes());
            }
        }
    }
}
