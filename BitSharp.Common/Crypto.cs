using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;

namespace BitSharp.Common
{
    public static class Crypto
    {
        private static readonly ThreadLocal<SHA256Managed> _sha256 = new ThreadLocal<SHA256Managed>();
        private static SHA256Managed sha256 { get { return _sha256.IsValueCreated ? _sha256.Value : new SHA256Managed(); } }

        private static readonly ThreadLocal<RIPEMD160Managed> _ripemd160 = new ThreadLocal<RIPEMD160Managed>();
        private static RIPEMD160Managed ripemd160 { get { return _ripemd160.IsValueCreated ? _ripemd160.Value : new RIPEMD160Managed(); } }

        public static byte[] DoubleSHA256(byte[] buffer)
        {
            return sha256.ComputeHash(sha256.ComputeHash(buffer));
        }

        public static byte[] SingleSHA256(byte[] buffer)
        {
            return sha256.ComputeHash(buffer);
        }

        public static byte[] SingleRIPEMD160(byte[] buffer)
        {
            return ripemd160.ComputeHash(buffer);
        }
    }
}
