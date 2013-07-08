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
    public class NetworkAddressWithTimeTest
    {
        public static readonly NetworkAddressWithTime NETWORK_ADDRESS_WITH_TIME_1 = new NetworkAddressWithTime
        (
            Time: 0x01,
            NetworkAddress: NetworkAddressTest.NETWORK_ADDRESS_1
        );

        public static readonly ImmutableArray<byte> NETWORK_ADDRESS_WITH_TIME_1_BYTES = ImmutableArray.Create<byte>(0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x00, 0x02);

        [TestMethod]
        public void TestWireEncodeNetworkAddressWithTime()
        {
            var actual = NETWORK_ADDRESS_WITH_TIME_1.With().ToRawBytes();
            CollectionAssert.AreEqual(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeNetworkAddressWithTime()
        {
            var actual = NetworkAddressWithTime.FromRawBytes(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToArray()).With().ToRawBytes();
            CollectionAssert.AreEqual(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToList(), actual.ToList());
        }
    }
}
