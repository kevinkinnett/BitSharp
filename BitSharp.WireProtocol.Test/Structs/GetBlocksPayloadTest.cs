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
    public class GetBlocksPayloadTest
    {
        public static readonly GetBlocksPayload GET_BLOCKS_PAYLOAD_1 = new GetBlocksPayload
        (
            Version: 0x01,
            BlockLocatorHashes: ImmutableArray.Create(UInt256.Parse("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", NumberStyles.HexNumber), UInt256.Parse("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100", NumberStyles.HexNumber)),
            HashStop: UInt256.Parse("8899aabbccddeeff00112233445566778899aabbccddeeff0011223344556677", NumberStyles.HexNumber)
        );

        public static readonly ImmutableArray<byte> GET_BLOCKS_PAYLOAD_1_BYTES = ImmutableArray.Create<byte>(0x01, 0x00, 0x00, 0x00, 0x02, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88);

        [TestMethod]
        public void TestWireEncodeGetBlocksPayload()
        {
            var actual = GET_BLOCKS_PAYLOAD_1.With().ToRawBytes();
            Debug.WriteLine(actual.ToHexDataString());
            CollectionAssert.AreEqual(GET_BLOCKS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeGetBlocksPayload()
        {
            var actual = GetBlocksPayload.FromRawBytes(GET_BLOCKS_PAYLOAD_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(GET_BLOCKS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }
    }
}
