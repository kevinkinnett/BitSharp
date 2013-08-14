using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class UnspentTxTest
    {
        [TestMethod]
        public void TestUnspentTxIsDefault()
        {
            var defaultUnspentTx = default(UnspentTx);
            Assert.IsTrue(defaultUnspentTx.IsDefault);

            var randomUnspentTx = RandomData.RandomUnspentTx();
            Assert.IsFalse(randomUnspentTx.IsDefault);
        }

        [TestMethod]
        public void TestUnspentTxEquality()
        {
            var randomUnspentTx = RandomData.RandomUnspentTx();

            var sameUnspentTx = new UnspentTx
            (
                txHash: randomUnspentTx.TxHash,
                blockHash: randomUnspentTx.BlockHash,
                txIndex: randomUnspentTx.TxIndex,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            var differentUnspentTxBlockHash = new UnspentTx
            (
                txHash: randomUnspentTx.TxHash,
                blockHash: ~randomUnspentTx.BlockHash,
                txIndex: randomUnspentTx.TxIndex,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            var differentUnspentTxTxIndex = new UnspentTx
            (
                txHash: randomUnspentTx.TxHash,
                blockHash: randomUnspentTx.BlockHash,
                txIndex: ~randomUnspentTx.TxIndex,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            var differentUnspentTxTxHash = new UnspentTx
            (
                txHash: ~randomUnspentTx.TxHash,
                blockHash: randomUnspentTx.BlockHash,
                txIndex: randomUnspentTx.TxIndex,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            var differentUnspentTxUnpsentOutputs = new UnspentTx
            (
                txHash: ~randomUnspentTx.TxHash,
                blockHash: randomUnspentTx.BlockHash,
                txIndex: randomUnspentTx.TxIndex,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            Assert.IsTrue(randomUnspentTx.Equals(sameUnspentTx));
            Assert.IsTrue(randomUnspentTx == sameUnspentTx);
            Assert.IsFalse(randomUnspentTx != sameUnspentTx);

            Assert.IsFalse(randomUnspentTx.Equals(differentUnspentTxBlockHash));
            Assert.IsFalse(randomUnspentTx == differentUnspentTxBlockHash);
            Assert.IsTrue(randomUnspentTx != differentUnspentTxBlockHash);

            Assert.IsFalse(randomUnspentTx.Equals(differentUnspentTxTxIndex));
            Assert.IsFalse(randomUnspentTx == differentUnspentTxTxIndex);
            Assert.IsTrue(randomUnspentTx != differentUnspentTxTxIndex);

            Assert.IsFalse(randomUnspentTx.Equals(differentUnspentTxTxHash));
            Assert.IsFalse(randomUnspentTx == differentUnspentTxTxHash);
            Assert.IsTrue(randomUnspentTx != differentUnspentTxTxHash);

            Assert.IsFalse(randomUnspentTx.Equals(differentUnspentTxUnpsentOutputs));
            Assert.IsFalse(randomUnspentTx == differentUnspentTxUnpsentOutputs);
            Assert.IsTrue(randomUnspentTx != differentUnspentTxUnpsentOutputs);
        }
    }
}
