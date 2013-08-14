using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class TxKeyTest
    {
        [TestMethod]
        public void TestTxKeyIsDefault()
        {
            var defaultTxKey = default(TxKey);
            Assert.IsTrue(defaultTxKey.IsDefault);

            var randomTxKey = RandomData.RandomTxKey();
            Assert.IsFalse(randomTxKey.IsDefault);
        }

        [TestMethod]
        public void TestTxKeyEquality()
        {
            var randomTxKey = RandomData.RandomTxKey();

            var sameTxKey = new TxKey
            (
                txHash: randomTxKey.TxHash,
                blockHash: randomTxKey.BlockHash,
                txIndex: randomTxKey.TxIndex
            );

            var differentTxKeyBlockHash = new TxKey
            (
                txHash: randomTxKey.TxHash,
                blockHash: ~randomTxKey.BlockHash,
                txIndex: randomTxKey.TxIndex
            );

            var differentTxKeyTxIndex = new TxKey
            (
                txHash: randomTxKey.TxHash,
                blockHash: randomTxKey.BlockHash,
                txIndex: ~randomTxKey.TxIndex
            );

            var differentTxKeyTxHash = new TxKey
            (
                txHash: ~randomTxKey.TxHash,
                blockHash: randomTxKey.BlockHash,
                txIndex: randomTxKey.TxIndex
            );

            Assert.IsTrue(randomTxKey.Equals(sameTxKey));
            Assert.IsTrue(randomTxKey == sameTxKey);
            Assert.IsFalse(randomTxKey != sameTxKey);

            Assert.IsFalse(randomTxKey.Equals(differentTxKeyBlockHash));
            Assert.IsFalse(randomTxKey == differentTxKeyBlockHash);
            Assert.IsTrue(randomTxKey != differentTxKeyBlockHash);

            Assert.IsFalse(randomTxKey.Equals(differentTxKeyTxIndex));
            Assert.IsFalse(randomTxKey == differentTxKeyTxIndex);
            Assert.IsTrue(randomTxKey != differentTxKeyTxIndex);

            Assert.IsFalse(randomTxKey.Equals(differentTxKeyTxHash));
            Assert.IsFalse(randomTxKey == differentTxKeyTxHash);
            Assert.IsTrue(randomTxKey != differentTxKeyTxHash);
        }
    }
}
