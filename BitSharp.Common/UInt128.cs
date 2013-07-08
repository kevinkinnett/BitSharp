using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Collections.Immutable;

namespace BitSharp.Common
{
    public struct UInt128
    {
        private const int SIZE_BYTES = 16;

        private readonly BigInteger value;
        private readonly int hashCode;

        public UInt128(byte[] value)
        {
            UInt.ConstructUInt(new BigInteger(value.Concat(0)), out this.value, out this.hashCode);
        }

        public UInt128(decimal value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt128(double value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt128(float value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt128(int value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt128(long value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt128(uint value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt128(ulong value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt128(BigInteger value)
        {
            UInt.ConstructUInt(value, out this.value, out this.hashCode);
        }

        public static UInt128 operator -(UInt128 left, UInt128 right)
        {
            return new UInt128(left.value - right.value);
        }

        public static UInt128 operator --(UInt128 value)
        {
            return new UInt128(value.value - 1);
        }

        public static bool operator !=(UInt128 left, UInt128 right)
        {
            return left.value != right.value;
        }

        public static bool operator !=(UInt128 left, long right)
        {
            return left.value != right;
        }

        public static bool operator !=(UInt128 left, ulong right)
        {
            return left.value != right;
        }

        public static bool operator !=(UInt128 left, BigInteger right)
        {
            return left.value != right;
        }

        public static bool operator !=(long left, UInt128 right)
        {
            return left != right.value;
        }

        public static bool operator !=(ulong left, UInt128 right)
        {
            return left != right.value;
        }

        public static bool operator !=(BigInteger left, UInt128 right)
        {
            return left != right.value;
        }

        public static UInt128 operator %(UInt128 dividend, UInt128 divisor)
        {
            return new UInt128(dividend.value % divisor.value);
        }

        public static UInt128 operator &(UInt128 left, UInt128 right)
        {
            return new UInt128(left.value & right.value);
        }

        public static UInt128 operator *(UInt128 left, UInt128 right)
        {
            return new UInt128(left.value * right.value);
        }

        public static UInt128 operator /(UInt128 dividend, UInt128 divisor)
        {
            return new UInt128(dividend.value / divisor.value);
        }

        public static UInt128 operator ^(UInt128 left, UInt128 right)
        {
            return new UInt128(left.value ^ right.value);
        }

        public static UInt128 operator |(UInt128 left, UInt128 right)
        {
            return new UInt128(left.value | right.value);
        }

        public static UInt128 operator ~(UInt128 value)
        {
            return new UInt128(~value.value);
        }

        public static UInt128 operator +(UInt128 left, UInt128 right)
        {
            return new UInt128(left.value + right.value);
        }

        public static UInt128 operator ++(UInt128 value)
        {
            return new UInt128(value.value + 1);
        }

        public static bool operator <(UInt128 left, UInt128 right)
        {
            return left.value < right.value;
        }

        public static bool operator <(UInt128 left, long right)
        {
            return left.value < right;
        }

        public static bool operator <(UInt128 left, ulong right)
        {
            return left.value < right;
        }

        public static bool operator <(UInt128 left, BigInteger right)
        {
            return left.value < right;
        }

        public static bool operator <(long left, UInt128 right)
        {
            return left < right.value;
        }

        public static bool operator <(ulong left, UInt128 right)
        {
            return left < right.value;
        }

        public static bool operator <(BigInteger left, UInt128 right)
        {
            return left < right.value;
        }

        public static UInt128 operator <<(UInt128 value, int shift)
        {
            return new UInt128(value.value << shift);
        }

        public static bool operator <=(UInt128 left, UInt128 right)
        {
            return left.value <= right.value;
        }

        public static bool operator <=(UInt128 left, long right)
        {
            return left.value <= right;
        }

        public static bool operator <=(UInt128 left, ulong right)
        {
            return left.value <= right;
        }

        public static bool operator <=(UInt128 left, BigInteger right)
        {
            return left.value <= right;
        }

        public static bool operator <=(long left, UInt128 right)
        {
            return left <= right.value;
        }

        public static bool operator <=(ulong left, UInt128 right)
        {
            return left <= right.value;
        }

        public static bool operator <=(BigInteger left, UInt128 right)
        {
            return left <= right.value;
        }

        public static bool operator ==(UInt128 left, UInt128 right)
        {
            return left.value == right.value;
        }

        public static bool operator ==(UInt128 left, long right)
        {
            return left.value == right;
        }

        public static bool operator ==(UInt128 left, ulong right)
        {
            return left.value == right;
        }

        public static bool operator ==(UInt128 left, BigInteger right)
        {
            return left.value == right;
        }

        public static bool operator ==(long left, UInt128 right)
        {
            return left == right.value;
        }

        public static bool operator ==(ulong left, UInt128 right)
        {
            return left == right.value;
        }

        public static bool operator ==(BigInteger left, UInt128 right)
        {
            return left == right.value;
        }

        public static bool operator >(UInt128 left, UInt128 right)
        {
            return left.value > right.value;
        }

        public static bool operator >(UInt128 left, long right)
        {
            return left.value > right;
        }

        public static bool operator >(UInt128 left, ulong right)
        {
            return left.value > right;
        }

        public static bool operator >(UInt128 left, BigInteger right)
        {
            return left.value > right;
        }

        public static bool operator >(long left, UInt128 right)
        {
            return left > right.value;
        }

        public static bool operator >(ulong left, UInt128 right)
        {
            return left > right.value;
        }

        public static bool operator >(BigInteger left, UInt128 right)
        {
            return left > right.value;
        }

        public static bool operator >=(UInt128 left, UInt128 right)
        {
            return left.value >= right.value;
        }

        public static bool operator >=(UInt128 left, long right)
        {
            return left.value >= right;
        }

        public static bool operator >=(UInt128 left, ulong right)
        {
            return left.value >= right;
        }

        public static bool operator >=(UInt128 left, BigInteger right)
        {
            return left.value >= right;
        }

        public static bool operator >=(long left, UInt128 right)
        {
            return left >= right.value;
        }

        public static bool operator >=(ulong left, UInt128 right)
        {
            return left >= right.value;
        }

        public static bool operator >=(BigInteger left, UInt128 right)
        {
            return left >= right.value;
        }

        public static UInt128 operator >>(UInt128 value, int shift)
        {
            return new UInt128(value.value >> shift);
        }

        public static explicit operator sbyte(UInt128 value)
        {
            return (sbyte)value.value;
        }

        public static explicit operator decimal(UInt128 value)
        {
            return (decimal)value.value;
        }

        public static explicit operator double(UInt128 value)
        {
            return (double)value.value;
        }

        public static explicit operator float(UInt128 value)
        {
            return (float)value.value;
        }

        public static explicit operator BigInteger(UInt128 value)
        {
            return value.value;
        }

        public static explicit operator ulong(UInt128 value)
        {
            return (ulong)value.value;
        }

        public static explicit operator long(UInt128 value)
        {
            return (long)value.value;
        }

        public static explicit operator uint(UInt128 value)
        {
            return (uint)value.value;
        }

        public static explicit operator int(UInt128 value)
        {
            return (int)value.value;
        }

        public static explicit operator short(UInt128 value)
        {
            return (short)value.value;
        }

        public static explicit operator ushort(UInt128 value)
        {
            return (ushort)value.value;
        }

        public static explicit operator byte(UInt128 value)
        {
            return (byte)value.value;
        }

        public static explicit operator UInt128(decimal value)
        {
            return new UInt128(value);
        }

        public static explicit operator UInt128(double value)
        {
            return new UInt128(value);
        }

        public static explicit operator UInt128(float value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(byte value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(int value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(long value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(sbyte value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(short value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(uint value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(ulong value)
        {
            return new UInt128(value);
        }

        public static implicit operator UInt128(ushort value)
        {
            return new UInt128(value);
        }

        //public static UInt128 Add(UInt128 left, UInt128 right);

        public static int Compare(UInt128 left, UInt128 right)
        {
            return BigInteger.Compare(left.value, right.value);
        }

        public int CompareTo(UInt128 other)
        {
            return this.value.CompareTo(other.value);
        }

        public int CompareTo(long other)
        {
            return this.value.CompareTo(other);
        }

        public int CompareTo(object obj)
        {
            return this.value.CompareTo(obj);
        }

        public int CompareTo(ulong other)
        {
            return this.value.CompareTo(other);
        }

        public int CompareTo(BigInteger other)
        {
            return this.value.CompareTo(other);
        }

        //public static UInt128 Divide(UInt128 dividend, UInt128 divisor);

        public static UInt128 DivRem(UInt128 dividend, UInt128 divisor, out UInt128 remainder)
        {
            BigInteger remainderBigInt;
            var result = new UInt128(BigInteger.DivRem(dividend.value, divisor.value, out remainderBigInt));
            remainder = new UInt128(remainderBigInt);
            return result;
        }

        public bool Equals(UInt256 other)
        {
            return this.value.Equals((BigInteger)other);
        }

        public bool Equals(UInt128 other)
        {
            return this.value.Equals(other.value);
        }

        public bool Equals(long other)
        {
            return this.value.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (obj is UInt128)
                return this.Equals((UInt128)obj);
            else if (obj is UInt256)
                return this.Equals((UInt256)obj);
            else
                return this.value.Equals(obj);
        }

        public bool Equals(ulong other)
        {
            return this.value.Equals(other);
        }

        public bool Equals(BigInteger other)
        {
            return this.value.Equals(other);
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        //public static UInt128 GreatestCommonDivisor(UInt128 left, UInt128 right);

        //public static double Log(UInt128 value);

        public static double Log(UInt128 value, double baseValue)
        {
            return BigInteger.Log(value.value, baseValue);
        }

        //public static double Log10(UInt128 value);

        //public static UInt128 Max(UInt128 left, UInt128 right);

        //public static UInt128 Min(UInt128 left, UInt128 right);

        //public static UInt128 ModPow(UInt128 value, UInt128 exponent, UInt128 modulus);

        //public static UInt128 Multiply(UInt128 left, UInt128 right);

        //public static UInt128 Negate(UInt128 value);

        public static UInt128 Parse(string value)
        {
            return new UInt128(BigInteger.Parse("0" + value));
        }

        public static UInt128 Parse(string value, IFormatProvider provider)
        {
            return new UInt128(BigInteger.Parse("0" + value, provider));
        }

        public static UInt128 Parse(string value, NumberStyles style)
        {
            return new UInt128(BigInteger.Parse("0" + value, style));
        }

        public static UInt128 Parse(string value, NumberStyles style, IFormatProvider provider)
        {
            return new UInt128(BigInteger.Parse("0" + value, style, provider));
        }

        public static UInt128 Pow(UInt128 value, int exponent)
        {
            return new UInt128(BigInteger.Pow(value.value, exponent));
        }

        //public static UInt128 Remainder(UInt128 dividend, UInt128 divisor);

        //public static UInt128 Subtract(UInt128 left, UInt128 right);

        public byte[] ToByteArray()
        {
            return UInt.ToByteArray(this.value, SIZE_BYTES);
        }

        public override string ToString()
        {
            return this.value.ToString();
        }

        public string ToString(IFormatProvider provider)
        {
            return this.value.ToString(provider);
        }

        public string ToString(string format)
        {
            return this.value.ToString(format);
        }

        public string ToString(string format, IFormatProvider provider)
        {
            return this.value.ToString(format, provider);
        }

        public static bool TryParse(string value, out UInt128 result)
        {
            BigInteger bigIntResult;
            var success = BigInteger.TryParse("0" + value, out bigIntResult);
            result = new UInt128(bigIntResult);
            return success;
        }

        public static bool TryParse(string value, NumberStyles style, IFormatProvider provider, out UInt128 result)
        {
            BigInteger bigIntResult;
            var success = BigInteger.TryParse("0" + value, style, provider, out bigIntResult);
            result = new UInt128(bigIntResult);
            return success;
        }
    }
}
