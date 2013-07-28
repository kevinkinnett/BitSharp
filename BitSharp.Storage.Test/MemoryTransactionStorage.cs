using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryTransactionStorage : ITransactionStorage
    {
        private readonly MemoryStorageContext _storageContext;

        public MemoryTransactionStorage(MemoryStorageContext storageContext)
        {
            this._storageContext = storageContext;
        }

        public MemoryStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
        }

        public bool TryReadValue(UInt256 key, out Transaction value)
        {
            value = this.StorageContext.BlockTransactionsStorage.Storage.SelectMany(x => x.Value).FirstOrDefault(x => x.Hash == key);
            return !value.IsDefault;
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Transaction>>> values)
        {
            throw new NotSupportedException();
        }
    }
}
