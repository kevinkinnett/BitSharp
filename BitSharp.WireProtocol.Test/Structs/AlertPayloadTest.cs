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
    public class AlertPayloadTest
    {
        public static readonly AlertPayload ALERT_PAYLOAD_1 = new AlertPayload
        (
            Payload: "Payload",
            Signature: "Signature"
        );

        public static readonly ImmutableArray<byte> ALERT_PAYLOAD_1_BYTES = ImmutableArray.Create<byte>(0x07, 0x50, 0x61, 0x79, 0x6c, 0x6f, 0x61, 0x64, 0x09, 0x53, 0x69, 0x67, 0x6e, 0x61, 0x74, 0x75, 0x72, 0x65);

        [TestMethod]
        public void TestWireEncodeAlertPayload()
        {
            var actual = ALERT_PAYLOAD_1.With().ToRawBytes();
            Debug.WriteLine(actual.ToHexDataString());
            CollectionAssert.AreEqual(ALERT_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeAlertPayload()
        {
            var actual = AlertPayload.FromRawBytes(ALERT_PAYLOAD_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(ALERT_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }
    }
}
