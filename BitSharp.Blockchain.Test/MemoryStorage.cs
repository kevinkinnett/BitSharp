using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    public class MemoryStorage<TKey, TValue> : IDataStorage<TKey, TValue>
    {
        protected readonly ConcurrentDictionary<TKey, TValue> storage = new ConcurrentDictionary<TKey, TValue>();

        public IEnumerable<TKey> ReadAllKeys()
        {
            return this.storage.Keys;
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> ReadAllValues()
        {
            return this.storage;
        }

        public bool TryReadValue(TKey key, out TValue value)
        {
            return this.storage.TryGetValue(key, out value);
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<TKey, WriteValue<TValue>>> values)
        {
            foreach (var keyPair in values)
            {
                this.storage.AddOrUpdate(
                    keyPair.Key,
                    keyPair.Value.Value,
                    (existingKey, existingValue) => keyPair.Value.IsCreate ? existingValue : keyPair.Value.Value);
            }

            return true;
        }
    }
}
