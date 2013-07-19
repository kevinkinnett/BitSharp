using BitSharp.Common;
using FirebirdSql.Data.FirebirdClient;
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

namespace BitSharp.Storage.Firebird.ExtensionMethods
{
    internal static class ExtensionMethods
    {
        public static byte[] GetCharBytes(this DbDataReader reader, int i)
        {
            var value = reader.GetString(i);

            //TODO for the love of...
            Guid guid;
            if (value.Length == 36 && Guid.TryParse(value, out guid))
            {
                return guid.ToByteArray();
            }
            else
            {
                //TODO make sure this won't actually mangle anything, see Guid above
                return value.Select(x => (byte)x).ToArray();
            }
        }

        public static byte[] GetBytes(this DbDataReader reader, int i)
        {
            var bytes = new byte[reader.GetBytes(i, 0, null, 0, 0)];
            reader.GetBytes(i, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static UInt16 GetUInt16(this DbDataReader reader, int i)
        {
            return Bits.ToUInt16(reader.GetCharBytes(i).Reverse().ToArray());
        }

        public static UInt32 GetUInt32(this DbDataReader reader, int i)
        {
            return Bits.ToUInt32(reader.GetCharBytes(i).Reverse().ToArray());
        }

        public static UInt64 GetUInt64(this DbDataReader reader, int i)
        {
            return Bits.ToUInt64(reader.GetCharBytes(i).Reverse().ToArray());
        }

        public static UInt256 GetUInt256(this DbDataReader reader, int i)
        {
            return new UInt256(reader.GetCharBytes(i).Reverse().ToArray());
        }

        public static BigInteger GetBigInteger(this DbDataReader reader, int i)
        {
            // BigIntegers are stored in big-endian order so that SQL Max() works correctly
            return new BigInteger(reader.GetCharBytes(i).Reverse().ToArray());
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
                return reader.GetInt32(i) != 0;
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

        public static FbParameter SetValue(this DbParameterCollection parameters, string parameterName, FbDbType dbType)
        {
            var param = new FbParameter { ParameterName = parameterName, FbDbType = dbType };
            if (parameters.Contains(parameterName))
                parameters.RemoveAt(parameterName);

            parameters.Add(param);
            return param;
        }

        public static FbParameter SetValue(this DbParameterCollection parameters, string parameterName, FbDbType dbType, int size)
        {
            var param = new FbParameter { ParameterName = parameterName, FbDbType = dbType, Size = size };
            if (parameters.Contains(parameterName))
                parameters.RemoveAt(parameterName);

            parameters.Add(param);
            return param;
        }

        public static FbParameter SetValue(this DbParameterCollection parameters, string parameterName, FbDbType dbType, FbCharset charset, int size)
        {
            var param = new FbParameter { ParameterName = parameterName, FbDbType = dbType, Charset = charset, Size = size };
            if (parameters.Contains(parameterName))
                parameters.RemoveAt(parameterName);

            parameters.Add(param);
            return param;
        }
    }
}
