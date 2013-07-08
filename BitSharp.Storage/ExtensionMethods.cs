using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.ExtensionMethods
{
    public static class StorageExtensionMethods
    {
        public static ImmutableDictionary<TKey, TValue> Compact<TKey, TValue>(this ImmutableDictionary<TKey, TValue> dictionary)
        {
            return ImmutableDictionary.Create(dictionary.ToArray());
        }

        public static ImmutableHashSet<T> Compact<T>(this ImmutableHashSet<T> set)
        {
            return ImmutableHashSet.Create<T>(set.ToArray());
        }

        public static ImmutableList<T> Compact<T>(this ImmutableList<T> list)
        {
            return ImmutableList.Create<T>(list.ToArray());
        }
    }
}
