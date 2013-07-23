using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class BlockchainMetadataTest
    {
        [TestMethod]
        public void TestBlockchainMetadataEquality()
        {
            var randomBlockchainMetadata = RandomData.RandomBlockchainMetadata();

            var sameBlockchainMetadata = new BlockchainMetadata
            (
                guid: randomBlockchainMetadata.Guid,
                rootBlockHash: randomBlockchainMetadata.RootBlockHash,
                totalWork: randomBlockchainMetadata.TotalWork
            );

            var differentGuid = Guid.NewGuid();
            while (differentGuid == randomBlockchainMetadata.Guid)
                differentGuid = Guid.NewGuid();

            var differentBlockchainMetadataGuid = new BlockchainMetadata
            (
                guid: differentGuid,
                rootBlockHash: randomBlockchainMetadata.RootBlockHash,
                totalWork: randomBlockchainMetadata.TotalWork
            );

            var differentBlockchainMetadataRootBlockHash = new BlockchainMetadata
            (
                guid: randomBlockchainMetadata.Guid,
                rootBlockHash: ~randomBlockchainMetadata.RootBlockHash,
                totalWork: randomBlockchainMetadata.TotalWork
            );

            var differentBlockchainMetadataTotalWork = new BlockchainMetadata
            (
                guid: randomBlockchainMetadata.Guid,
                rootBlockHash: randomBlockchainMetadata.RootBlockHash,
                totalWork: ~randomBlockchainMetadata.TotalWork
            );

            Assert.IsTrue(randomBlockchainMetadata.Equals(sameBlockchainMetadata));
            Assert.IsTrue(randomBlockchainMetadata == sameBlockchainMetadata);
            Assert.IsFalse(randomBlockchainMetadata != sameBlockchainMetadata);

            Assert.IsFalse(randomBlockchainMetadata.Equals(differentBlockchainMetadataGuid));
            Assert.IsFalse(randomBlockchainMetadata == differentBlockchainMetadataGuid);
            Assert.IsTrue(randomBlockchainMetadata != differentBlockchainMetadataGuid);

            Assert.IsFalse(randomBlockchainMetadata.Equals(differentBlockchainMetadataRootBlockHash));
            Assert.IsFalse(randomBlockchainMetadata == differentBlockchainMetadataRootBlockHash);
            Assert.IsTrue(randomBlockchainMetadata != differentBlockchainMetadataRootBlockHash);

            Assert.IsFalse(randomBlockchainMetadata.Equals(differentBlockchainMetadataTotalWork));
            Assert.IsFalse(randomBlockchainMetadata == differentBlockchainMetadataTotalWork);
            Assert.IsTrue(randomBlockchainMetadata != differentBlockchainMetadataTotalWork);
        }
    }
}
