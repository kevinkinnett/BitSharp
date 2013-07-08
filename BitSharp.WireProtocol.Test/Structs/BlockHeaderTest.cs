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
    public class BlockHeaderTest
    {
        public static readonly BlockHeader BLOCK_HEADER_1 = new BlockHeader
        (
            Version: 0x01,
            PreviousBlock: UInt256.Parse("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", NumberStyles.HexNumber),
            MerkleRoot: UInt256.Parse("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100", NumberStyles.HexNumber),
            Time: 0x02,
            Bits: 0x03,
            Nonce: 0x04
        );

        public static readonly ImmutableArray<byte> BLOCK_HEADER_1_BYTES = ImmutableArray.Create<byte>(0x01, 0x00, 0x00, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0xff, 0xee, 0xdd, 0xcc, 0xbb, 0xaa, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00);

        [TestMethod]
        public void TestWireEncodeBlockHeader()
        {
            var actual = BLOCK_HEADER_1.With().ToRawBytes();
            CollectionAssert.AreEqual(BLOCK_HEADER_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeBlockHeader()
        {
            var actual = BlockHeader.FromRawBytes(BLOCK_HEADER_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(BLOCK_HEADER_1_BYTES.ToList(), actual.ToList());
        }
    }
}
