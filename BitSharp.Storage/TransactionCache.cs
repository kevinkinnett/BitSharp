using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class TransactionCache : UnboundedCache<TxKeySearch, Transaction>
    {
        private readonly CacheContext _cacheContext;

        public TransactionCache(CacheContext cacheContext, long maxCacheMemorySize)
            : base("TransactionCache", new TransactionStorage(cacheContext), 0, maxCacheMemorySize, Transaction.SizeEstimator)
        { }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public class TransactionStorage : IUnboundedStorage<TxKeySearch, Transaction>
        {
            private readonly CacheContext _cacheContext;
            
            public TransactionStorage(CacheContext cacheContext)
            {
                this._cacheContext = cacheContext;
            }

            public CacheContext CacheContext { get { return this._cacheContext; } }

            public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

            public void Dispose()
            {
            }

            public bool TryReadValue(TxKeySearch txKeySearch, out Transaction value)
            {
                TxKey txKey;
                if (this.CacheContext.TxKeyCache.TryGetValue(txKeySearch, out txKey))
                {
                    Block block;
                    if (this.CacheContext.BlockCache.TryGetValue(txKey.BlockHash, out block))
                    {
                        if (txKey.TxIndex >= block.Transactions.Length)
                            throw new MissingDataException(DataType.Transaction, txKey.TxHash); //TODO should be invalid data, not missing data

                        value = block.Transactions[txKey.TxIndex.ToIntChecked()];
                        return true;
                    }
                }

                value = default(Transaction);
                return false;
            }

            public bool TryWriteValues(IEnumerable<KeyValuePair<TxKeySearch, WriteValue<Transaction>>> values)
            {
                throw new NotSupportedException();
            }
        }
    }
}
