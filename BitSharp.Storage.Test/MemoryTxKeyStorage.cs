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

        public bool TryReadValue(TxKeySearch key, out TxKey value)
        {
            var txBlock = this.StorageContext.BlockStorage.Storage.AsParallel().Where(x => x.Value.Transactions.Any(tx => tx.Hash == key.TxHash)).FirstOrDefault();
            if (!txBlock.Key.IsDefault)
            {
                var txIndex = txBlock.Value.Transactions.ToList().FindIndex(tx => tx.Hash == key.TxHash);
                value = new TxKey(key.TxHash, txBlock.Value.Hash, (UInt32)txIndex);
                return true;
            }
            else
            {
                value = default(TxKey);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<TxKeySearch, WriteValue<TxKey>>> values)
        {
            throw new NotSupportedException();
        }
    }
}
