using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class TxOutputKeyTest
    {
        [TestMethod]
        public void TestTxOutputKeyIsDefault()
        {
            var defaultTxOutputKey = default(TxOutputKey);
            Assert.IsTrue(defaultTxOutputKey.IsDefault);

            var randomTxOutputKey = RandomData.RandomTxOutputKey();
            Assert.IsFalse(randomTxOutputKey.IsDefault);
        }

        [TestMethod]
        public void TestTxOutputKeyEquality()
        {
            var randomTxOutputKey = RandomData.RandomTxOutputKey();

            var sameTxOutputKey = new TxOutputKey
            (
                txHash: randomTxOutputKey.TxHash,
                txOutputIndex: randomTxOutputKey.TxOutputIndex
            );

            var differentTxOutputKeyTxHash = new TxOutputKey
            (
                txHash: ~randomTxOutputKey.TxHash,
                txOutputIndex: randomTxOutputKey.TxOutputIndex
            );

            var differentTxOutputKeyTxOutputIndex = new TxOutputKey
            (
                txHash: randomTxOutputKey.TxHash,
                txOutputIndex: ~randomTxOutputKey.TxOutputIndex
            );

            Assert.IsTrue(randomTxOutputKey.Equals(sameTxOutputKey));
            Assert.IsTrue(randomTxOutputKey == sameTxOutputKey);
            Assert.IsFalse(randomTxOutputKey != sameTxOutputKey);

            Assert.IsFalse(randomTxOutputKey.Equals(differentTxOutputKeyTxHash));
            Assert.IsFalse(randomTxOutputKey == differentTxOutputKeyTxHash);
            Assert.IsTrue(randomTxOutputKey != differentTxOutputKeyTxHash);

            Assert.IsFalse(randomTxOutputKey.Equals(differentTxOutputKeyTxOutputIndex));
            Assert.IsFalse(randomTxOutputKey == differentTxOutputKeyTxOutputIndex);
            Assert.IsTrue(randomTxOutputKey != differentTxOutputKeyTxOutputIndex);
        }
    }
}
