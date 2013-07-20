using System;
using BitSharp.Common;
using BitSharp.Network.ExtensionMethods;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Immutable;

namespace BitSharp.Network
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
    }
}
