using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.Data
{
    public struct BlockHeader
    {
        private readonly UInt32 _version;
        private readonly UInt256 _previousBlock;
        private readonly UInt256 _merkleRoot;
        private readonly UInt32 _time;
        private readonly UInt32 _bits;
        private readonly UInt32 _nonce;
        private readonly UInt256 _hash;

        public BlockHeader(UInt32 version, UInt256 previousBlock, UInt256 merkleRoot, UInt32 time, UInt32 bits, UInt32 nonce, UInt256? hash = null)
        {
            this._version = version;
            this._previousBlock = previousBlock;
            this._merkleRoot = merkleRoot;
            this._time = time;
            this._bits = bits;
            this._nonce = nonce;

            this._hash = hash ?? CalculateHash(version, previousBlock, merkleRoot, time, bits, nonce);
        }

        public UInt32 Version { get { return this._version; } }

        public UInt256 PreviousBlock { get { return this._previousBlock; } }

        public UInt256 MerkleRoot { get { return this._merkleRoot; } }

        public UInt32 Time { get { return this._time; } }

        public UInt32 Bits { get { return this._bits; } }

        public UInt32 Nonce { get { return this._nonce; } }
        
        public UInt256 Hash { get { return this._hash; } }

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

        private static UInt256 CalculateHash(UInt32 Version, UInt256 PreviousBlock, UInt256 MerkleRoot, UInt32 Time, UInt32 Bits, UInt32 Nonce)
        {
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write4Bytes(Version);
                writer.Write32Bytes(PreviousBlock);
                writer.Write32Bytes(MerkleRoot);
                writer.Write4Bytes(Time);
                writer.Write4Bytes(Bits);
                writer.Write4Bytes(Nonce);

                return new UInt256(Crypto.DoubleSHA256(stream.ToArray()));
            }
        }

        public static long SizeEstimator(BlockHeader blockHeader)
        {
            return 80;
        }
    }
}
