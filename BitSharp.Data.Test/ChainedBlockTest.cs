using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class ChainedBlockTest
    {
        [TestMethod]
        public void TestChainedBlockIsDefault()
        {
            var defaultChainedBlock = default(ChainedBlock);
            Assert.IsTrue(defaultChainedBlock.IsDefault);

            var randomChainedBlock = RandomData.RandomChainedBlock();
            Assert.IsFalse(randomChainedBlock.IsDefault);
        }

        [TestMethod]
        public void TestChainedBlockEquality()
        {
            var randomChainedBlock = RandomData.RandomChainedBlock();

            var sameChainedBlock = new ChainedBlock
            (
                blockHash: randomChainedBlock.BlockHash,
                previousBlockHash: randomChainedBlock.PreviousBlockHash,
                height: randomChainedBlock.Height,
                totalWork: randomChainedBlock.TotalWork
            );

            var differentChainedBlockBlockHash = new ChainedBlock
            (
                blockHash: ~randomChainedBlock.BlockHash,
                previousBlockHash: randomChainedBlock.PreviousBlockHash,
                height: randomChainedBlock.Height,
                totalWork: randomChainedBlock.TotalWork
            );

            var differentChainedBlockPreviousBlockHash = new ChainedBlock
            (
                blockHash: randomChainedBlock.BlockHash,
                previousBlockHash: ~randomChainedBlock.PreviousBlockHash,
                height: randomChainedBlock.Height,
                totalWork: randomChainedBlock.TotalWork
            );

            var differentChainedBlockHeight = new ChainedBlock
            (
                blockHash: randomChainedBlock.BlockHash,
                previousBlockHash: randomChainedBlock.PreviousBlockHash,
                height: ~randomChainedBlock.Height,
                totalWork: randomChainedBlock.TotalWork
            );

            var differentChainedBlockTotalWork = new ChainedBlock
            (
                blockHash: randomChainedBlock.BlockHash,
                previousBlockHash: randomChainedBlock.PreviousBlockHash,
                height: randomChainedBlock.Height,
                totalWork: ~randomChainedBlock.TotalWork
            );

            Assert.IsTrue(randomChainedBlock.Equals(sameChainedBlock));
            Assert.IsTrue(randomChainedBlock == sameChainedBlock);
            Assert.IsFalse(randomChainedBlock != sameChainedBlock);

            Assert.IsFalse(randomChainedBlock.Equals(differentChainedBlockBlockHash));
            Assert.IsFalse(randomChainedBlock == differentChainedBlockBlockHash);
            Assert.IsTrue(randomChainedBlock != differentChainedBlockBlockHash);

            Assert.IsFalse(randomChainedBlock.Equals(differentChainedBlockPreviousBlockHash));
            Assert.IsFalse(randomChainedBlock == differentChainedBlockPreviousBlockHash);
            Assert.IsTrue(randomChainedBlock != differentChainedBlockPreviousBlockHash);

            Assert.IsFalse(randomChainedBlock.Equals(differentChainedBlockHeight));
            Assert.IsFalse(randomChainedBlock == differentChainedBlockHeight);
            Assert.IsTrue(randomChainedBlock != differentChainedBlockHeight);

            Assert.IsFalse(randomChainedBlock.Equals(differentChainedBlockTotalWork));
            Assert.IsFalse(randomChainedBlock == differentChainedBlockTotalWork);
            Assert.IsTrue(randomChainedBlock != differentChainedBlockTotalWork);
        }
    }
}
