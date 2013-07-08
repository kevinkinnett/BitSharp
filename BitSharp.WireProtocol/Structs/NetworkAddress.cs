using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace BitSharp.WireProtocol
{
    public struct NetworkAddress
    {
        public readonly UInt64 Services;
        public readonly ImmutableArray<byte> IPv6Address;
        public readonly UInt16 Port;

        public NetworkAddress(UInt64 Services, ImmutableArray<byte> IPv6Address, UInt16 Port)
        {
            this.Services = Services;
            this.IPv6Address = IPv6Address;
            this.Port = Port;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(Services, IPv6Address, Port);
        }

        public NetworkAddress With(UInt64? Services = null, ImmutableArray<byte>? IPv6Address = null, UInt16? Port = null)
        {
            return new NetworkAddress
            (
                Services ?? this.Services,
                IPv6Address ?? this.IPv6Address,
                Port ?? this.Port
            );
        }

        public static NetworkAddress FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static NetworkAddress ReadRawBytes(WireReader reader)
        {
            return new NetworkAddress
            (
                Services: reader.Read8Bytes(),
                IPv6Address: reader.ReadRawBytes(16).ToImmutableArray(),
                Port: reader.Read2BytesBE()
            );
        }

        internal static byte[] ToRawBytes(UInt64 Services, ImmutableArray<byte> IPv6Address, UInt16 Port)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, Services, IPv6Address, Port);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt64 Services, ImmutableArray<byte> IPv6Address, UInt16 Port)
        {
            writer.Write8Bytes(Services);
            writer.WriteRawBytes(16, IPv6Address.ToArray());
            writer.Write2BytesBE(Port);
        }
    }
}
