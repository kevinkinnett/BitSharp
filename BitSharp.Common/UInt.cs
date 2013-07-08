using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.Globalization;
using System.Numerics;
using System.Collections.Immutable;

namespace BitSharp.Common
{
    internal static class UInt
    {
        internal static void ConstructUInt(BigInteger setValue, out BigInteger value, out int hashCode)
        {
            if (setValue < 0)
                setValue = new BigInteger(setValue.ToByteArray().Concat(0));
            Debug.Assert(setValue >= 0);
            value = setValue;
            hashCode = value.GetHashCode();
        }

        internal static byte[] ToByteArray(BigInteger value, int byteSize)
        {
            var bytes = value.ToByteArray();

            // check that value isn't too large
            if (bytes.Length > byteSize)
            {
                // one extra byte is allowed to make the value positive but it must be zero
                if (bytes.Length == byteSize + 1 && bytes[bytes.Length - 1] == 0)
                {
                    var newBytes = new byte[byteSize];
                    Buffer.BlockCopy(bytes, 0, newBytes, 0, byteSize);
                    bytes = newBytes;
                }
                // value is too large
                else
                {
                    throw new ArgumentOutOfRangeException("value");
                }
            }
            // if the value is smaller than allowed, zero pad it
            else if (bytes.Length < byteSize)
            {
                bytes = bytes.Concat(new byte[byteSize - bytes.Length]); // zero pad at the start of the array where the most significant bytes are
            }

            Debug.Assert(bytes.Length == byteSize);

            return bytes;
        }
    }
}
