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
            Assert.IsTrue(!(randomBlockHeader != sameBlockHeader));

            Assert.IsTrue(!randomBlockHeader.Equals(differentBlockHeaderVersion));
            Assert.IsTrue(!(randomBlockHeader == differentBlockHeaderVersion));
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderVersion);

            Assert.IsTrue(!randomBlockHeader.Equals(differentBlockHeaderPreviousBlock));
            Assert.IsTrue(!(randomBlockHeader == differentBlockHeaderPreviousBlock));
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderPreviousBlock);

            Assert.IsTrue(!randomBlockHeader.Equals(differentBlockHeaderMerkleRoot));
            Assert.IsTrue(!(randomBlockHeader == differentBlockHeaderMerkleRoot));
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderMerkleRoot);

            Assert.IsTrue(!randomBlockHeader.Equals(differentBlockHeaderTime));
            Assert.IsTrue(!(randomBlockHeader == differentBlockHeaderTime));
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderTime);

            Assert.IsTrue(!randomBlockHeader.Equals(differentBlockHeaderBits));
            Assert.IsTrue(!(randomBlockHeader == differentBlockHeaderBits));
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderBits);

            Assert.IsTrue(!randomBlockHeader.Equals(differentBlockHeaderNonce));
            Assert.IsTrue(!(randomBlockHeader == differentBlockHeaderNonce));
            Assert.IsTrue(randomBlockHeader != differentBlockHeaderNonce);
        }
    }
}
