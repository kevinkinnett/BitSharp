using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common.Test;

namespace BitSharp.Common.Test
{
    [TestClass]
    public class ExtensionMethodTests : TestBase
    {
        [TestMethod]
        public void TestByteArrayConcat()
        {
            var first = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
            var second = (byte)127;

            var result = first.Concat(second).ToArray();
            CollectionAssert.AreEqual(first, result.Take(256).ToArray());
            Assert.AreEqual(second, result[256]);
        }

        [TestMethod]
        public void TestListConcat()
        {
            var first = Enumerable.Range(0, 256).Select(x => (byte)x).ToList();
            var second = (byte)127;

            var result = first.Concat(second).ToList();
            CollectionAssert.AreEqual(first, result.Take(256).ToList());
            Assert.AreEqual(second, result[256]);
        }
    }
}
