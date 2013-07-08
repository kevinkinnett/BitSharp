using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common;
using System.Globalization;
using System.Collections.Immutable;

namespace BitSharp.WireProtocol.Test
{
    [TestClass]
    public class TransactionInTest
    {
        public static readonly TransactionIn TRANSACTION_INPUT_1 = new TransactionIn
        (
            PreviousTransactionHash: UInt256.Parse("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", NumberStyles.HexNumber),
            PreviousTransactionIndex: 0x01,
            ScriptSignature: ImmutableArray.Create<byte>(0x00, 0x01, 0x02, 0x03, 0x04),
            Sequence: 0x02
        );

        public static readonly ImmutableArray<byte> TRANSACTION_INPUT_1_BYTES = ImmutableArray.Create<byte>(0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0x01, 0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x02, 0x03, 0x04, 0x02, 0x00, 0x00, 0x00);

        [TestMethod]
        public void TestWireEncodeTransactionIn()
        {
            var actual = TRANSACTION_INPUT_1.With().ToRawBytes();
            Debug.WriteLine(actual.ToHexDataString());
            CollectionAssert.AreEqual(TRANSACTION_INPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransactionIn()
        {
            var actual = TransactionIn.FromRawBytes(TRANSACTION_INPUT_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(TRANSACTION_INPUT_1_BYTES.ToList(), actual.ToList());
        }
    }
}
