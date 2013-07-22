using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class DataCalculatorTest
    {
        [TestMethod]
        public void TestBitsToTarget()
        {
            var bits1 = 0x1b0404cbU;
            var expected1 = UInt256.Parse("404cb000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var actual1 = DataCalculator.BitsToTarget(bits1);
            Assert.AreEqual(expected1, actual1);

            // difficulty: 1
            var bits2 = 0x1d00ffffU;
            var expected2 = UInt256.Parse("ffff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var actual2 = DataCalculator.BitsToTarget(bits2);
            Assert.AreEqual(expected2, actual2);
        }

        [TestMethod]
        public void TestTargetToBits()
        {
            var target1 = UInt256.Parse("404cb000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var expected1 = 0x1b0404cbU;
            var actual1 = DataCalculator.TargetToBits(target1);

            Assert.AreEqual(expected1, actual1);

            // difficulty: 1
            var target2 = UInt256.Parse("ffff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var expected2 = 0x1d00ffffU;
            var actual2 = DataCalculator.TargetToBits(target2);

            Assert.AreEqual(expected2, actual2);

            var target3 = UInt256.Parse("7fff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var expected3 = 0x1c7fff00U;
            var actual3 = DataCalculator.TargetToBits(target3);

            Assert.AreEqual(expected3, actual3);
        }

        [TestMethod]
        public void TestCalculateBlockHash()
        {
            var expectedHash = UInt256.Parse("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f", NumberStyles.HexNumber);
            var blockHeader = new BlockHeader
            (
                version: 1,
                previousBlock: 0,
                merkleRoot: UInt256.Parse("4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b", NumberStyles.HexNumber),
                time: 1231006505,
                bits: 486604799,
                nonce: 2083236893
            );

            Assert.AreEqual(expectedHash, DataCalculator.CalculateBlockHash(blockHeader));
            Assert.AreEqual(expectedHash, DataCalculator.CalculateBlockHash(blockHeader.Version, blockHeader.PreviousBlock, blockHeader.MerkleRoot, blockHeader.Time, blockHeader.Bits, blockHeader.Nonce));
        }

        [TestMethod]
        public void TestCalculateTransactionHash()
        {
            var expectedHash = UInt256.Parse("4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b", NumberStyles.HexNumber);
            var tx = new Transaction
            (
                version: 1,
                inputs: ImmutableArray.Create(
                    new TxInput
                    (
                        previousTxOutputKey: new TxOutputKey(txHash: 0, txOutputIndex: 4294967295),
                        scriptSignature: ImmutableArray.Create("04ffff001d0104455468652054696d65732030332f4a616e2f32303039204368616e63656c6c6f72206f6e206272696e6b206f66207365636f6e64206261696c6f757420666f722062616e6b73".HexToByteArray()),
                        sequence: 4294967295
                    )),
                outputs: ImmutableArray.Create(
                    new TxOutput
                    (
                        value: (UInt64)(50L * 100.MILLION()),
                        scriptPublicKey: ImmutableArray.Create("4104678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5fac".HexToByteArray())
                    )),
                lockTime: 0
            );

            Assert.AreEqual(expectedHash, DataCalculator.CalculateTransactionHash(tx));
            Assert.AreEqual(expectedHash, DataCalculator.CalculateTransactionHash(tx.Version, tx.Inputs, tx.Outputs, tx.LockTime));
        }
    }
}
