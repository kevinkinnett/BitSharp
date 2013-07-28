using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockCache : BoundedCache<UInt256, Block>
    {
        private readonly CacheContext _cacheContext;

        public BlockCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("BlockCache", cacheContext.BlockStorage, maxFlushMemorySize, maxCacheMemorySize, Block.SizeEstimator)
        {
            this._cacheContext = cacheContext;

            //TODO keep this?
            //this.OnRetrieved += (blockHash, block) => this.CacheContext.TxKeyCache.CacheBlock(block);
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        protected override void BeforeCreateOrUpdate(UInt256 blockHash, Block block, bool isCreate)
        {
            //TODO keep this?
            //this.CacheContext.TransactionCache.CacheBlock(block);
        }
    }
}
