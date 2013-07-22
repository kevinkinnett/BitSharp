using System;
using System.Linq;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Numerics;

namespace BitSharp.Common.Test
{
    [TestClass]
    public class UInt256Test
    {
        [TestMethod]
        public void TestUInt256Equality()
        {
            var part1 = 0UL;
            var part2 = 1UL;
            var part3 = 2UL;
            var part4 = 3UL;

            var expectedBigInt = (new BigInteger(part1) << 96) + (new BigInteger(part2) << 64) + (new BigInteger(part3) << 32) + new BigInteger(part4);

            var expected = new UInt256(expectedBigInt);

            var same = new UInt256(expectedBigInt);
            var differentPart1 = new UInt256((new BigInteger(~part1) << 96) + (new BigInteger(part2) << 64) + (new BigInteger(part3) << 32) + new BigInteger(part4));
            var differentPart2 = new UInt256((new BigInteger(part1) << 96) + (new BigInteger(~part2) << 64) + (new BigInteger(part3) << 32) + new BigInteger(part4));
            var differentPart3 = new UInt256((new BigInteger(part1) << 96) + (new BigInteger(part2) << 64) + (new BigInteger(~part3) << 32) + new BigInteger(part4));
            var differentPart4 = new UInt256((new BigInteger(part1) << 96) + (new BigInteger(part2) << 64) + (new BigInteger(part3) << 32) + new BigInteger(~part4));

            Assert.IsTrue(expected.Equals(same));
            Assert.IsTrue(expected == same);
            Assert.IsFalse(expected != same);

            Assert.IsFalse(expected.Equals(differentPart1));
            Assert.IsFalse(expected == differentPart1);
            Assert.IsTrue(expected != differentPart1);

            Assert.IsFalse(expected.Equals(differentPart2));
            Assert.IsFalse(expected == differentPart2);
            Assert.IsTrue(expected != differentPart2);

            Assert.IsFalse(expected.Equals(differentPart3));
            Assert.IsFalse(expected == differentPart3);
            Assert.IsTrue(expected != differentPart3);

            Assert.IsFalse(expected.Equals(differentPart4));
            Assert.IsFalse(expected == differentPart4);
            Assert.IsTrue(expected != differentPart4);
        }

        [TestMethod]
        public void TestUInt256Inequality()
        {
            var expected = UInt256.Parse(TestData.HEX_STRING_64, NumberStyles.HexNumber);
            var actual = UInt256.Zero;

            Assert.AreNotEqual(expected, actual);
            Assert.IsFalse(expected == actual);
            Assert.IsTrue(expected != actual);
            CollectionAssert.AreNotEqual(expected.ToByteArray(), actual.ToByteArray());
        }

        [TestMethod]
        public void TestUInt256RawBytes()
        {
            var expected = UInt256.Parse(TestData.HEX_STRING_64, NumberStyles.HexNumber);
            var actual1 = new UInt256(expected.ToByteArray());
            var actual2 = new UInt256(new UInt256(expected.ToByteArray()).ToByteArray());
            var actual3 = new UInt256(new UInt256(new UInt256(expected.ToByteArray()).ToByteArray()).ToByteArray());

            Assert.AreEqual(expected, actual1);
            Assert.AreEqual(expected, actual2);
            Assert.AreEqual(expected, actual3);
        }

        [TestMethod]
        public void TestUInt256Sha256()
        {
            var expected = Crypto.DoubleSHA256(UInt256.Parse(TestData.HEX_STRING_64, NumberStyles.HexNumber).ToByteArray());
            var actual = new UInt256(expected).ToByteArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestUInt256HexString()
        {
            var hex = TestData.HEX_STRING_64;
            var expected1 = "0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b";
            var expected2 = "[3b,a3,ed,fd,7a,7b,12,b2,7a,c7,2c,3e,67,76,8f,61,7f,c8,1b,c3,88,8a,51,32,3a,9f,b8,aa,4b,1e,5e,4a]";

            var actual1 = UInt256.Parse(hex, NumberStyles.HexNumber).ToHexNumberString();
            var actual2 = UInt256.Parse(hex, NumberStyles.HexNumber).ToHexDataString();

            Assert.AreEqual(expected1, actual1);
            Assert.AreEqual(expected2, actual2);
        }
    }
}
