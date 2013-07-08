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
    public class MessageTest
    {
        public static readonly Message MESSAGE_1 = new Message
        (
            Magic: 0xD9B4BEF9,
            Command: "command",
            PayloadSize: 5,
            PayloadChecksum: 0xC1078A76,
            Payload: ImmutableArray.Create<byte>(0, 1, 2, 3, 4)
        );

        public static readonly ImmutableArray<byte> MESSAGE_1_BYTES = ImmutableArray.Create<byte>(0xf9, 0xbe, 0xb4, 0xd9, 0x63, 0x6f, 0x6d, 0x6d, 0x61, 0x6e, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x76, 0x8a, 0x07, 0xc1, 0x00, 0x01, 0x02, 0x03, 0x04);

        [TestMethod]
        public void TestWireEncodeMessage()
        {
            var actual = MESSAGE_1.With().ToRawBytes();
            CollectionAssert.AreEqual(MESSAGE_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeMessage()
        {
            var actual = Message.FromRawBytes(MESSAGE_1_BYTES.ToArray()).With().ToRawBytes();
            Debug.WriteLine(actual.ToHexDataString());
            CollectionAssert.AreEqual(MESSAGE_1_BYTES.ToList(), actual.ToList());
        }
    }
}
