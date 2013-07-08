using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    [TestClass]
    public class MainnetRulesTest
    {
        private MainnetRules rules;

        [TestInitialize]
        public void TestInitialize()
        {
            this.rules = new MainnetRules();
        }

        [TestMethod]
        public void TestBitsToTarget()
        {
            var bits1 = 0x1b0404cbU;
            var expected1 = UInt256.Parse("404cb000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var actual1 = this.rules.BitsToTarget(bits1);
            Assert.AreEqual(expected1, actual1);

            // difficulty: 1
            var bits2 = 0x1d00ffffU;
            var expected2 = UInt256.Parse("ffff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var actual2 = this.rules.BitsToTarget(bits2);
            Assert.AreEqual(expected2, actual2);
        }

        [TestMethod]
        public void TestTargetToDifficulty()
        {
            // difficulty will be matched to 9 decimal places
            var precision = 1e-9D;

            var bits1 = 0x1b0404cbU;
            var expected1 = 16307.420938524D; //16,307.420938523983278341199298581
            var actual1 = this.rules.TargetToDifficulty(this.rules.BitsToTarget(bits1));

            Assert.AreEqual(expected1, actual1, precision);

            // difficulty: 1
            var bits2 = 0x1d00ffffU;
            var expected2 = 1D;
            var actual2 = this.rules.TargetToDifficulty(this.rules.BitsToTarget(bits2));

            Assert.AreEqual(expected2, actual2, precision);

            var bits3 = 0x1c7fff00U;
            var expected3 = 2.000030518D;
            var actual3 = this.rules.TargetToDifficulty(this.rules.BitsToTarget(bits3));

            Assert.AreEqual(expected3, actual3, precision);
        }

        [TestMethod]
        public void TestTargetToBits()
        {
            var target1 = UInt256.Parse("404cb000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var expected1 = 0x1b0404cbU;
            var actual1 = this.rules.TargetToBits(target1);

            Assert.AreEqual(expected1, actual1);

            // difficulty: 1
            var target2 = UInt256.Parse("ffff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var expected2 = 0x1d00ffffU;
            var actual2 = this.rules.TargetToBits(target2);

            Assert.AreEqual(expected2, actual2);

            var target3 = UInt256.Parse("7fff0000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
            var expected3 = 0x1c7fff00U;
            var actual3 = this.rules.TargetToBits(target3);

            Assert.AreEqual(expected3, actual3);
        }

        [TestMethod]
        public void TestDifficultyToTarget()
        {
            var difficulty1 = 16307.420938524D;
            var expected1 = 0x1b0404cbU;
            var actual1 = this.rules.TargetToBits(this.rules.DifficultyToTarget(difficulty1));

            Assert.AreEqual(expected1, actual1);

            // difficulty: 1
            var difficulty2 = 1D;
            var expected2 = 0x1d00ffffU;
            var actual2 = this.rules.TargetToBits(this.rules.DifficultyToTarget(difficulty2));

            Assert.AreEqual(expected2, actual2);

            var difficulty3 = 2.000030518D;
            var expected3 = 0x1c7fff00U;
            var actual3 = this.rules.TargetToBits(this.rules.DifficultyToTarget(difficulty3));

            Assert.AreEqual(expected3, actual3);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestTargetBitsTooHigh()
        {
            Assert.AreEqual(this.rules.HighestTargetBits, 0x1d00ffffU);

            var bits = 0x1d00ffffU + 1;
            this.rules.BitsToTarget(bits);
        }
    }
}
