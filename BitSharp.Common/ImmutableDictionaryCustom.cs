using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public static class ImmutableDictionaryCustom
    {
        public static ImmutableDictionaryCustom<TKey, TValue> Create<TKey, TValue>()
        {
            return ImmutableDictionaryCustom<TKey, TValue>.Create(new Dictionary<TKey, TValue>());
        }

        public static ImmutableDictionaryCustom<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue> dictionary)
        {
            return ImmutableDictionaryCustom<TKey, TValue>.Create(dictionary);
        }
    }

    public struct ImmutableDictionaryCustom<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IReadOnlyDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> dictionary;
        private readonly ImmutableArray<TKey> keys;
        private readonly ImmutableArray<TValue> values;

        private ImmutableDictionaryCustom(IDictionary<TKey, TValue> dictionary, bool clone = true)
        {
            if (dictionary == null)
                throw new ArgumentNullException("dictionary");

            if (clone)
            {
                this.dictionary = new Dictionary<TKey, TValue>(dictionary.Count);
                foreach (var item in dictionary)
                    this.dictionary.Add(item.Key, item.Value);
            }
            else
            {
                this.dictionary = dictionary;
            }

            //TODO this probably uses way too much extra memory
            this.keys = this.dictionary.Keys.ToImmutableArray();
            this.values = this.dictionary.Values.ToImmutableArray();
        }

        public TValue this[TKey key]
        {
            get { return this.dictionary[key]; }
        }

        public int Count
        {
            get { return this.dictionary.Count; }
        }

        public ImmutableDictionaryCustom<TKey, TValue> Add(TKey key, TValue value, bool overwrite = false)
        {
            var result = this.ToDictionary();

            if (overwrite)
            {
                result[key] = value;
            }
            else
            {
                if (!result.ContainsKey(key))
                    result.Add(key, value);
            }

            return new ImmutableDictionaryCustom<TKey, TValue>(result, clone: false);
        }

        public ImmutableDictionaryCustom<TKey, TValue> AddRange(IDictionary<TKey, TValue> other, bool overwrite = false)
        {
            var result = this.ToDictionary();

            if (overwrite)
            {
                foreach (var item in other)
                    result[item.Key] = item.Value;
            }
            else
            {
                foreach (var item in other)
                {
                    if (!result.ContainsKey(item.Key))
                        result.Add(item.Key, item.Value);
                }
            }

            return new ImmutableDictionaryCustom<TKey, TValue>(result, clone: false);
        }

        public ImmutableDictionaryCustom<TKey, TValue> AddRange2(IReadOnlyDictionary<TKey, TValue> other, bool overwrite = false)
        {
            var result = this.ToDictionary();

            if (overwrite)
            {
                foreach (var item in other)
                    result[item.Key] = item.Value;
            }
            else
            {
                foreach (var item in other)
                {
                    if (!result.ContainsKey(item.Key))
                        result.Add(item.Key, item.Value);
                }
            }

            return new ImmutableDictionaryCustom<TKey, TValue>(result, clone: false);
        }

        public ImmutableDictionaryCustom<TKey, TValue> RemoveRange(IEnumerable<TKey> keys)
        {
            var result = this.ToDictionary();

            foreach (var key in keys)
                result.Remove(key);

            return new ImmutableDictionaryCustom<TKey, TValue>(result, clone: false);
        }

        public Dictionary<TKey, TValue> ToDictionary()
        {
            var result = new Dictionary<TKey, TValue>(this.Count);
            foreach (var item in this.dictionary)
                result.Add(item.Key, item.Value);

            return result;
        }

        public ImmutableDictionaryCustom<TKey, TValue> ToImmutableDictionary()
        {
            return this;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var item in this.dictionary)
                yield return item;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool ContainsKey(TKey key)
        {
            return this.dictionary.ContainsKey(key);
        }

        public IEnumerable<TKey> Keys
        {
            get { return this.keys; }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return this.dictionary.TryGetValue(key, out value);
        }

        public IEnumerable<TValue> Values
        {
            get { return this.values; }
        }

        public static ImmutableDictionaryCustom<TKey, TValue> Create(IDictionary<TKey, TValue> dictionary)
        {
            return new ImmutableDictionaryCustom<TKey, TValue>(dictionary);
        }
    }

    public static class ImmutableDictionaryCustomExtensions
    {
        public static ImmutableDictionaryCustom<TKey, TValue> ToImmutableDictionaryCustom<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return ImmutableDictionaryCustom<TKey, TValue>.Create(dictionary);
        }
    }
}
