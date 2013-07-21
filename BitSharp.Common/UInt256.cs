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

        public static bool operator ==(UInt256 left, UInt256 right)
        {
            return left.part1 == right.part1 && left.part2 == right.part2 && left.part3 == right.part3 && left.part4 == right.part4;
        }

        public static bool operator !=(UInt256 left, UInt256 right)
        {
            return !(left == right);
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

        public override string ToString()
        {
            return this.ToHexNumberString();
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
    }
}
