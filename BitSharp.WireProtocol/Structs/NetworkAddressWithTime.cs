using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace BitSharp.WireProtocol
{
    public struct NetworkAddressWithTime
    {
        public readonly UInt32 Time;
        public readonly NetworkAddress NetworkAddress;

        public NetworkAddressWithTime(UInt32 Time, NetworkAddress NetworkAddress)
        {
            this.Time = Time;
            this.NetworkAddress = NetworkAddress;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Time, NetworkAddress);
        }

        public NetworkAddressWithTime With(UInt32? Time = null, NetworkAddress? NetworkAddress = null)
        {
            return new NetworkAddressWithTime
            (
                Time ?? this.Time,
                NetworkAddress ?? this.NetworkAddress
            );
        }

        public static NetworkAddressWithTime FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static NetworkAddressWithTime ReadRawBytes(WireReader reader)
        {
            return new NetworkAddressWithTime
            (
                Time: reader.Read4Bytes(),
                NetworkAddress: NetworkAddress.ReadRawBytes(reader)
            );
        }

        internal static byte[] ToRawBytes(UInt32 Time, NetworkAddress NetworkAddress)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Time, NetworkAddress);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt32 Time, NetworkAddress NetworkAddress)
        {
            writer.Write4Bytes(Time);
            writer.WriteRawBytes(NetworkAddress.ToRawBytes());
        }
    }

    public static class NetworkAddressExtensions
    {
        public static IPEndPoint ToIPEndPoint(this NetworkAddress networkAddress)
        {
            var address = new IPAddress(networkAddress.IPv6Address.ToArray());
            if (address.IsIPv4MappedToIPv6)
                address = new IPAddress(networkAddress.IPv6Address.Skip(12).ToArray());

            return new IPEndPoint(address, networkAddress.Port);
        }
    }
}
