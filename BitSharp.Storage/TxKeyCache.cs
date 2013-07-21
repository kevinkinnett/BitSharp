using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class TxKeyCache : UnboundedCache<UInt256, HashSet<TxKey>>
    {
        private readonly CacheContext _cacheContext;

        public TxKeyCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("TxKeyCache", cacheContext.StorageContext.TxKeyStorage, maxFlushMemorySize, maxCacheMemorySize, txKey => 70)
        { }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        internal void InvalidateBlock(Block block)
        {
            foreach (var tx in block.Transactions)
            {
                DecacheValue(tx.Hash);
            }
        }
    }
}
