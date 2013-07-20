using BitSharp.Common;
using BitSharp.Network.ExtensionMethods;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace BitSharp.Network
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

        public NetworkAddressWithTime With(UInt32? Time = null, NetworkAddress? NetworkAddress = null)
        {
            return new NetworkAddressWithTime
            (
                Time ?? this.Time,
                NetworkAddress ?? this.NetworkAddress
            );
        }
    }
}
