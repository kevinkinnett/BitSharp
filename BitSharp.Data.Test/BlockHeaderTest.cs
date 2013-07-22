using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class BlockHeaderTest
    {
        [TestMethod]
        public void TestBlockHeaderIsDefault()
        {
            var defaultBlockHeader = default(BlockHeader);
            Assert.IsTrue(defaultBlockHeader.IsDefault);

            var randomBlockHeader = RandomData.RandomBlockHeader();
            Assert.IsFalse(randomBlockHeader.IsDefault);
        }

        [TestMethod]
        public void TestBlockHeaderEquality()
        {
            var randomBlockHeader = RandomData.RandomBlockHeader();

            var sameBlockHeader = new BlockHeader(randomBlockHeader.Version, randomBlockHeader.PreviousBlock, randomBlockHeader.MerkleRoot, randomBlockHeader.Time, randomBlockHeader.Bits, randomBlockHeader.Nonce);

            var differentBlockHeaderVersion = randomBlockHeader.With(Version: ~randomBlockHeader.Version);
            var differentBlockHeaderPreviousBlock = randomBlockHeader.With(PreviousBlock: ~randomBlockHeader.PreviousBlock);
            var differentBlockHeaderMerkleRoot = randomBlockHeader.With(MerkleRoot: ~randomBlockHeader.MerkleRoot);
            var differentBlockHeaderTime = randomBlockHeader.With(Time: ~randomBlockHeader.Time);
            var differentBlockHeaderBits = randomBlockHeader.With(Bits: ~randomBlockHeader.Bits);
            var differentBlockHeaderNonce = randomBlockHeader.With(Nonce: ~randomBlockHeader.Nonce);

            Assert.IsTrue(randomBlockHeader.Equals(sameBlockHeader));
            Assert.IsTrue(randomBlockHeader == sameBlockHeader);
            Assert.IsFalse(randomBlockHeader != sameBlockHeader);

            Assert.IsFalse(randomBlockHeader.Equals(differentBlockHeaderVersion));
            Assert.IsFalse(randomBlockHeader == differentBlockHeaderVersion);
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderVersion);

            Assert.IsFalse(randomBlockHeader.Equals(differentBlockHeaderPreviousBlock));
            Assert.IsFalse(randomBlockHeader == differentBlockHeaderPreviousBlock);
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderPreviousBlock);

            Assert.IsFalse(randomBlockHeader.Equals(differentBlockHeaderMerkleRoot));
            Assert.IsFalse(randomBlockHeader == differentBlockHeaderMerkleRoot);
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderMerkleRoot);

            Assert.IsFalse(randomBlockHeader.Equals(differentBlockHeaderTime));
            Assert.IsFalse(randomBlockHeader == differentBlockHeaderTime);
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderTime);

            Assert.IsFalse(randomBlockHeader.Equals(differentBlockHeaderBits));
            Assert.IsFalse(randomBlockHeader == differentBlockHeaderBits);
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderBits);

            Assert.IsFalse(randomBlockHeader.Equals(differentBlockHeaderNonce));
            Assert.IsFalse(randomBlockHeader == differentBlockHeaderNonce);
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderNonce);
        }
    }
}
