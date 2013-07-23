using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Network.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Network
{
    /// <summary>
    /// NetworkEncoder is responsible for encoding and decoding data according to the P2P protocol.
    /// </summary>
    public class NetworkEncoder
    {
        public static Block DecodeBlock(Stream stream, UInt256? blockHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new Block
                (
                    header: DecodeBlockHeader(stream, blockHash),
                    transactions: reader.DecodeList<Transaction>(() => DecodeTransaction(stream))
                );
            }
        }

        public static void EncodeBlock(Stream stream, Block block)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                EncodeBlockHeader(stream, block.Header);
                writer.EncodeList(block.Transactions, tx => EncodeTransaction(stream, tx));
            }
        }

        public static byte[] EncodeBlock(Block block)
        {
            var stream = new MemoryStream();
            EncodeBlock(stream, block);
            return stream.ToArray();
        }

        public static BlockHeader DecodeBlockHeader(Stream stream, UInt256? blockHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new BlockHeader
                (
                    version: reader.Read4Bytes(),
                    previousBlock: reader.Read32Bytes(),
                    merkleRoot: reader.Read32Bytes(),
                    time: reader.Read4Bytes(),
                    bits: reader.Read4Bytes(),
                    nonce: reader.Read4Bytes(),
                    hash: blockHash
                );
            }
        }

        public static void EncodeBlockHeader(Stream stream, BlockHeader blockHeader)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(blockHeader.Version);
                writer.Write32Bytes(blockHeader.PreviousBlock);
                writer.Write32Bytes(blockHeader.MerkleRoot);
                writer.Write4Bytes(blockHeader.Time);
                writer.Write4Bytes(blockHeader.Bits);
                writer.Write4Bytes(blockHeader.Nonce);
            }
        }

        public static byte[] EncodeBlockHeader(BlockHeader blockHeader)
        {
            var stream = new MemoryStream();
            EncodeBlockHeader(stream, blockHeader);
            return stream.ToArray();
        }

        public static Transaction DecodeTransaction(Stream stream, UInt256? txHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new Transaction
                (
                    version: reader.Read4Bytes(),
                    inputs: reader.DecodeList(() => DecodeTxInput(stream)),
                    outputs: reader.DecodeList(() => DecodeTxOutput(stream)),
                    lockTime: reader.Read4Bytes(),
                    hash: txHash
                );
            }
        }

        public static void EncodeTransaction(Stream stream, Transaction tx)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(tx.Version);
                writer.EncodeList(tx.Inputs, input => EncodeTxInput(stream, input));
                writer.EncodeList(tx.Outputs, output => EncodeTxOutput(stream, output));
                writer.Write4Bytes(tx.LockTime);
            }
        }

        public static byte[] EncodeTransaction(Transaction tx)
        {
            var stream = new MemoryStream();
            EncodeTransaction(stream, tx);
            return stream.ToArray();
        }

        public static TxInput DecodeTxInput(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new TxInput
                (
                    previousTxOutputKey: new TxOutputKey
                    (
                        txHash: reader.Read32Bytes(),
                        txOutputIndex: reader.Read4Bytes()
                    ),
                    scriptSignature: reader.ReadVarBytes().ToImmutableArray(),
                    sequence: reader.Read4Bytes()
                );
            }
        }

        public static void EncodeTxInput(Stream stream, TxInput txInput)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(txInput.PreviousTxOutputKey.TxHash);
                writer.Write4Bytes(txInput.PreviousTxOutputKey.TxOutputIndex);
                writer.WriteVarBytes(txInput.ScriptSignature.ToArray());
                writer.Write4Bytes(txInput.Sequence);
            }
        }

        public static byte[] EncodeTxInput(TxInput txInput)
        {
            var stream = new MemoryStream();
            EncodeTxInput(stream, txInput);
            return stream.ToArray();
        }

        public static TxOutput DecodeTxOutput(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new TxOutput
                (
                    value: reader.Read8Bytes(),
                    scriptPublicKey: reader.ReadVarBytes().ToImmutableArray()
                );
            }
        }

        public static void EncodeTxOutput(Stream stream, TxOutput txOutput)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write8Bytes(txOutput.Value);
                writer.WriteVarBytes(txOutput.ScriptPublicKey.ToArray());
            }
        }

        public static byte[] EncodeTxOutput(TxOutput txOutput)
        {
            var stream = new MemoryStream();
            EncodeTxOutput(stream, txOutput);
            return stream.ToArray();
        }

        public static AlertPayload DecodeAlertPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new AlertPayload
                (
                    Payload: reader.ReadVarString(),
                    Signature: reader.ReadVarString()
                );
            }
        }

        public static void EncodeAlertPayload(Stream stream, AlertPayload alertPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteVarString(alertPayload.Payload);
                writer.WriteVarString(alertPayload.Signature);
            }
        }

        public static byte[] EncodeAlertPayload(AlertPayload alertPayload)
        {
            var stream = new MemoryStream();
            EncodeAlertPayload(stream, alertPayload);
            return stream.ToArray();
        }

        public static AddressPayload DecodeAddressPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new AddressPayload
                (
                    NetworkAddresses: reader.DecodeList(() => DecodeNetworkAddressWithTime(stream))
                );
            }
        }

        public static void EncodeAddressPayload(Stream stream, AddressPayload addressPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.EncodeList(addressPayload.NetworkAddresses, networkAddress => EncodeNetworkAddressWithTime(stream, networkAddress));
            }
        }

        public static byte[] EncodeAddressPayload(AddressPayload addressPayload)
        {
            var stream = new MemoryStream();
            EncodeAddressPayload(stream, addressPayload);
            return stream.ToArray();
        }

        public static GetBlocksPayload DecodeGetBlocksPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new GetBlocksPayload
                (
                    Version: reader.Read4Bytes(),
                    BlockLocatorHashes: reader.DecodeList(() => reader.Read32Bytes()),
                    HashStop: reader.Read32Bytes()
                );
            }
        }

        public static void EncodeGetBlocksPayload(Stream stream, GetBlocksPayload getBlocksPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(getBlocksPayload.Version);
                writer.EncodeList(getBlocksPayload.BlockLocatorHashes, locatorHash => writer.Write32Bytes(locatorHash));
                writer.Write32Bytes(getBlocksPayload.HashStop);
            }
        }

        public static byte[] EncodeGetBlocksPayload(GetBlocksPayload getBlocksPayload)
        {
            var stream = new MemoryStream();
            EncodeGetBlocksPayload(stream, getBlocksPayload);
            return stream.ToArray();
        }

        public static InventoryPayload DecodeInventoryPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new InventoryPayload
                (
                    InventoryVectors: reader.DecodeList(() => DecodeInventoryVector(stream))
                );
            }
        }

        public static void EncodeInventoryPayload(Stream stream, InventoryPayload invPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.EncodeList(invPayload.InventoryVectors, invVector => EncodeInventoryVector(stream, invVector));
            }
        }

        public static byte[] EncodeInventoryPayload(InventoryPayload invPayload)
        {
            var stream = new MemoryStream();
            EncodeInventoryPayload(stream, invPayload);
            return stream.ToArray();
        }

        public static InventoryVector DecodeInventoryVector(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new InventoryVector
                (
                    Type: reader.Read4Bytes(),
                    Hash: reader.Read32Bytes()
                );
            }
        }

        public static void EncodeInventoryVector(Stream stream, InventoryVector invVector)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(invVector.Type);
                writer.Write32Bytes(invVector.Hash);
            }
        }

        public static byte[] EncodeInventoryVector(InventoryVector invVector)
        {
            var stream = new MemoryStream();
            EncodeInventoryVector(stream, invVector);
            return stream.ToArray();
        }

        public static Message DecodeMessage(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                var magic = reader.Read4Bytes();
                var command = reader.ReadFixedString(12);
                var payloadSize = reader.Read4Bytes();
                var payloadChecksum = reader.Read4Bytes();
                var payload = reader.ReadBytes(payloadSize.ToIntChecked()).ToImmutableArray();

                return new Message
                (
                    Magic: magic,
                    Command: command,
                    PayloadSize: payloadSize,
                    PayloadChecksum: payloadChecksum,
                    Payload: payload
                );
            }
        }

        public static void EncodeMessage(Stream stream, Message message)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(message.Magic);
                writer.WriteFixedString(12, message.Command);
                writer.Write4Bytes(message.PayloadSize);
                writer.Write4Bytes(message.PayloadChecksum);
                writer.WriteBytes(message.PayloadSize.ToIntChecked(), message.Payload.ToArray());
            }
        }

        public static byte[] EncodeMessage(Message message)
        {
            var stream = new MemoryStream();
            EncodeMessage(stream, message);
            return stream.ToArray();
        }

        public static NetworkAddress DecodeNetworkAddress(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new NetworkAddress
                (
                    Services: reader.Read8Bytes(),
                    IPv6Address: reader.ReadBytes(16).ToImmutableArray(),
                    Port: reader.Read2BytesBE()
                );
            }
        }

        public static void EncodeNetworkAddress(Stream stream, NetworkAddress networkAddress)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write8Bytes(networkAddress.Services);
                writer.WriteBytes(16, networkAddress.IPv6Address.ToArray());
                writer.Write2BytesBE(networkAddress.Port);
            }
        }

        public static byte[] EncodeNetworkAddress(NetworkAddress networkAddress)
        {
            var stream = new MemoryStream();
            EncodeNetworkAddress(stream, networkAddress);
            return stream.ToArray();
        }

        public static NetworkAddressWithTime DecodeNetworkAddressWithTime(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new NetworkAddressWithTime
                (
                    Time: reader.Read4Bytes(),
                    NetworkAddress: DecodeNetworkAddress(stream)
                );
            }
        }

        public static void EncodeNetworkAddressWithTime(Stream stream, NetworkAddressWithTime networkAddress)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(networkAddress.Time);
                EncodeNetworkAddress(stream, networkAddress.NetworkAddress);
            }
        }

        public static byte[] EncodeNetworkAddressWithTime(NetworkAddressWithTime networkAddress)
        {
            var stream = new MemoryStream();
            EncodeNetworkAddressWithTime(stream, networkAddress);
            return stream.ToArray();
        }

        public static VersionPayload DecodeVersionPayload(Stream stream, int payloadLength)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                var position = stream.Position;

                var versionPayload = new VersionPayload
                (
                    ProtocolVersion: reader.Read4Bytes(),
                    ServicesBitfield: reader.Read8Bytes(),
                    UnixTime: reader.Read8Bytes(),
                    RemoteAddress: DecodeNetworkAddress(stream),
                    LocalAddress: DecodeNetworkAddress(stream),
                    Nonce: reader.Read8Bytes(),
                    UserAgent: reader.ReadVarString(),
                    StartBlockHeight: reader.Read4Bytes(),
                    Relay: false
                );

                var readCount = stream.Position - position;
                if (versionPayload.ProtocolVersion >= VersionPayload.RELAY_VERSION && payloadLength - readCount == 1)
                    versionPayload = versionPayload.With(Relay: reader.ReadBool());

                return versionPayload;
            }
        }

        public static void EncodeVersionPayload(Stream stream, VersionPayload versionPayload, bool withRelay)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(versionPayload.ProtocolVersion);
                writer.Write8Bytes(versionPayload.ServicesBitfield);
                writer.Write8Bytes(versionPayload.UnixTime);
                EncodeNetworkAddress(stream, versionPayload.RemoteAddress);
                EncodeNetworkAddress(stream, versionPayload.LocalAddress);
                writer.Write8Bytes(versionPayload.Nonce);
                writer.WriteVarString(versionPayload.UserAgent);
                writer.Write4Bytes(versionPayload.StartBlockHeight);

                if (withRelay)
                    writer.WriteBool(versionPayload.Relay);
            }
        }

        public static byte[] EncodeVersionPayload(VersionPayload versionPayload, bool withRelay)
        {
            var stream = new MemoryStream();
            EncodeVersionPayload(stream, versionPayload, withRelay);
            return stream.ToArray();
        }
    }
}
