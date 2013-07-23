using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common;
using System.Collections.Immutable;
using System.IO;

namespace BitSharp.Network
{
    public static class Messaging
    {
        public static readonly UInt32 MAGIC_TESTNET3 = 0x0709110B;
        public static readonly UInt32 MAGIC_MAIN = 0xD9B4BEF9;
        public static readonly UInt64 SERVICES_BITFIELD = 1; // 1 (NODE_NETWORK services)
        public static readonly UInt32 PROTOCOL_VERSION = 70002;
        public static readonly string USER_AGENT = "/BitSharp:0.0.0/";

        //TODO
        public static int Port { get; set; }
        public static UInt32 Magic { get; set; }

        private static readonly Uri externalIPServiceUri = new Uri("http://icanhazip.com/");
        private static IPAddress externalIPAddress;
        private static DateTime externalIPAddressTime;
        
        public static IPAddress GetExternalIPAddress()
        {
            if (externalIPAddress == null || (DateTime.UtcNow - externalIPAddressTime).TotalHours >= 1)
            {
                using (var webClient = new WebClient())
                {
                    var ipString = webClient.DownloadString(externalIPServiceUri).Replace("\n", "");

                    if (IPAddress.TryParse(ipString, out externalIPAddress))
                    {
                        externalIPAddressTime = DateTime.UtcNow;
                    }
                }
            }

            return externalIPAddress;
        }

        public static IPEndPoint GetExternalIPEndPoint()
        {
            return new IPEndPoint(GetExternalIPAddress(), Port);
        }

        public static Message ConstructMessage(string command, byte[] payload)
        {
            var message = new Message
            (
                Magic: Messaging.Magic,
                Command: command,
                PayloadSize: (UInt32)payload.Length,
                PayloadChecksum: CalculatePayloadChecksum(payload),
                Payload: payload.ToImmutableArray()
            );

            return message;
        }

        public static UInt32 CalculatePayloadChecksum(byte[] payload)
        {
            return Bits.ToUInt32(Crypto.DoubleSHA256(payload).Take(4).ToArray());
        }

        public static bool VerifyPayloadChecksum(UInt32 checksum, byte[] payload)
        {
            return checksum == CalculatePayloadChecksum(payload);
        }

        public static IPAddress BytesToIPAddress(byte[] bytes)
        {
            var ipAddress = new IPAddress(bytes);
            if (ipAddress.IsIPv4MappedToIPv6)
                ipAddress = new IPAddress(bytes.Skip(12).ToArray());

            return ipAddress;
        }

        public static byte[] IPAddressToBytes(IPAddress ipAddress)
        {
            if (!ipAddress.IsIPv4MappedToIPv6)
                ipAddress=ipAddress.MapToIPv6();

            return ipAddress.GetAddressBytes();
        }

        public static NetworkAddress ConstructNetworkAddress(IPAddress ip, int port)
        {
            return new NetworkAddress
            (
                Services: 1, // 1 (NODE_NETWORK services)
                // if address is IPv4, map it onto IPv6
                IPv6Address: IPAddressToBytes(ip).ToImmutableArray(),
                Port: (UInt16)port
            );
        }

        public static NetworkAddressWithTime ConstructNetworkAddressWithTime(DateTime time, IPAddress ip, int port)
        {
            return new NetworkAddressWithTime
            (
                Time: time.ToUnixTime(), //TODO is time LE or BE on network messages?
                NetworkAddress: ConstructNetworkAddress(ip, port)
            );
        }

        public static VersionPayload ConstructVersionPayload(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, UInt64 nodeId, UInt32 startBlockHeight)
        {
            return new VersionPayload
            (
                ProtocolVersion: PROTOCOL_VERSION,
                ServicesBitfield: SERVICES_BITFIELD,
                UnixTime: DateTime.UtcNow.ToUnixTime(), //TODO
                RemoteAddress: ConstructNetworkAddress(remoteEndPoint.Address, remoteEndPoint.Port),
                LocalAddress: ConstructNetworkAddress(localEndPoint.Address, port: localEndPoint.Port),
                Nonce: nodeId,
                UserAgent: USER_AGENT,
                StartBlockHeight: startBlockHeight,
                Relay: false //TODO
            );
        }

        public static InventoryPayload ConstructInventoryPayload(ImmutableArray<InventoryVector> invVectors)
        {
            return new InventoryPayload
            (
                InventoryVectors: invVectors
            );
        }

        public static GetBlocksPayload ConstructGetBlocksPayload(ImmutableArray<UInt256> blockLocatorHashes, UInt256 hashStop)
        {
            return new GetBlocksPayload
            (
                Version: PROTOCOL_VERSION,
                BlockLocatorHashes: blockLocatorHashes,
                HashStop: hashStop
            );
        }
    }
}
