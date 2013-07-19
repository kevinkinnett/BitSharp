using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.SqlServer.ExtensionMethods
{
    internal static class ExtensionMethods
    {
        public static byte[] GetBytes(this DbDataReader reader, int i)
        {
            var bytes = new byte[reader.GetBytes(i, 0, null, 0, 0)];
            reader.GetBytes(i, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static UInt16 GetUInt16(this DbDataReader reader, int i)
        {
            return Bits.ToUInt16(reader.GetBytes(i).Reverse().ToArray());
        }

        public static UInt32 GetUInt32(this DbDataReader reader, int i)
        {
            return Bits.ToUInt32(reader.GetBytes(i).Reverse().ToArray());
        }

        public static UInt64 GetUInt64(this DbDataReader reader, int i)
        {
            return Bits.ToUInt64(reader.GetBytes(i).Reverse().ToArray());
        }

        public static UInt256 GetUInt256(this DbDataReader reader, int i)
        {
            return new UInt256(reader.GetBytes(i).Reverse().ToArray());
        }

        public static BigInteger GetBigInteger(this DbDataReader reader, int i)
        {
            // BigIntegers are stored in big-endian order so that SQL Max() works correctly
            return new BigInteger(reader.GetBytes(i).Reverse().ToArray());
        }

        public static BigInteger? GetBigIntegerNullable(this DbDataReader reader, int i)
        {
            if (!reader.IsDBNull(i))
                return reader.GetBigInteger(i);
            else
                return null;
        }

        public static bool? GetBooleanNullable(this DbDataReader reader, int i)
        {
            if (!reader.IsDBNull(i))
                return reader.GetBoolean(i);
            else
                return null;
        }

        public static long? GetInt64Nullable(this DbDataReader reader, int i)
        {
            if (!reader.IsDBNull(i))
                return reader.GetInt64(i);
            else
                return null;
        }

        public static byte[] ToDbByteArray(this UInt16 value)
        {
            return Bits.GetBytesBE(value);
        }

        public static byte[] ToDbByteArray(this UInt32 value)
        {
            return Bits.GetBytesBE(value);
        }

        public static byte[] ToDbByteArray(this UInt64 value)
        {
            return Bits.GetBytesBE(value);
        }

        public static byte[] ToDbByteArray(this UInt128 value)
        {
            return value.ToByteArray().Reverse().ToArray();
        }

        public static byte[] ToDbByteArray(this UInt256 value)
        {
            return value.ToByteArray().Reverse().ToArray();
        }

        public static byte[] ToDbByteArray(this BigInteger value)
        {
            var bigIntBytes = value.ToByteArray().Reverse().ToArray();
            if (bigIntBytes.Length > 64 && !(bigIntBytes.Length == 65 && bigIntBytes[0] == 0))
                throw new ArgumentOutOfRangeException();

            var buffer = new byte[64];
            if (bigIntBytes.Length <= 64)
                Buffer.BlockCopy(bigIntBytes, 0, buffer, 64 - bigIntBytes.Length, bigIntBytes.Length);
            else
                Buffer.BlockCopy(bigIntBytes, 1, buffer, 0, 64);

            return buffer;
        }

        public static SqlParameter SetValue(this DbParameterCollection parameters, string parameterName, SqlDbType dbType)
        {
            var param = new SqlParameter { ParameterName = parameterName, SqlDbType = dbType };
            if (parameters.Contains(parameterName))
                parameters.RemoveAt(parameterName);

            parameters.Add(param);
            return param;
        }

        public static SqlParameter SetValue(this DbParameterCollection parameters, string parameterName, SqlDbType dbType, int size)
        {
            var param = new SqlParameter { ParameterName = parameterName, SqlDbType = dbType, Size = size };
            if (parameters.Contains(parameterName))
                parameters.RemoveAt(parameterName);

            parameters.Add(param);
            return param;
        }

        public static bool IsDeadlock(this SqlException e)
        {
            return e.ErrorCode == 1205;
        }
    }
}
