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
    public class BlockTest
    {
        [TestMethod]
        public void TestIsDefault()
        {
            var defaultBlock = default(Block);
            Assert.IsTrue(defaultBlock.IsDefault);

            var randomBlock = RandomData.RandomBlock();
            Assert.IsFalse(randomBlock.IsDefault);
        }

        [TestMethod]
        public void TestEquality()
        {
            var randomBlock = RandomData.RandomBlock();

            var sameBlock = new Block
            (
                header: new BlockHeader(randomBlock.Header.Version, randomBlock.Header.PreviousBlock, randomBlock.Header.MerkleRoot, randomBlock.Header.Time, randomBlock.Header.Bits, randomBlock.Header.Nonce),
                transactions: ImmutableArray.Create(randomBlock.Transactions.ToArray())
            );

            var differentBlock = randomBlock.With(Header: randomBlock.Header.With(Bits: ~randomBlock.Header.Bits));

            Assert.IsTrue(randomBlock.Equals(sameBlock));
            Assert.IsTrue(randomBlock == sameBlock);
            Assert.IsTrue(!(randomBlock != sameBlock));

            Assert.IsTrue(!randomBlock.Equals(differentBlock));
            Assert.IsTrue(!(randomBlock == differentBlock));
            Assert.IsTrue(randomBlock != differentBlock);
        }
    }
}
