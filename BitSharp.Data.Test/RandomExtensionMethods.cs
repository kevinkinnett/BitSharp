
using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    public static class RandomExtensionMethods
    {
        public static UInt32 NextUInt32(this Random random)
        {
            // purposefully left unchecked to get full range of UInt32
            return (UInt32)random.Next();
        }

        public static UInt64 NextUInt64(this Random random)
        {
            return (random.NextUInt32() << 32) + random.NextUInt64();
        }

        public static UInt256 NextUInt256(this Random random)
        {
            return new UInt256(
                (new BigInteger(random.NextUInt32()) << 96) +
                (new BigInteger(random.NextUInt32()) << 64) +
                (new BigInteger(random.NextUInt32()) << 32) +
                new BigInteger(random.NextUInt32()));
        }
    }
}
