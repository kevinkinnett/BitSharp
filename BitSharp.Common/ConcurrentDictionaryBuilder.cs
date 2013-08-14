using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class ConcurrentDictionaryBuilder<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDictionary<TKey, TValue>
    {
        private readonly ImmutableDictionary<TKey, TValue>.Builder builder;
        //TODO make the set disposable because of lock?
        private readonly ReaderWriterLockSlim builderLock;

        public ConcurrentDictionaryBuilder()
        {
            this.builder = ImmutableDictionary.CreateBuilder<TKey, TValue>();
            this.builderLock = new ReaderWriterLockSlim();
        }

        public bool TryAdd(TKey key, TValue value)
        {
            return this.builderLock.DoWrite(() =>
            {
                if (!this.builder.ContainsKey(key))
                {
                    this.builder.Add(key, value);
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }

        public bool TryAdd(KeyValuePair<TKey, TValue> item)
        {
            return this.builderLock.DoWrite(() =>
            {
                if (!this.builder.ContainsKey(item.Key))
                {
                    this.builder.Add(item);
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }

        public bool TryRemove(TKey key, out TValue value)
        {
            TValue valueLocal = default(TValue);

            var result = this.builderLock.DoWrite(() =>
            {
                if (this.builder.TryGetValue(key, out valueLocal))
                {
                    this.builder.Remove(key);
                    return true;
                }
                else
                    return false;
            });

            value = valueLocal;
            return result;
        }

        public void Add(TKey key, TValue value)
        {
            this.builderLock.DoWrite(() =>
                this.builder.Add(key, value));
        }

        public bool ContainsKey(TKey key)
        {
            return this.builderLock.DoRead(() =>
                this.builder.ContainsKey(key));
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return this.ToImmutable().Keys.ToImmutableList();
            }
        }

        public bool Remove(TKey key)
        {
            return this.builderLock.DoWrite(() =>
                this.builder.Remove(key));
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            TValue valueLocal = default(TValue);
            var result = this.builderLock.DoRead(() =>
                this.builder.TryGetValue(key, out valueLocal));

            value = valueLocal;
            return result;
        }

        public ICollection<TValue> Values
        {
            get
            {
                return this.ToImmutable().Values.ToImmutableList();
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                return this.builderLock.DoRead(() =>
                    this.builder[key]);
            }
            set
            {
                this.builderLock.DoWrite(() =>
                    this.builder[key] = value);
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            this.builderLock.DoWrite(() =>
                this.builder.Add(item));
        }

        public void Clear()
        {
            this.builderLock.DoWrite(() =>
                this.builder.Clear());
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return this.builderLock.DoRead(() =>
                this.builder.Contains(item));
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            var keyPairs = this.ToImmutable().ToArray();
            Buffer.BlockCopy(keyPairs, 0, array, arrayIndex, keyPairs.Length);
        }

        public int Count
        {
            get
            {
                return this.builderLock.DoRead(() =>
                    this.builder.Count);
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return this.builderLock.DoWrite(() =>
                this.Remove(item));
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.ToImmutable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public ImmutableDictionary<TKey, TValue> ToImmutable()
        {
            return this.builderLock.DoRead(() =>
                this.builder.ToImmutable());
        }
    }
}
