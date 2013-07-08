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
    public struct UInt256 : IComparable<UInt256>
    {
        private const int SIZE_BYTES = 32;

        private readonly BigInteger value;
        private readonly int hashCode;

        public UInt256(byte[] value)
        {
            UInt.ConstructUInt(new BigInteger(value.Concat(0)), out this.value, out this.hashCode);
        }

        public UInt256(decimal value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt256(double value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt256(float value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt256(int value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt256(long value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt256(uint value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt256(ulong value)
        {
            UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        }

        public UInt256(UInt128 value)
        {
            UInt.ConstructUInt((BigInteger)value, out this.value, out this.hashCode);
        }

        public UInt256(BigInteger value)
        {
            UInt.ConstructUInt(value, out this.value, out this.hashCode);
        }

        public static UInt256 operator -(UInt256 left, UInt256 right)
        {
            return new UInt256(left.value - right.value);
        }

        public static UInt256 operator --(UInt256 value)
        {
            return new UInt256(value.value - 1);
        }

        public static bool operator !=(UInt256 left, UInt256 right)
        {
            return left.value != right.value;
        }

        public static bool operator !=(UInt256 left, long right)
        {
            return left.value != right;
        }

        public static bool operator !=(UInt256 left, ulong right)
        {
            return left.value != right;
        }

        public static bool operator !=(UInt256 left, BigInteger right)
        {
            return left.value != right;
        }

        public static bool operator !=(long left, UInt256 right)
        {
            return left != right.value;
        }

        public static bool operator !=(ulong left, UInt256 right)
        {
            return left != right.value;
        }

        public static bool operator !=(BigInteger left, UInt256 right)
        {
            return left != right.value;
        }

        public static UInt256 operator %(UInt256 dividend, UInt256 divisor)
        {
            return new UInt256(dividend.value % divisor.value);
        }

        public static UInt256 operator &(UInt256 left, UInt256 right)
        {
            return new UInt256(left.value & right.value);
        }

        public static UInt256 operator *(UInt256 left, UInt256 right)
        {
            return new UInt256(left.value * right.value);
        }

        public static UInt256 operator /(UInt256 dividend, UInt256 divisor)
        {
            return new UInt256(dividend.value / divisor.value);
        }

        public static UInt256 operator ^(UInt256 left, UInt256 right)
        {
            return new UInt256(left.value ^ right.value);
        }

        public static UInt256 operator |(UInt256 left, UInt256 right)
        {
            return new UInt256(left.value | right.value);
        }

        public static UInt256 operator ~(UInt256 value)
        {
            return new UInt256(~value.value);
        }

        public static UInt256 operator +(UInt256 left, UInt256 right)
        {
            return new UInt256(left.value + right.value);
        }

        public static UInt256 operator ++(UInt256 value)
        {
            return new UInt256(value.value + 1);
        }

        public static bool operator <(UInt256 left, UInt256 right)
        {
            return left.value < right.value;
        }

        public static bool operator <(UInt256 left, long right)
        {
            return left.value < right;
        }

        public static bool operator <(UInt256 left, ulong right)
        {
            return left.value < right;
        }

        public static bool operator <(UInt256 left, BigInteger right)
        {
            return left.value < right;
        }

        public static bool operator <(long left, UInt256 right)
        {
            return left < right.value;
        }

        public static bool operator <(ulong left, UInt256 right)
        {
            return left < right.value;
        }

        public static bool operator <(BigInteger left, UInt256 right)
        {
            return left < right.value;
        }

        public static UInt256 operator <<(UInt256 value, int shift)
        {
            return new UInt256(value.value << shift);
        }

        public static bool operator <=(UInt256 left, UInt256 right)
        {
            return left.value <= right.value;
        }

        public static bool operator <=(UInt256 left, long right)
        {
            return left.value <= right;
        }

        public static bool operator <=(UInt256 left, ulong right)
        {
            return left.value <= right;
        }

        public static bool operator <=(UInt256 left, BigInteger right)
        {
            return left.value <= right;
        }

        public static bool operator <=(long left, UInt256 right)
        {
            return left <= right.value;
        }

        public static bool operator <=(ulong left, UInt256 right)
        {
            return left <= right.value;
        }

        public static bool operator <=(BigInteger left, UInt256 right)
        {
            return left <= right.value;
        }

        public static bool operator ==(UInt256 left, UInt256 right)
        {
            return left.value == right.value;
        }

        public static bool operator ==(UInt256 left, long right)
        {
            return left.value == right;
        }

        public static bool operator ==(UInt256 left, ulong right)
        {
            return left.value == right;
        }

        public static bool operator ==(UInt256 left, BigInteger right)
        {
            return left.value == right;
        }

        public static bool operator ==(long left, UInt256 right)
        {
            return left == right.value;
        }

        public static bool operator ==(ulong left, UInt256 right)
        {
            return left == right.value;
        }

        public static bool operator ==(BigInteger left, UInt256 right)
        {
            return left == right.value;
        }

        public static bool operator >(UInt256 left, UInt256 right)
        {
            return left.value > right.value;
        }

        public static bool operator >(UInt256 left, long right)
        {
            return left.value > right;
        }

        public static bool operator >(UInt256 left, ulong right)
        {
            return left.value > right;
        }

        public static bool operator >(UInt256 left, BigInteger right)
        {
            return left.value > right;
        }

        public static bool operator >(long left, UInt256 right)
        {
            return left > right.value;
        }

        public static bool operator >(ulong left, UInt256 right)
        {
            return left > right.value;
        }

        public static bool operator >(BigInteger left, UInt256 right)
        {
            return left > right.value;
        }

        public static bool operator >=(UInt256 left, UInt256 right)
        {
            return left.value >= right.value;
        }

        public static bool operator >=(UInt256 left, long right)
        {
            return left.value >= right;
        }

        public static bool operator >=(UInt256 left, ulong right)
        {
            return left.value >= right;
        }

        public static bool operator >=(UInt256 left, BigInteger right)
        {
            return left.value >= right;
        }

        public static bool operator >=(long left, UInt256 right)
        {
            return left >= right.value;
        }

        public static bool operator >=(ulong left, UInt256 right)
        {
            return left >= right.value;
        }

        public static bool operator >=(BigInteger left, UInt256 right)
        {
            return left >= right.value;
        }

        public static UInt256 operator >>(UInt256 value, int shift)
        {
            return new UInt256(value.value >> shift);
        }

        public static explicit operator sbyte(UInt256 value)
        {
            return (sbyte)value.value;
        }

        public static explicit operator decimal(UInt256 value)
        {
            return (decimal)value.value;
        }

        public static explicit operator double(UInt256 value)
        {
            return (double)value.value;
        }

        public static explicit operator float(UInt256 value)
        {
            return (float)value.value;
        }

        public static explicit operator BigInteger(UInt256 value)
        {
            return value.value;
        }

        public static explicit operator UInt128(UInt256 value)
        {
            return new UInt128((BigInteger)value);
        }

        public static explicit operator ulong(UInt256 value)
        {
            return (ulong)value.value;
        }

        public static explicit operator long(UInt256 value)
        {
            return (long)value.value;
        }

        public static explicit operator uint(UInt256 value)
        {
            return (uint)value.value;
        }

        public static explicit operator int(UInt256 value)
        {
            return (int)value.value;
        }

        public static explicit operator short(UInt256 value)
        {
            return (short)value.value;
        }

        public static explicit operator ushort(UInt256 value)
        {
            return (ushort)value.value;
        }

        public static explicit operator byte(UInt256 value)
        {
            return (byte)value.value;
        }

        public static explicit operator UInt256(decimal value)
        {
            return new UInt256(value);
        }

        public static explicit operator UInt256(double value)
        {
            return new UInt256(value);
        }

        public static explicit operator UInt256(float value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(byte value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(int value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(long value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(sbyte value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(short value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(uint value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(ulong value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(UInt128 value)
        {
            return new UInt256(value);
        }

        public static implicit operator UInt256(ushort value)
        {
            return new UInt256(value);
        }

        //public static UInt256 Add(UInt256 left, UInt256 right);

        public static int Compare(UInt256 left, UInt256 right)
        {
            return BigInteger.Compare(left.value, right.value);
        }

        public int CompareTo(UInt256 other)
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

        //public static UInt256 Divide(UInt256 dividend, UInt256 divisor);

        public static UInt256 DivRem(UInt256 dividend, UInt256 divisor, out UInt256 remainder)
        {
            BigInteger remainderBigInt;
            var result = new UInt256(BigInteger.DivRem(dividend.value, divisor.value, out remainderBigInt));
            remainder = new UInt256(remainderBigInt);
            return result;
        }

        public bool Equals(UInt256 other)
        {
            return this.value.Equals(other.value);
        }

        public bool Equals(UInt128 other)
        {
            return this.value.Equals((BigInteger)other);
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

        //public static UInt256 GreatestCommonDivisor(UInt256 left, UInt256 right);

        //public static double Log(UInt256 value);

        public static double Log(UInt256 value, double baseValue)
        {
            return BigInteger.Log(value.value, baseValue);
        }

        //public static double Log10(UInt256 value);

        //public static UInt256 Max(UInt256 left, UInt256 right);

        //public static UInt256 Min(UInt256 left, UInt256 right);

        //public static UInt256 ModPow(UInt256 value, UInt256 exponent, UInt256 modulus);

        //public static UInt256 Multiply(UInt256 left, UInt256 right);

        //public static UInt256 Negate(UInt256 value);

        public static UInt256 Parse(string value)
        {
            return new UInt256(BigInteger.Parse("0" + value));
        }

        public static UInt256 Parse(string value, IFormatProvider provider)
        {
            return new UInt256(BigInteger.Parse("0" + value, provider));
        }

        public static UInt256 Parse(string value, NumberStyles style)
        {
            return new UInt256(BigInteger.Parse("0" + value, style));
        }

        public static UInt256 Parse(string value, NumberStyles style, IFormatProvider provider)
        {
            return new UInt256(BigInteger.Parse("0" + value, style, provider));
        }

        public static UInt256 Pow(UInt256 value, int exponent)
        {
            return new UInt256(BigInteger.Pow(value.value, exponent));
        }

        //public static UInt256 Remainder(UInt256 dividend, UInt256 divisor);

        //public static UInt256 Subtract(UInt256 left, UInt256 right);

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

        public static bool TryParse(string value, out UInt256 result)
        {
            BigInteger bigIntResult;
            var success = BigInteger.TryParse("0" + value, out bigIntResult);
            result = new UInt256(bigIntResult);
            return success;
        }

        public static bool TryParse(string value, NumberStyles style, IFormatProvider provider, out UInt256 result)
        {
            BigInteger bigIntResult;
            var success = BigInteger.TryParse("0" + value, style, provider, out bigIntResult);
            result = new UInt256(bigIntResult);
            return success;
        }
    }
}
