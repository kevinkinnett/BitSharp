using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common;
using System.Collections.Immutable;

namespace BitSharp.WireProtocol.Test
{
    [TestClass]
    public class TransactionTest
    {
        public static readonly Transaction TRANSACTION_1 = new Transaction
        (
            Version: 0x01,
            Inputs: ImmutableArray.Create(TransactionInTest.TRANSACTION_INPUT_1),
            Outputs: ImmutableArray.Create(TransactionOutTest.TRANSACTION_OUTPUT_1),
            LockTime: 0x02
        );

        public static readonly ImmutableArray<byte> TRANSACTION_1_BYTES = ImmutableArray.Create<byte>(0x01, 0x00, 0x00, 0x00, 0x01, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0x01, 0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x02, 0x03, 0x04, 0x02, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x02, 0x03, 0x04, 0x02, 0x00, 0x00, 0x00);

        [TestMethod]
        public void TestWireEncodeTransaction()
        {
            var actual = TRANSACTION_1.With().ToRawBytes();
            Debug.WriteLine(actual.ToHexDataString());
            CollectionAssert.AreEqual(TRANSACTION_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransaction()
        {
            var actual = Transaction.FromRawBytes(TRANSACTION_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(TRANSACTION_1_BYTES.ToList(), actual.ToList());
        }
    }
}
