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
    public class InventoryVectorTest
    {
        public static readonly InventoryVector INVENTORY_VECTOR_1 = new InventoryVector
        (
            Type: 0x01,
            Hash: UInt256.Parse("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", NumberStyles.HexNumber)
        );

        public static readonly ImmutableArray<byte> INVENTORY_VECTOR_1_BYTES = ImmutableArray.Create<byte>(0x01, 0x00, 0x00, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00);

        [TestMethod]
        public void TestWireEncodeInventoryVector()
        {
            var actual = INVENTORY_VECTOR_1.With().ToRawBytes();
            CollectionAssert.AreEqual(INVENTORY_VECTOR_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeInventoryVector()
        {
            var actual = InventoryVector.FromRawBytes(INVENTORY_VECTOR_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(INVENTORY_VECTOR_1_BYTES.ToList(), actual.ToList());
        }
    }
}
