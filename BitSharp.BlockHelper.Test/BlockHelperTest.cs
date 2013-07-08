using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Org.BouncyCastle.Math;
using System.Diagnostics;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.WireProtocol;
using BitSharp.Common.Test;
using System.Globalization;

namespace BitSharp.BlockHelper.Test
{
    [TestClass]
    public class BlockHelperTest : TestBase
    {
        private BlockProvider provider;

        [TestInitialize]
        public void Setup()
        {
            provider = new FileSystemBlockProvider();
            //provider = new BlockExplorerProvider();
        }

        [TestMethod]
        public void TestGetBlockByIndex()
        {
            var block = provider.GetBlock(0);
            AssertBlocksAreEqual(BlockHelperTestData.GENESIS_BLOCK, block);
        }

        [TestMethod]
        public void TestGetBlockByHash()
        {
            var block = provider.GetBlock(BlockHelperTestData.GENESIS_BLOCK_HASH_STRING);
            AssertBlocksAreEqual(BlockHelperTestData.GENESIS_BLOCK, block);
        }

        [TestMethod]
        public void TestGenesisCalculateBlockHash()
        {
            var block = provider.GetBlock(0);
            var result1 = block.Hash;
            var result2 = block.Header.Hash;

            Assert.AreEqual(BlockHelperTestData.GENESIS_BLOCK_HASH, result1);
            Assert.AreEqual(BlockHelperTestData.GENESIS_BLOCK_HASH, result2);
        }

        [TestMethod]
        public void TestBlockChaining()
        {
            var blocks = new Dictionary<int, Block>();

            foreach (var blockIndex in SmallRangeOfBlockIndexes())
            {
                var block = provider.GetBlock(blockIndex);
                blocks[blockIndex] = block;

                if (blocks.ContainsKey(blockIndex - 1))
                {
                    var prevBlock = blocks[blockIndex - 1];
                    Assert.AreEqual(block.Header.PreviousBlock, prevBlock.Hash);
                }
            }
        }

        [TestMethod]
        public void TestMerkleRoots()
        {
            foreach (var block in SmallRangeOfBlocks())
            {
                Assert.AreEqual(block.Header.MerkleRoot, block.Transactions.CalculateMerkleRoot());
            }
        }

        [TestMethod]
        public void TestBlockToRawBytes()
        {
            Debug.WriteLine(BlockHelperTestData.GENESIS_BLOCK_BYTES.ToHexDataString());
            Debug.WriteLine(BlockHelperTestData.GENESIS_BLOCK.With().ToRawBytes().ToHexDataString());

            var data = BlockHelperTestData.GENESIS_BLOCK.With().ToRawBytes();
            Debug.WriteLine(data.ToHexDataString());

            CollectionAssert.AreEqual(BlockHelperTestData.GENESIS_BLOCK_BYTES, BlockHelperTestData.GENESIS_BLOCK.With().ToRawBytes().ToList());
        }

        [TestMethod]
        public void TestTransactionHash()
        {
            var block = provider.GetBlock(0);
            var hash = block.Transactions[0].Hash;

            var expectedHash = UInt256.Parse("4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b", NumberStyles.HexNumber);
            Assert.AreEqual(expectedHash, hash);
        }

        private void AssertBlocksAreEqual(Block expected, Block actual)
        {
            Assert.AreEqual(expected.Header.Version, actual.Header.Version);
            Assert.AreEqual(expected.Header.PreviousBlock, actual.Header.PreviousBlock);
            Assert.AreEqual(expected.Header.MerkleRoot, actual.Header.MerkleRoot);
            Assert.AreEqual(expected.Header.Time, actual.Header.Time);
            Assert.AreEqual(expected.Header.Bits, actual.Header.Bits);
            Assert.AreEqual(expected.Header.Nonce, actual.Header.Nonce);
            //TODO transactions
        }

        private IEnumerable<int> SmallRangeOfBlockIndexes()
        {
            return Enumerable.Range(0, 100).Concat(Enumerable.Range(1000, 100)).Concat(Enumerable.Range(200 * 1000, 100));
        }

        private IEnumerable<Block> SmallRangeOfBlocks()
        {
            foreach (var blockIndex in SmallRangeOfBlockIndexes())
                yield return provider.GetBlock(blockIndex);
        }
    }
}
