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
    public struct GetBlocksPayload
    {
        public readonly UInt32 Version;
        public readonly ImmutableArray<UInt256> BlockLocatorHashes;
        public readonly UInt256 HashStop;

        public GetBlocksPayload(UInt32 Version, ImmutableArray<UInt256> BlockLocatorHashes, UInt256 HashStop)
        {
            this.Version = Version;
            this.BlockLocatorHashes = BlockLocatorHashes;
            this.HashStop = HashStop;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Version, BlockLocatorHashes, HashStop);
        }

        public GetBlocksPayload With(UInt32? Version = null, ImmutableArray<UInt256>? BlockLocatorHashes = null, UInt256? HashStop = null)
        {
            return new GetBlocksPayload
            (
                Version ?? this.Version,
                BlockLocatorHashes ?? this.BlockLocatorHashes,
                HashStop ?? this.HashStop
            );
        }

        public static GetBlocksPayload FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static GetBlocksPayload ReadRawBytes(WireReader reader)
        {
            return new GetBlocksPayload
            (
                Version: reader.Read4Bytes(),
                BlockLocatorHashes: WireEncoder.ReadList(reader, r => reader.Read32Bytes()),
                HashStop: reader.Read32Bytes()
            );
        }

        internal static byte[] ToRawBytes(UInt32 Version, ImmutableArray<UInt256> BlockLocatorHashes, UInt256 HashStop)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Version, BlockLocatorHashes, HashStop);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt32 Version, ImmutableArray<UInt256> BlockLocatorHashes, UInt256 HashStop)
        {
            writer.Write4Bytes(Version);
            writer.WriteVarInt((UInt64)BlockLocatorHashes.Length);
            foreach (var hash in BlockLocatorHashes)
            {
                writer.Write32Bytes(hash);
            }
            writer.Write32Bytes(HashStop);
        }
    }
}
