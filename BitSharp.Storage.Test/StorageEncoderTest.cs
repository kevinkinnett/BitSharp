using BitSharp.Data.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    [TestClass]
    public class StorageEncoderTest
    {
        [TestMethod]
        public void TestDecodeBlock()
        {
            for (var i = 0; i < 100; i++)
            {
                var block = RandomData.RandomBlock();
                //TODO
            }
        }

        [TestMethod]
        public void TestEncodeBlockStream()
        {
        }

        [TestMethod]
        public void TestEncodeBlockArray()
        {
        }

        [TestMethod]
        public void TestDecodeBlockHeader()
        {
        }

        [TestMethod]
        public void TestEncodeBlockHeaderStream()
        {
        }

        [TestMethod]
        public void TestEncodeBlockHeaderArray()
        {
        }

        [TestMethod]
        public void TestDecodeTransaction()
        {
        }

        [TestMethod]
        public void TestEncodeTransactionStream()
        {
        }

        [TestMethod]
        public void TestEncodeTransactionArray()
        {
        }

        [TestMethod]
        public void TestDecodeTxInput()
        {
        }

        [TestMethod]
        public void TestEncodeTxInputStream()
        {
        }

        [TestMethod]
        public void TestEncodeTxInputArray()
        {
        }

        [TestMethod]
        public void TestDecodeTxOutput()
        {
        }

        [TestMethod]
        public void TestEncodeTxOutputStream()
        {
        }

        [TestMethod]
        public void TestEncodeTxOutputArray()
        {
        }

        [TestMethod]
        public void TestDecodeVarBytes()
        {
        }

        [TestMethod]
        public void TestEncodeVarBytes()
        {
        }

        [TestMethod]
        public void TestDecodeList()
        {
        }

        [TestMethod]
        public void TestEncodeList()
        {
        }
    }
}
