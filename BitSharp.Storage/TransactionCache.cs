using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class TransactionCache : UnboundedCache<TxKey, Transaction>
    {
        private readonly CacheContext _cacheContext;

        public TransactionCache(CacheContext cacheContext, long maxCacheMemorySize)
            : base("TransactionCache", new TransactionStorage(cacheContext), 0, maxCacheMemorySize, Transaction.SizeEstimator)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        //TODO public?
        public void CacheBlock(Block block)
        {
            for (var txIndex = 0; txIndex < block.Transactions.Length; txIndex++)
            {
                var tx = block.Transactions[txIndex];
                CacheValue(new TxKey(block.Hash, (UInt32)txIndex, tx.Hash), tx);
            }
        }
    }
}
