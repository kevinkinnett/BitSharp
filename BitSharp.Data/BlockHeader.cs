using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;

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

        private readonly bool notDefault;
        private readonly int hashCode;

        public BlockHeader(UInt32 version, UInt256 previousBlock, UInt256 merkleRoot, UInt32 time, UInt32 bits, UInt32 nonce, UInt256? hash = null)
        {
            this._version = version;
            this._previousBlock = previousBlock;
            this._merkleRoot = merkleRoot;
            this._time = time;
            this._bits = bits;
            this._nonce = nonce;

            this._hash = hash ?? DataCalculator.CalculateBlockHash(version, previousBlock, merkleRoot, time, bits, nonce);

            this.notDefault = true;
            this.hashCode = this._hash.GetHashCode();
        }

        public bool IsDefault { get { return !this.notDefault; } }

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

        public BigInteger CalculateWork()
        {
            return DataCalculator.CalculateWork(this);
        }

        public UInt256 CalculateTarget()
        {
            return DataCalculator.BitsToTarget(this.Bits);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BlockHeader))
                return false;

            return (BlockHeader)obj == this;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static bool operator ==(BlockHeader left, BlockHeader right)
        {
            return left.Hash == right.Hash && left.Version == right.Version && left.PreviousBlock == right.PreviousBlock && left.MerkleRoot == right.MerkleRoot && left.Time == right.Time && left.Bits == right.Bits && left.Nonce == right.Nonce;
        }

        public static bool operator !=(BlockHeader left, BlockHeader right)
        {
            return !(left == right);
        }

        public static long SizeEstimator(BlockHeader blockHeader)
        {
            return 80;
        }
    }
}
