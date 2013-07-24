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
    public class TransactionCache : UnboundedCache<UInt256, Transaction>
    {
        private readonly CacheContext _cacheContext;

        public TransactionCache(CacheContext cacheContext, long maxCacheMemorySize)
            : base("TransactionCache", cacheContext.StorageContext.TransactionStorage, 0, maxCacheMemorySize, Transaction.SizeEstimator)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }
    }
}
