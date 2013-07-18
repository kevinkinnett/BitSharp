using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
    }
}
