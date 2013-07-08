using System;
using System.Linq;
using BitSharp.Common.ExtensionMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace BitSharp.Common.Test
{
    [TestClass]
    public class UIntTest
    {
        [TestMethod]
        public void TestUInt256Equality()
        {
            var expected = UInt256.Parse(TestData.HEX_STRING_64, NumberStyles.HexNumber);
            var actual = new UInt256(expected.ToByteArray());

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(expected == actual);
            Assert.IsFalse(expected != actual);
            CollectionAssert.AreEqual(expected.ToByteArray(), actual.ToByteArray());
        }

        [TestMethod]
        public void TestUInt256Inequality()
        {
            var expected = UInt256.Parse(TestData.HEX_STRING_64, NumberStyles.HexNumber);
            var actual = (UInt256)0;

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

        [TestMethod]
        public void TestUInt128Equality()
        {
            var expected = UInt128.Parse(TestData.HEX_STRING_32, NumberStyles.HexNumber);
            var actual = new UInt128(expected.ToByteArray());

            Assert.AreEqual(expected, actual);
            Assert.IsTrue(expected == actual);
            Assert.IsFalse(expected != actual);
            CollectionAssert.AreEqual(expected.ToByteArray(), actual.ToByteArray());
        }

        [TestMethod]
        public void TestUInt128Inequality()
        {
            var expected = UInt128.Parse(TestData.HEX_STRING_32, NumberStyles.HexNumber);
            var actual = (UInt128)0;

            Assert.AreNotEqual(expected, actual);
            Assert.IsFalse(expected == actual);
            Assert.IsTrue(expected != actual);
            CollectionAssert.AreNotEqual(expected.ToByteArray(), actual.ToByteArray());
        }

        [TestMethod]
        public void TestUInt128RawBytes()
        {
            var expected = UInt128.Parse(TestData.HEX_STRING_32, NumberStyles.HexNumber);
            var actual1 = new UInt128(expected.ToByteArray());
            var actual2 = new UInt128(new UInt128(expected.ToByteArray()).ToByteArray());
            var actual3 = new UInt128(new UInt128(new UInt128(expected.ToByteArray()).ToByteArray()).ToByteArray());

            Assert.AreEqual(expected, actual1);
            Assert.AreEqual(expected, actual2);
            Assert.AreEqual(expected, actual3);
        }

        [TestMethod]
        public void TestUInt128Sha256()
        {
            var expected = Crypto.DoubleSHA256(UInt128.Parse(TestData.HEX_STRING_32, NumberStyles.HexNumber).ToByteArray()).Take(16).ToArray();
            var actual = new UInt128(expected).ToByteArray();

            CollectionAssert.AreEqual(expected.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestUInt128HexString()
        {
            var hex = TestData.HEX_STRING_32;
            var expected1 = "0x4a5e1e4baab89f3a32518a88c31bc87f";
            var expected2 = "[7f,c8,1b,c3,88,8a,51,32,3a,9f,b8,aa,4b,1e,5e,4a]";

            var actual1 = UInt128.Parse(hex, NumberStyles.HexNumber).ToHexNumberString();
            var actual2 = UInt128.Parse(hex, NumberStyles.HexNumber).ToHexDataString();

            Assert.AreEqual(expected1, actual1);
            Assert.AreEqual(expected2, actual2);
        }
    }
}
