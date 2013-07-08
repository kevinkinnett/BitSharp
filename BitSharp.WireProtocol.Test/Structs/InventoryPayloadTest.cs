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
    public class InventoryPayloadTest
    {
        public static readonly InventoryPayload INVENTORY_PAYLOAD_1 = new InventoryPayload
        (
            InventoryVectors: ImmutableArray.Create(InventoryVectorTest.INVENTORY_VECTOR_1)
        );

        public static readonly ImmutableArray<byte> INVENTORY_PAYLOAD_1_BYTES = ImmutableArray.Create<byte>(0x01, 0x01, 0x00, 0x00, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00);

        [TestMethod]
        public void TestWireEncodeInventoryPayload()
        {
            var actual = INVENTORY_PAYLOAD_1.ToRawBytes();
            Debug.WriteLine(actual.ToHexDataString());
            CollectionAssert.AreEqual(INVENTORY_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeInventoryPayload()
        {
            var actual = InventoryPayload.FromRawBytes(INVENTORY_PAYLOAD_1_BYTES.ToArray()).ToRawBytes();
            CollectionAssert.AreEqual(INVENTORY_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }
    }
}
