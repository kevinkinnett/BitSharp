using BitSharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node.ExtensionMethods
{
    public static class ExtensionMethods
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
