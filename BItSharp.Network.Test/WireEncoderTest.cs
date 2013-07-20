using BitSharp.Data;
using BitSharp.Common.ExtensionMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Network.Test
{
    [TestClass]
    public partial class WireEncoderTest
    {
        [TestMethod]
        public void TestWireEncodeAddressPayload()
        {
            var actual = WireEncoder.EncodeAddressPayload(ADDRESS_PAYLOAD_1);
            CollectionAssert.AreEqual(ADDRESS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeAddressPayload()
        {
            var actual = WireEncoder.EncodeAddressPayload(WireEncoder.DecodeAddressPayload(ADDRESS_PAYLOAD_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(ADDRESS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeAlertPayload()
        {
            var actual = WireEncoder.EncodeAlertPayload(ALERT_PAYLOAD_1);
            CollectionAssert.AreEqual(ALERT_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeAlertPayload()
        {
            var actual = WireEncoder.EncodeAlertPayload(WireEncoder.DecodeAlertPayload(ALERT_PAYLOAD_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(ALERT_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeBlockHeader()
        {
            var actual = WireEncoder.EncodeBlockHeader(BLOCK_HEADER_1);
            CollectionAssert.AreEqual(BLOCK_HEADER_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeBlockHeader()
        {
            var actual = WireEncoder.EncodeBlockHeader(WireEncoder.DecodeBlockHeader(BLOCK_HEADER_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(BLOCK_HEADER_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeBlock()
        {
            var actual = WireEncoder.EncodeBlock(BLOCK_1);
            CollectionAssert.AreEqual(BLOCK_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeBlock()
        {
            var actual = WireEncoder.EncodeBlock(WireEncoder.DecodeBlock(BLOCK_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(BLOCK_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeGetBlocksPayload()
        {
            var actual = WireEncoder.EncodeGetBlocksPayload(GET_BLOCKS_PAYLOAD_1);
            CollectionAssert.AreEqual(GET_BLOCKS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeGetBlocksPayload()
        {
            var actual = WireEncoder.EncodeGetBlocksPayload(WireEncoder.DecodeGetBlocksPayload(GET_BLOCKS_PAYLOAD_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(GET_BLOCKS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeInventoryPayload()
        {
            var actual = WireEncoder.EncodeInventoryPayload(INVENTORY_PAYLOAD_1);
            CollectionAssert.AreEqual(INVENTORY_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeInventoryPayload()
        {
            var actual = WireEncoder.EncodeInventoryPayload(WireEncoder.DecodeInventoryPayload(INVENTORY_PAYLOAD_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(INVENTORY_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeInventoryVector()
        {
            var actual = WireEncoder.EncodeInventoryVector(INVENTORY_VECTOR_1);
            CollectionAssert.AreEqual(INVENTORY_VECTOR_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeInventoryVector()
        {
            var actual = WireEncoder.EncodeInventoryVector(WireEncoder.DecodeInventoryVector(INVENTORY_VECTOR_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(INVENTORY_VECTOR_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeMessage()
        {
            var actual = WireEncoder.EncodeMessage(MESSAGE_1);
            CollectionAssert.AreEqual(MESSAGE_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeMessage()
        {
            var actual = WireEncoder.EncodeMessage(WireEncoder.DecodeMessage(MESSAGE_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(MESSAGE_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeNetworkAddress()
        {
            var actual = WireEncoder.EncodeNetworkAddress(NETWORK_ADDRESS_1);
            CollectionAssert.AreEqual(NETWORK_ADDRESS_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeNetworkAddress()
        {
            var actual = WireEncoder.EncodeNetworkAddress(WireEncoder.DecodeNetworkAddress(NETWORK_ADDRESS_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(NETWORK_ADDRESS_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeNetworkAddressWithTime()
        {
            var actual = WireEncoder.EncodeNetworkAddressWithTime(NETWORK_ADDRESS_WITH_TIME_1);
            CollectionAssert.AreEqual(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeNetworkAddressWithTime()
        {
            var actual = WireEncoder.EncodeNetworkAddressWithTime(WireEncoder.DecodeNetworkAddressWithTime(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeTransactionIn()
        {
            var actual = WireEncoder.EncodeTxInput(TRANSACTION_INPUT_1);
            CollectionAssert.AreEqual(TRANSACTION_INPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransactionIn()
        {
            var actual = WireEncoder.EncodeTxInput(WireEncoder.DecodeTxInput(TRANSACTION_INPUT_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(TRANSACTION_INPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeTransactionOut()
        {
            var actual = WireEncoder.EncodeTxOutput(TRANSACTION_OUTPUT_1);
            CollectionAssert.AreEqual(TRANSACTION_OUTPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransactionOut()
        {
            var actual = WireEncoder.EncodeTxOutput(WireEncoder.DecodeTxOutput(TRANSACTION_OUTPUT_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(TRANSACTION_OUTPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeTransaction()
        {
            var actual = WireEncoder.EncodeTransaction(TRANSACTION_1);
            CollectionAssert.AreEqual(TRANSACTION_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransaction()
        {
            var actual = WireEncoder.EncodeTransaction(WireEncoder.DecodeTransaction(TRANSACTION_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(TRANSACTION_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeVersionPayload()
        {
            var actual1 = WireEncoder.EncodeVersionPayload(VERSION_PAYLOAD_1);
            CollectionAssert.AreEqual(VERSION_PAYLOAD_1_BYTES.ToList(), actual1.ToList());

            var actual2 = WireEncoder.EncodeVersionPayload(VERSION_PAYLOAD_2);
            CollectionAssert.AreEqual(VERSION_PAYLOAD_2_BYTES.ToList(), actual2.ToList());
        }

        [TestMethod]
        public void TestWireDecodeVersionPayload()
        {
            var actual1 = WireEncoder.EncodeVersionPayload(WireEncoder.DecodeVersionPayload(VERSION_PAYLOAD_1_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(VERSION_PAYLOAD_1_BYTES.ToList(), actual1.ToList());

            var actual2 = WireEncoder.EncodeVersionPayload(WireEncoder.DecodeVersionPayload(VERSION_PAYLOAD_2_BYTES.ToArray().ToMemoryStream()));
            CollectionAssert.AreEqual(VERSION_PAYLOAD_2_BYTES.ToList(), actual2.ToList());
        }
    }
}
