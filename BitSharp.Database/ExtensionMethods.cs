using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Database.ExtensionMethods
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
            if (bigIntBytes.Length > 64)
                throw new ArgumentOutOfRangeException();

            var buffer = new byte[64];
            Buffer.BlockCopy(bigIntBytes, 0, buffer, 64 - bigIntBytes.Length, bigIntBytes.Length);
            return buffer;
        }

#if SQLITE
        public static SQLiteParameter SetValue(this DbParameterCollection parameters, string parameterName, DbType dbType)
        {
            var param = new SQLiteParameter { ParameterName = parameterName, DbType = dbType };
#elif SQL_SERVER
        public static SqlParameter SetValue(this DbParameterCollection parameters, string parameterName, DbType dbType)
        {
            var param = new SqlParameter { ParameterName = parameterName, DbType = dbType };
#endif
            if (parameters.Contains(parameterName))
                parameters.RemoveAt(parameterName);

            parameters.Add(param);
            return param;
        }

#if SQLITE
        public static SQLiteParameter SetValue(this DbParameterCollection parameters, string parameterName, DbType dbType, int size)
        {
            var param = new SQLiteParameter { ParameterName = parameterName, DbType = dbType, Size = size };
#elif SQL_SERVER
        public static SqlParameter SetValue(this DbParameterCollection parameters, string parameterName, DbType dbType, int size)
        {
            var param = new SqlParameter { ParameterName = parameterName, DbType = dbType, Size = size };
#endif
            if (parameters.Contains(parameterName))
                parameters.RemoveAt(parameterName);

            parameters.Add(param);
            return param;
        }
    }
}
