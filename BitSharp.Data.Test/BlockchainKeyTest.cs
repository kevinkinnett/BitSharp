using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class BlockchainKeyTest
    {
        [TestMethod]
        public void TestBlockchainKeyIsDefault()
        {
            var defaultBlockchainKey = default(BlockchainKey);
            Assert.IsTrue(defaultBlockchainKey.IsDefault);

            var randomBlockchainKey = RandomData.RandomBlockchainKey();
            Assert.IsFalse(randomBlockchainKey.IsDefault);
        }

        [TestMethod]
        public void TestBlockchainKeyEquality()
        {
            var randomBlockchainKey = RandomData.RandomBlockchainKey();

            var sameBlockchainKey = new BlockchainKey
            (
                guid: randomBlockchainKey.Guid,
                rootBlockHash: randomBlockchainKey.RootBlockHash
            );

            var differentGuid = Guid.NewGuid();
            while (differentGuid == randomBlockchainKey.Guid)
                differentGuid = Guid.NewGuid();

            var differentBlockchainKeyGuid = new BlockchainKey
            (
                guid: differentGuid,
                rootBlockHash: randomBlockchainKey.RootBlockHash
            );

            var differentBlockchainKeyRootBlockHash = new BlockchainKey
            (
                guid: randomBlockchainKey.Guid,
                rootBlockHash: ~randomBlockchainKey.RootBlockHash
            );

            Assert.IsTrue(randomBlockchainKey.Equals(sameBlockchainKey));
            Assert.IsTrue(randomBlockchainKey == sameBlockchainKey);
            Assert.IsFalse(randomBlockchainKey != sameBlockchainKey);

            Assert.IsFalse(randomBlockchainKey.Equals(differentBlockchainKeyGuid));
            Assert.IsFalse(randomBlockchainKey == differentBlockchainKeyGuid);
            Assert.IsTrue(randomBlockchainKey != differentBlockchainKeyGuid);

            Assert.IsFalse(randomBlockchainKey.Equals(differentBlockchainKeyRootBlockHash));
            Assert.IsFalse(randomBlockchainKey == differentBlockchainKeyRootBlockHash);
            Assert.IsTrue(randomBlockchainKey != differentBlockchainKeyRootBlockHash);
        }
    }
}
