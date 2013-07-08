using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class Bits
    {
        private static readonly bool isLE = BitConverter.IsLittleEndian;

        public static byte[] GetBytes(UInt16 value)
        {
            return Order(BitConverter.GetBytes(value));
        }

        public static byte[] GetBytesBE(UInt16 value)
        {
            return OrderBE(BitConverter.GetBytes(value));
        }

        public static byte[] GetBytes(UInt32 value)
        {
            return Order(BitConverter.GetBytes(value));
        }

        public static byte[] GetBytesBE(UInt32 value)
        {
            return OrderBE(BitConverter.GetBytes(value));
        }

        public static byte[] GetBytes(UInt64 value)
        {
            return Order(BitConverter.GetBytes(value));
        }

        public static byte[] GetBytesBE(UInt64 value)
        {
            return OrderBE(BitConverter.GetBytes(value));
        }

        public static byte[] GetBytes(UInt256 value)
        {
            return value.ToByteArray();
        }

        public static string ToString(byte[] value)
        {
            return BitConverter.ToString(Order(value));
        }

        public static UInt16 ToUInt16(byte[] value)
        {
            return BitConverter.ToUInt16(Order(value), startIndex: 0);
        }

        public static UInt16 ToUInt16BE(byte[] value)
        {
            return BitConverter.ToUInt16(OrderBE(value), startIndex: 0);
        }

        public static UInt32 ToUInt32(byte[] value)
        {
            return BitConverter.ToUInt32(Order(value), startIndex: 0);
        }

        public static UInt64 ToUInt64(byte[] value)
        {
            return BitConverter.ToUInt64(Order(value), startIndex: 0);
        }

        public static UInt64 ToUInt64(byte[] value, int startIndex)
        {
            return BitConverter.ToUInt64(Order(value), startIndex);
        }

        public static UInt256 ToUInt256(byte[] value)
        {
            return new UInt256(value);
        }

        public static byte[] Order(byte[] value)
        {
            return isLE ? value : value.Reverse().ToArray();
        }

        public static byte[] OrderBE(byte[] value)
        {
            return isLE ? value.Reverse().ToArray() : value;
        }
    }
}
