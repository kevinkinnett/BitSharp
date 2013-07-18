using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryStorage<TKey, TValue> : IBoundedStorage<TKey, TValue>
    {
        private readonly MemoryStorageContext _storageContext;
        private readonly ConcurrentDictionary<TKey, TValue> _storage = new ConcurrentDictionary<TKey, TValue>();

        public MemoryStorage(MemoryStorageContext storageContext)
        {
            this._storageContext = storageContext;
        }

        public MemoryStorageContext StorageContext { get { return this._storageContext; } }

        protected ConcurrentDictionary<TKey, TValue> Storage { get { return this._storage; } }

        public void Dispose()
        {
        }

        public IEnumerable<TKey> ReadAllKeys()
        {
            return this._storage.Keys;
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> ReadAllValues()
        {
            return this._storage;
        }

        public bool TryReadValue(TKey key, out TValue value)
        {
            return this._storage.TryGetValue(key, out value);
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<TKey, WriteValue<TValue>>> values)
        {
            foreach (var keyPair in values)
            {
                this._storage.AddOrUpdate(
                    keyPair.Key,
                    keyPair.Value.Value,
                    (existingKey, existingValue) => keyPair.Value.IsCreate ? existingValue : keyPair.Value.Value);
            }

            return true;
        }

        public bool TryWriteValue(KeyValuePair<TKey, WriteValue<TValue>> keyPair)
        {
            this._storage.AddOrUpdate(
                keyPair.Key,
                keyPair.Value.Value,
                (existingKey, existingValue) => keyPair.Value.IsCreate ? existingValue : keyPair.Value.Value);

            return true;
        }
    }
}
