using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;

namespace BitSharp.Script.Test
{
    [TestClass]
    public class ScriptBuilderTest
    {
        private static readonly Random random = new Random();

        [TestMethod]
        public void TestWritePushData0()
        {
            var data = new byte[0];
            var expected = new byte[] { 0x00 };

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        [TestMethod]
        public void TestWritePushData75()
        {
            var data = random.NextBytes(0x4B);
            var expected = new byte[] { 0x4B }.Concat(data);

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        [TestMethod]
        public void TestWritePushData76()
        {
            var data = random.NextBytes(0x4C);
            var expected = new byte[] { 0x4C }.Concat(0x4C).Concat(data);

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        [TestMethod]
        public void TestWritePushData255()
        {
            var data = random.NextBytes(0xFF);
            var expected = new byte[] { 0x4C }.Concat(0xFF).Concat(data);

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        [TestMethod]
        public void TestWritePushData256()
        {
            var data = random.NextBytes(256);
            var expected = new byte[] { 0x4D }.Concat(0x00).Concat(0x01).Concat(data);

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        [TestMethod]
        public void TestWritePushData65535()
        {
            var data = random.NextBytes(65535);
            var expected = new byte[] { 0x4D }.Concat(0xFF).Concat(0xFF).Concat(data);

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        [TestMethod]
        public void TestWritePushData65536()
        {
            var data = random.NextBytes(65536);
            var expected = new byte[] { 0x4E }.Concat(0x00).Concat(0x00).Concat(0x01).Concat(0x00).Concat(data);

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        //TODO how do i enable gcAllowVeryLargeObjects for unit testing?
        [TestMethod]
        public void TestWritePushData4294967295()
        {
            var data = random.NextBytes(4294967295);
            var expected = new byte[] { 0x4E }.Concat(0xFF).Concat(0xFF).Concat(0xFF).Concat(0xFF).Concat(data);

            var actual = new ScriptBuilder();
            actual.WritePushData(data);

            CollectionAssert.AreEqual(expected, actual.GetScript());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestWritePushData4294967296()
        {
            var data = random.NextBytes(4294967296);
            var actual = new ScriptBuilder();
            actual.WritePushData(data);
        }
    }
}
