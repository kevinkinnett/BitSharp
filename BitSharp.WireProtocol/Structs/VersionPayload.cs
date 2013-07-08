using System;
using BitSharp.Common;
using BitSharp.WireProtocol.ExtensionMethods;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Immutable;

namespace BitSharp.WireProtocol
{
    public struct VersionPayload
    {
        public static readonly UInt32 RELAY_VERSION = 70001;

        public readonly UInt32 ProtocolVersion;
        public readonly UInt64 ServicesBitfield;
        public readonly UInt64 UnixTime;
        public readonly NetworkAddress RemoteAddress;
        public readonly NetworkAddress LocalAddress;
        public readonly UInt64 Nonce;
        public readonly string UserAgent;
        public readonly UInt32 StartBlockHeight;
        public readonly bool Relay;

        public VersionPayload(UInt32 ProtocolVersion, UInt64 ServicesBitfield, UInt64 UnixTime, NetworkAddress RemoteAddress, NetworkAddress LocalAddress, UInt64 Nonce, string UserAgent, UInt32 StartBlockHeight, bool Relay)
        {
            this.ProtocolVersion = ProtocolVersion;
            this.ServicesBitfield = ServicesBitfield;
            this.UnixTime = UnixTime;
            this.RemoteAddress = RemoteAddress;
            this.LocalAddress = LocalAddress;
            this.Nonce = Nonce;
            this.UserAgent = UserAgent;
            this.StartBlockHeight = StartBlockHeight;
            this.Relay = Relay;
        }

        //TODO expensive property
        public byte[] ToRawBytes()
        {
            return ToRawBytes(ProtocolVersion, ServicesBitfield, UnixTime, RemoteAddress, LocalAddress, Nonce, UserAgent, StartBlockHeight, Relay);
        }

        public VersionPayload With(UInt32? ProtocolVersion = null, UInt64? ServicesBitfield = null, UInt64? UnixTime = null, NetworkAddress? RemoteAddress = null, NetworkAddress? LocalAddress = null, UInt64? Nonce = null, string UserAgent = null, UInt32? StartBlockHeight = null, bool? Relay = null)
        {
            return new VersionPayload
            (
                ProtocolVersion ?? this.ProtocolVersion,
                ServicesBitfield ?? this.ServicesBitfield,
                UnixTime ?? this.UnixTime,
                RemoteAddress ?? this.RemoteAddress,
                LocalAddress ?? this.LocalAddress,
                Nonce ?? this.Nonce,
                UserAgent ?? this.UserAgent,
                StartBlockHeight ?? this.StartBlockHeight,
                Relay ?? this.Relay
            );
        }

        public static VersionPayload FromRawBytes(byte[] bytes)
        {
            return ReadRawBytes(new WireReader(bytes.ToStream()));
        }

        internal static VersionPayload ReadRawBytes(WireReader reader)
        {
            var versionPayload = new VersionPayload
            (
                ProtocolVersion: reader.Read4Bytes(),
                ServicesBitfield: reader.Read8Bytes(),
                UnixTime: reader.Read8Bytes(),
                RemoteAddress: NetworkAddress.ReadRawBytes(reader),
                LocalAddress: NetworkAddress.ReadRawBytes(reader),
                Nonce: reader.Read8Bytes(),
                UserAgent: reader.ReadVarString(),
                StartBlockHeight: reader.Read4Bytes(),
                Relay: false
            );

            //TODO don't read here? this seems wrong
            //if (versionPayload.ProtocolVersion >= RELAY_VERSION)
            //    versionPayload = versionPayload.With(Relay: reader.ReadBool());

            return versionPayload;
        }

        internal static byte[] ToRawBytes(UInt32 ProtocolVersion, UInt64 ServicesBitfield, UInt64 UnixTime, NetworkAddress RemoteAddress, NetworkAddress LocalAddress, UInt64 Nonce, string UserAgent, UInt32 StartBlockHeight, bool Relay)
        {
            var stream = new MemoryStream();
            var writer = new WireWriter(stream);

            WriteRawBytes(writer, ProtocolVersion, ServicesBitfield, UnixTime, RemoteAddress, LocalAddress, Nonce, UserAgent, StartBlockHeight, Relay);

            return stream.ToArray();
        }

        internal static void WriteRawBytes(WireWriter writer, UInt32 ProtocolVersion, UInt64 ServicesBitfield, UInt64 UnixTime, NetworkAddress RemoteAddress, NetworkAddress LocalAddress, UInt64 Nonce, string UserAgent, UInt32 StartBlockHeight, bool Relay)
        {
            writer.Write4Bytes(ProtocolVersion);
            writer.Write8Bytes(ServicesBitfield);
            writer.Write8Bytes(UnixTime);
            writer.WriteRawBytes(RemoteAddress.ToRawBytes());
            writer.WriteRawBytes(LocalAddress.ToRawBytes());
            writer.Write8Bytes(Nonce);
            writer.WriteVarString(UserAgent);
            writer.Write4Bytes(StartBlockHeight);

            //TODO don't write here? this seems wrong
            //if (versionPayload.ProtocolVersion >= RELAY_VERSION)
            //    writer.WriteBool(versionPayload.Relay);
        }
    }
}
