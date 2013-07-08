using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System.Collections.Immutable;

namespace BitSharp.WireProtocol.Test
{
    [TestClass]
    public class TransactionOutTest
    {
        public static readonly TransactionOut TRANSACTION_OUTPUT_1 = new TransactionOut
        (
            Value: 0x01,
            ScriptPublicKey: ImmutableArray.Create<byte>(0x00, 0x01, 0x02, 0x03, 0x04)
        );

        public static readonly ImmutableArray<byte> TRANSACTION_OUTPUT_1_BYTES = ImmutableArray.Create<byte>(0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x02, 0x03, 0x04);

        [TestMethod]
        public void TestWireEncodeTransactionOut()
        {
            var actual = TRANSACTION_OUTPUT_1.With().ToRawBytes();
            Debug.WriteLine(actual.ToHexDataString());
            CollectionAssert.AreEqual(TRANSACTION_OUTPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransactionOut()
        {
            var actual = TransactionOut.FromRawBytes(TRANSACTION_OUTPUT_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(TRANSACTION_OUTPUT_1_BYTES.ToList(), actual.ToList());
        }
    }
}
