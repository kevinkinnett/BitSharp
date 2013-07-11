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
        private static readonly UInt256 _zero = new UInt256(new byte[0]);

        private const int SIZE_BYTES = 32;

        // parts are big-endian
        private readonly UInt64 part1;
        private readonly UInt64 part2;
        private readonly UInt64 part3;
        private readonly UInt64 part4;
        private readonly int hashCode;
        private readonly bool notDefault;

        public UInt256(byte[] value)
        {
            if (value.Length > 32 && !(value.Length == 33 && value[32] == 0))
                throw new ArgumentOutOfRangeException();

            if (value.Length < 32)
                value = value.Concat(new byte[32 - value.Length]);

            // read LE parts in reverse order to store in BE
            var part1Bytes = new byte[8];
            var part2Bytes = new byte[8];
            var part3Bytes = new byte[8];
            var part4Bytes = new byte[8];
            Buffer.BlockCopy(value, 0, part4Bytes, 0, 8);
            Buffer.BlockCopy(value, 8, part3Bytes, 0, 8);
            Buffer.BlockCopy(value, 16, part2Bytes, 0, 8);
            Buffer.BlockCopy(value, 24, part1Bytes, 0, 8);

            // convert parts and store
            this.part1 = Bits.ToUInt64(part1Bytes);
            this.part2 = Bits.ToUInt64(part2Bytes);
            this.part3 = Bits.ToUInt64(part3Bytes);
            this.part4 = Bits.ToUInt64(part4Bytes);

            this.hashCode = this.part1.GetHashCode() ^ this.part2.GetHashCode() ^ this.part3.GetHashCode() ^ this.part4.GetHashCode();

            this.notDefault = true;
        }

        public UInt256(int value)
            : this(Bits.GetBytes(value))
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
        }

        public UInt256(long value)
            : this(Bits.GetBytes(value))
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
        }

        public UInt256(uint value)
            : this(Bits.GetBytes(value))
        { }

        public UInt256(ulong value)
            : this(Bits.GetBytes(value))
        { }

        public UInt256(BigInteger value)
            : this(value.ToByteArray())
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
        }

        public bool IsDefault { get { return !this.notDefault; } }

        public byte[] ToByteArray()
        {
            var buffer = new byte[32];
            Buffer.BlockCopy(Bits.GetBytes(this.part4), 0, buffer, 0, 8);
            Buffer.BlockCopy(Bits.GetBytes(this.part3), 0, buffer, 8, 8);
            Buffer.BlockCopy(Bits.GetBytes(this.part2), 0, buffer, 16, 8);
            Buffer.BlockCopy(Bits.GetBytes(this.part1), 0, buffer, 24, 8);

            return buffer;
        }

        public BigInteger ToBigInteger()
        {
            // add a trailing zero so that value is always positive
            return new BigInteger(ToByteArray().Concat(0));
        }

        public int CompareTo(UInt256 other)
        {
            if (this == other)
                return 0;
            else if (this < other)
                return -1;
            else if (this > other)
                return +1;

            throw new Exception();
        }

        public static UInt256 Zero
        {
            get { return _zero; }
        }

        public static explicit operator BigInteger(UInt256 value)
        {
            return value.ToBigInteger();
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

        public static implicit operator UInt256(ushort value)
        {
            return new UInt256(value);
        }

        public static bool operator !=(UInt256 left, UInt256 right)
        {
            return left.part1 != right.part1 || left.part2 != right.part2 || left.part3 != right.part3 || left.part4 != right.part4;
        }

        public static bool operator <(UInt256 left, UInt256 right)
        {
            if (left.part1 < right.part1)
                return true;
            else if (left.part1 == right.part1 && left.part2 < right.part2)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 < right.part3)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 == right.part3 && left.part4 < right.part4)
                return true;

            return false;
        }

        public static bool operator <=(UInt256 left, UInt256 right)
        {
            if (left.part1 < right.part1)
                return true;
            else if (left.part1 == right.part1 && left.part2 < right.part2)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 < right.part3)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 == right.part3 && left.part4 < right.part4)
                return true;

            return left == right;
        }

        public static bool operator ==(UInt256 left, UInt256 right)
        {
            return left.part1 == right.part1 || left.part2 == right.part2 || left.part3 == right.part3 || left.part4 == right.part4;
        }

        public static bool operator >(UInt256 left, UInt256 right)
        {
            if (left.part1 > right.part1)
                return true;
            else if (left.part1 == right.part1 && left.part2 > right.part2)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 > right.part3)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 == right.part3 && left.part4 > right.part4)
                return true;

            return false;
        }

        public static bool operator >=(UInt256 left, UInt256 right)
        {
            if (left.part1 > right.part1)
                return true;
            else if (left.part1 == right.part1 && left.part2 > right.part2)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 > right.part3)
                return true;
            else if (left.part1 == right.part1 && left.part2 == right.part2 && left.part3 == right.part3 && left.part4 > right.part4)
                return true;

            return left == right;
        }

        // TODO doesn't compare against other numerics
        public override bool Equals(object obj)
        {
            if (!(obj is UInt256))
                return false;

            var other = (UInt256)obj;
            return other.part1 == this.part1 && other.part2 == this.part2 && other.part3 == this.part3 && other.part4 == this.part4;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static UInt256 Parse(string value)
        {
            return new UInt256(BigInteger.Parse("0" + value).ToByteArray());
        }

        public static UInt256 Parse(string value, IFormatProvider provider)
        {
            return new UInt256(BigInteger.Parse("0" + value, provider).ToByteArray());
        }

        public static UInt256 Parse(string value, NumberStyles style)
        {
            return new UInt256(BigInteger.Parse("0" + value, style).ToByteArray());
        }

        public static UInt256 Parse(string value, NumberStyles style, IFormatProvider provider)
        {
            return new UInt256(BigInteger.Parse("0" + value, style, provider).ToByteArray());
        }

        public static double Log(UInt256 value, double baseValue)
        {
            return BigInteger.Log(value.ToBigInteger(), baseValue);
        }

        public static UInt256 operator %(UInt256 dividend, UInt256 divisor)
        {
            return new UInt256(dividend.ToBigInteger() % divisor.ToBigInteger());
        }

        public static UInt256 Pow(UInt256 value, int exponent)
        {
            return new UInt256(BigInteger.Pow(value.ToBigInteger(), exponent));
        }

        public static UInt256 operator *(UInt256 left, UInt256 right)
        {
            return new UInt256(left.ToBigInteger() * right.ToBigInteger());
        }

        public static UInt256 operator >>(UInt256 value, int shift)
        {
            return new UInt256(value.ToBigInteger() >> shift);
        }

        public static UInt256 operator /(UInt256 dividend, UInt256 divisor)
        {
            return new UInt256(dividend.ToBigInteger() / divisor.ToBigInteger());
        }

        public static UInt256 DivRem(UInt256 dividend, UInt256 divisor, out UInt256 remainder)
        {
            BigInteger remainderBigInt;
            var result = new UInt256(BigInteger.DivRem(dividend.ToBigInteger(), divisor.ToBigInteger(), out remainderBigInt));
            remainder = new UInt256(remainderBigInt);
            return result;
        }

        public static explicit operator double(UInt256 value)
        {
            return (double)value.ToBigInteger();
        }

        //public UInt256(byte[] value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value.Concat(0)), out this.value, out this.hashCode);
        //}

        //public UInt256(decimal value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        //}

        //public UInt256(double value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        //}

        //public UInt256(float value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        //}

        //public UInt256(int value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        //}

        //public UInt256(long value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        //}

        //public UInt256(uint value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        //}

        //public UInt256(ulong value)
        //{
        //    UInt.ConstructUInt(new BigInteger(value), out this.value, out this.hashCode);
        //}

        //public UInt256(UInt128 value)
        //{
        //    UInt.ConstructUInt((BigInteger)value, out this.value, out this.hashCode);
        //}

        //public UInt256(BigInteger value)
        //{
        //    UInt.ConstructUInt(value, out this.value, out this.hashCode);
        //}

        //public static UInt256 operator -(UInt256 left, UInt256 right)
        //{
        //    return new UInt256(left.value - right.value);
        //}

        //public static UInt256 operator --(UInt256 value)
        //{
        //    return new UInt256(value.value - 1);
        //}

        //public static bool operator !=(UInt256 left, UInt256 right)
        //{
        //    return left.value != right.value;
        //}

        //public static bool operator !=(UInt256 left, long right)
        //{
        //    return left.value != right;
        //}

        //public static bool operator !=(UInt256 left, ulong right)
        //{
        //    return left.value != right;
        //}

        //public static bool operator !=(UInt256 left, BigInteger right)
        //{
        //    return left.value != right;
        //}

        //public static bool operator !=(long left, UInt256 right)
        //{
        //    return left != right.value;
        //}

        //public static bool operator !=(ulong left, UInt256 right)
        //{
        //    return left != right.value;
        //}

        //public static bool operator !=(BigInteger left, UInt256 right)
        //{
        //    return left != right.value;
        //}

        //public static UInt256 operator %(UInt256 dividend, UInt256 divisor)
        //{
        //    return new UInt256(dividend.value % divisor.value);
        //}

        //public static UInt256 operator &(UInt256 left, UInt256 right)
        //{
        //    return new UInt256(left.value & right.value);
        //}

        //public static UInt256 operator *(UInt256 left, UInt256 right)
        //{
        //    return new UInt256(left.value * right.value);
        //}

        //public static UInt256 operator /(UInt256 dividend, UInt256 divisor)
        //{
        //    return new UInt256(dividend.value / divisor.value);
        //}

        //public static UInt256 operator ^(UInt256 left, UInt256 right)
        //{
        //    return new UInt256(left.value ^ right.value);
        //}

        //public static UInt256 operator |(UInt256 left, UInt256 right)
        //{
        //    return new UInt256(left.value | right.value);
        //}

        //public static UInt256 operator ~(UInt256 value)
        //{
        //    return new UInt256(~value.value);
        //}

        //public static UInt256 operator +(UInt256 left, UInt256 right)
        //{
        //    return new UInt256(left.value + right.value);
        //}

        //public static UInt256 operator ++(UInt256 value)
        //{
        //    return new UInt256(value.value + 1);
        //}

        //public static bool operator <(UInt256 left, UInt256 right)
        //{
        //    return left.value < right.value;
        //}

        //public static bool operator <(UInt256 left, long right)
        //{
        //    return left.value < right;
        //}

        //public static bool operator <(UInt256 left, ulong right)
        //{
        //    return left.value < right;
        //}

        //public static bool operator <(UInt256 left, BigInteger right)
        //{
        //    return left.value < right;
        //}

        //public static bool operator <(long left, UInt256 right)
        //{
        //    return left < right.value;
        //}

        //public static bool operator <(ulong left, UInt256 right)
        //{
        //    return left < right.value;
        //}

        //public static bool operator <(BigInteger left, UInt256 right)
        //{
        //    return left < right.value;
        //}

        //public static UInt256 operator <<(UInt256 value, int shift)
        //{
        //    return new UInt256(value.value << shift);
        //}

        //public static bool operator <=(UInt256 left, UInt256 right)
        //{
        //    return left.value <= right.value;
        //}

        //public static bool operator <=(UInt256 left, long right)
        //{
        //    return left.value <= right;
        //}

        //public static bool operator <=(UInt256 left, ulong right)
        //{
        //    return left.value <= right;
        //}

        //public static bool operator <=(UInt256 left, BigInteger right)
        //{
        //    return left.value <= right;
        //}

        //public static bool operator <=(long left, UInt256 right)
        //{
        //    return left <= right.value;
        //}

        //public static bool operator <=(ulong left, UInt256 right)
        //{
        //    return left <= right.value;
        //}

        //public static bool operator <=(BigInteger left, UInt256 right)
        //{
        //    return left <= right.value;
        //}

        //public static bool operator ==(UInt256 left, UInt256 right)
        //{
        //    return left.value == right.value;
        //}

        //public static bool operator ==(UInt256 left, long right)
        //{
        //    return left.value == right;
        //}

        //public static bool operator ==(UInt256 left, ulong right)
        //{
        //    return left.value == right;
        //}

        //public static bool operator ==(UInt256 left, BigInteger right)
        //{
        //    return left.value == right;
        //}

        //public static bool operator ==(long left, UInt256 right)
        //{
        //    return left == right.value;
        //}

        //public static bool operator ==(ulong left, UInt256 right)
        //{
        //    return left == right.value;
        //}

        //public static bool operator ==(BigInteger left, UInt256 right)
        //{
        //    return left == right.value;
        //}

        //public static bool operator >(UInt256 left, UInt256 right)
        //{
        //    return left.value > right.value;
        //}

        //public static bool operator >(UInt256 left, long right)
        //{
        //    return left.value > right;
        //}

        //public static bool operator >(UInt256 left, ulong right)
        //{
        //    return left.value > right;
        //}

        //public static bool operator >(UInt256 left, BigInteger right)
        //{
        //    return left.value > right;
        //}

        //public static bool operator >(long left, UInt256 right)
        //{
        //    return left > right.value;
        //}

        //public static bool operator >(ulong left, UInt256 right)
        //{
        //    return left > right.value;
        //}

        //public static bool operator >(BigInteger left, UInt256 right)
        //{
        //    return left > right.value;
        //}

        //public static bool operator >=(UInt256 left, UInt256 right)
        //{
        //    return left.value >= right.value;
        //}

        //public static bool operator >=(UInt256 left, long right)
        //{
        //    return left.value >= right;
        //}

        //public static bool operator >=(UInt256 left, ulong right)
        //{
        //    return left.value >= right;
        //}

        //public static bool operator >=(UInt256 left, BigInteger right)
        //{
        //    return left.value >= right;
        //}

        //public static bool operator >=(long left, UInt256 right)
        //{
        //    return left >= right.value;
        //}

        //public static bool operator >=(ulong left, UInt256 right)
        //{
        //    return left >= right.value;
        //}

        //public static bool operator >=(BigInteger left, UInt256 right)
        //{
        //    return left >= right.value;
        //}

        //public static UInt256 operator >>(UInt256 value, int shift)
        //{
        //    return new UInt256(value.value >> shift);
        //}

        //public static explicit operator sbyte(UInt256 value)
        //{
        //    return (sbyte)value.value;
        //}

        //public static explicit operator decimal(UInt256 value)
        //{
        //    return (decimal)value.value;
        //}

        //public static explicit operator double(UInt256 value)
        //{
        //    return (double)value.value;
        //}

        //public static explicit operator float(UInt256 value)
        //{
        //    return (float)value.value;
        //}

        //public static explicit operator BigInteger(UInt256 value)
        //{
        //    return value.value;
        //}

        //public static explicit operator UInt128(UInt256 value)
        //{
        //    return new UInt128((BigInteger)value);
        //}

        //public static explicit operator ulong(UInt256 value)
        //{
        //    return (ulong)value.value;
        //}

        //public static explicit operator long(UInt256 value)
        //{
        //    return (long)value.value;
        //}

        //public static explicit operator uint(UInt256 value)
        //{
        //    return (uint)value.value;
        //}

        //public static explicit operator int(UInt256 value)
        //{
        //    return (int)value.value;
        //}

        //public static explicit operator short(UInt256 value)
        //{
        //    return (short)value.value;
        //}

        //public static explicit operator ushort(UInt256 value)
        //{
        //    return (ushort)value.value;
        //}

        //public static explicit operator byte(UInt256 value)
        //{
        //    return (byte)value.value;
        //}

        //public static explicit operator UInt256(decimal value)
        //{
        //    return new UInt256(value);
        //}

        //public static explicit operator UInt256(double value)
        //{
        //    return new UInt256(value);
        //}

        //public static explicit operator UInt256(float value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(byte value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(int value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(long value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(sbyte value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(short value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(uint value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(ulong value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(UInt128 value)
        //{
        //    return new UInt256(value);
        //}

        //public static implicit operator UInt256(ushort value)
        //{
        //    return new UInt256(value);
        //}

        ////public static UInt256 Add(UInt256 left, UInt256 right);

        //public static int Compare(UInt256 left, UInt256 right)
        //{
        //    return BigInteger.Compare(left.value, right.value);
        //}

        //public int CompareTo(UInt256 other)
        //{
        //    return this.value.CompareTo(other.value);
        //}

        //public int CompareTo(long other)
        //{
        //    return this.value.CompareTo(other);
        //}

        //public int CompareTo(object obj)
        //{
        //    return this.value.CompareTo(obj);
        //}

        //public int CompareTo(ulong other)
        //{
        //    return this.value.CompareTo(other);
        //}

        //public int CompareTo(BigInteger other)
        //{
        //    return this.value.CompareTo(other);
        //}

        ////public static UInt256 Divide(UInt256 dividend, UInt256 divisor);

        //public static UInt256 DivRem(UInt256 dividend, UInt256 divisor, out UInt256 remainder)
        //{
        //    BigInteger remainderBigInt;
        //    var result = new UInt256(BigInteger.DivRem(dividend.value, divisor.value, out remainderBigInt));
        //    remainder = new UInt256(remainderBigInt);
        //    return result;
        //}

        //public bool Equals(UInt256 other)
        //{
        //    return this.value.Equals(other.value);
        //}

        //public bool Equals(UInt128 other)
        //{
        //    return this.value.Equals((BigInteger)other);
        //}

        //public bool Equals(long other)
        //{
        //    return this.value.Equals(other);
        //}

        //public override bool Equals(object obj)
        //{
        //    if (obj is UInt128)
        //        return this.Equals((UInt128)obj);
        //    else if (obj is UInt256)
        //        return this.Equals((UInt256)obj);
        //    else
        //        return this.value.Equals(obj);
        //}

        //public bool Equals(ulong other)
        //{
        //    return this.value.Equals(other);
        //}

        //public bool Equals(BigInteger other)
        //{
        //    return this.value.Equals(other);
        //}

        //public override int GetHashCode()
        //{
        //    return this.hashCode;
        //}

        ////public static UInt256 GreatestCommonDivisor(UInt256 left, UInt256 right);

        ////public static double Log(UInt256 value);

        //public static double Log(UInt256 value, double baseValue)
        //{
        //    return BigInteger.Log(value.value, baseValue);
        //}

        ////public static double Log10(UInt256 value);

        ////public static UInt256 Max(UInt256 left, UInt256 right);

        ////public static UInt256 Min(UInt256 left, UInt256 right);

        ////public static UInt256 ModPow(UInt256 value, UInt256 exponent, UInt256 modulus);

        ////public static UInt256 Multiply(UInt256 left, UInt256 right);

        ////public static UInt256 Negate(UInt256 value);

        //public static UInt256 Parse(string value)
        //{
        //    return new UInt256(BigInteger.Parse("0" + value));
        //}

        //public static UInt256 Parse(string value, IFormatProvider provider)
        //{
        //    return new UInt256(BigInteger.Parse("0" + value, provider));
        //}

        //public static UInt256 Parse(string value, NumberStyles style)
        //{
        //    return new UInt256(BigInteger.Parse("0" + value, style));
        //}

        //public static UInt256 Parse(string value, NumberStyles style, IFormatProvider provider)
        //{
        //    return new UInt256(BigInteger.Parse("0" + value, style, provider));
        //}

        //public static UInt256 Pow(UInt256 value, int exponent)
        //{
        //    return new UInt256(BigInteger.Pow(value.value, exponent));
        //}

        ////public static UInt256 Remainder(UInt256 dividend, UInt256 divisor);

        ////public static UInt256 Subtract(UInt256 left, UInt256 right);

        //public byte[] ToByteArray()
        //{
        //    return UInt.ToByteArray(this.value, SIZE_BYTES);
        //}

        //public override string ToString()
        //{
        //    return this.value.ToString();
        //}

        //public string ToString(IFormatProvider provider)
        //{
        //    return this.value.ToString(provider);
        //}

        //public string ToString(string format)
        //{
        //    return this.value.ToString(format);
        //}

        //public string ToString(string format, IFormatProvider provider)
        //{
        //    return this.value.ToString(format, provider);
        //}

        //public static bool TryParse(string value, out UInt256 result)
        //{
        //    BigInteger bigIntResult;
        //    var success = BigInteger.TryParse("0" + value, out bigIntResult);
        //    result = new UInt256(bigIntResult);
        //    return success;
        //}

        //public static bool TryParse(string value, NumberStyles style, IFormatProvider provider, out UInt256 result)
        //{
        //    BigInteger bigIntResult;
        //    var success = BigInteger.TryParse("0" + value, style, provider, out bigIntResult);
        //    result = new UInt256(bigIntResult);
        //    return success;
        //}
    }
}
