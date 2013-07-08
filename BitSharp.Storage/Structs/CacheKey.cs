using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    internal struct CacheKey<T>
    {
        public readonly T Key;
        public readonly long Index;
        private readonly int _hashCode;

        public CacheKey(T Key, long Index)
        {
            this.Key = Key;
            this.Index = Index;
            _hashCode = this.Key.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CacheKey<T>))
                return false;

            var other = (CacheKey<T>)obj;
            return this.Key.Equals(other.Key);
        }

        public override int GetHashCode()
        {
            return this._hashCode;
        }

        public static explicit operator CacheKey<T>(T key)
        {
            return new CacheKey<T>(key, -1);
        }
    }
}
