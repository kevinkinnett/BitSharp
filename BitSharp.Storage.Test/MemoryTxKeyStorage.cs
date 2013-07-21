using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryTxKeyStorage : ITxKeyStorage
    {
        private readonly MemoryStorageContext _storageContext;

        public MemoryTxKeyStorage(MemoryStorageContext storageContext)
        {
            this._storageContext = storageContext;
        }

        public MemoryStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
        }

        public bool TryReadValue(UInt256 key, out HashSet<TxKey> value)
        {
            value = new HashSet<TxKey>();
            foreach (var txBlock in this.StorageContext.BlockStorage.Storage.AsParallel().Where(x => x.Value.Transactions.Any(tx => tx.Hash == key)))
            {
                var txIndex = txBlock.Value.Transactions.ToList().FindIndex(tx => tx.Hash == key);
                value.Add(new TxKey(key, txBlock.Value.Hash, (UInt32)txIndex));
            }

            return value.Count > 0;
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<HashSet<TxKey>>>> values)
        {
            throw new NotSupportedException();
        }
    }
}
