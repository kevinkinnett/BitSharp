using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.WireProtocol
{
    public struct BlockHeader
    {
        public readonly UInt32 Version;
        public readonly UInt256 PreviousBlock;
        public readonly UInt256 MerkleRoot;
        public readonly UInt32 Time;
        public readonly UInt32 Bits;
        public readonly UInt32 Nonce;
        public readonly UInt256 Hash;

        public BlockHeader(UInt32 Version, UInt256 PreviousBlock, UInt256 MerkleRoot, UInt32 Time, UInt32 Bits, UInt32 Nonce, UInt256? Hash = null)
        {
            this.Version = Version;
            this.PreviousBlock = PreviousBlock;
            this.MerkleRoot = MerkleRoot;
            this.Time = Time;
            this.Bits = Bits;
            this.Nonce = Nonce;

            this.Hash = Hash ?? new UInt256(Crypto.DoubleSHA256(ToRawBytes(Version, PreviousBlock, MerkleRoot, Time, Bits, Nonce)));
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Version, PreviousBlock, MerkleRoot, Time, Bits, Nonce);
        }

        public static long SizeEstimator(BlockHeader blockHeader)
        {
            return 80;
        }

        public BlockHeader With(UInt32? Version = null, UInt256? PreviousBlock = null, UInt256? MerkleRoot = null, UInt32? Time = null, UInt32? Bits = null, UInt32? Nonce = null)
        {
            return new BlockHeader
            (
                Version ?? this.Version,
                PreviousBlock ?? this.PreviousBlock,
                MerkleRoot ?? this.MerkleRoot,
                Time ?? this.Time,
                Bits ?? this.Bits,
                Nonce ?? this.Nonce
            );
        }

        public static BlockHeader FromRawBytes(byte[] bytes, UInt256? Hash = null)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static BlockHeader ReadRawBytes(WireReader reader, UInt256? Hash = null)
        {
            return new BlockHeader
            (
                Version: reader.Read4Bytes(),
                PreviousBlock: reader.Read32Bytes(),
                MerkleRoot: reader.Read32Bytes(),
                Time: reader.Read4Bytes(),
                Bits: reader.Read4Bytes(),
                Nonce: reader.Read4Bytes(),
                Hash: Hash
            );
        }

        internal static byte[] ToRawBytes(UInt32 Version, UInt256 PreviousBlock, UInt256 MerkleRoot, UInt32 Time, UInt32 Bits, UInt32 Nonce)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Version, PreviousBlock, MerkleRoot, Time, Bits, Nonce);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt32 Version, UInt256 PreviousBlock, UInt256 MerkleRoot, UInt32 Time, UInt32 Bits, UInt32 Nonce)
        {
            writer.Write4Bytes(Version);
            writer.Write32Bytes(PreviousBlock);
            writer.Write32Bytes(MerkleRoot);
            writer.Write4Bytes(Time);
            writer.Write4Bytes(Bits);
            writer.Write4Bytes(Nonce);
        }
    }
}
