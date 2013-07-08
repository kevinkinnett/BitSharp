
using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.WireProtocol.ExtensionMethods
{
    public static class ExtensionMethods
    {
        public static ByteArrayStream ToStream(this byte[] bytes)
        {
            return new ByteArrayStream(bytes);
        }

        public static ByteArrayStream ToStream(this ImmutableArray<byte> bytes)
        {
            return new ByteArrayStream(bytes.ToArray());
        }
    }
}
