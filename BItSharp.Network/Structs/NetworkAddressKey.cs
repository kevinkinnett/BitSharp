using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Network
{
    public struct NetworkAddressKey
    {
        public readonly ImmutableArray<byte> IPv6Address;
        public readonly UInt16 Port;
        private readonly int _hashCode;

        public NetworkAddressKey(ImmutableArray<byte> IPv6Address, UInt16 Port)
        {
            this.IPv6Address = IPv6Address;
            this.Port = Port;

            this._hashCode = Port.GetHashCode() ^ new BigInteger(IPv6Address.ToArray()).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is NetworkAddressKey))
                return false;

            var other = (NetworkAddressKey)obj;
            return other.IPv6Address.SequenceEqual(this.IPv6Address) && other.Port == this.Port;
        }

        public override int GetHashCode()
        {
            return this._hashCode;
        }
    }

}
