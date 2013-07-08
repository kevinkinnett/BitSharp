using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.WireProtocol
{
    public struct AddressPayload
    {
        public readonly ImmutableArray<NetworkAddressWithTime> NetworkAddresses;

        public AddressPayload(ImmutableArray<NetworkAddressWithTime> NetworkAddresses)
        {
            this.NetworkAddresses = NetworkAddresses;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(NetworkAddresses);
        }

        public AddressPayload With(ImmutableArray<NetworkAddressWithTime>? NetworkAddresses = null)
        {
            return new AddressPayload
            (
                NetworkAddresses ?? this.NetworkAddresses
            );
        }

        public static AddressPayload FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static AddressPayload ReadRawBytes(WireReader reader)
        {
            return new AddressPayload
            (
                NetworkAddresses: WireEncoder.ReadList(reader, NetworkAddressWithTime.ReadRawBytes)
            );
        }

        internal static byte[] ToRawBytes(ImmutableArray<NetworkAddressWithTime> NetworkAddresses)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, NetworkAddresses);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, ImmutableArray<NetworkAddressWithTime> NetworkAddresses)
        {
            writer.WriteVarInt((UInt64)NetworkAddresses.Length);
            foreach (var address in NetworkAddresses)
            {
                writer.WriteRawBytes(address.ToRawBytes());
            }
        }
    }
}
