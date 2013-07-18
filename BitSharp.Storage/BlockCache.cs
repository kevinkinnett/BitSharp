using BitSharp.Common;
using BitSharp.Data;
using BitSharp.WireProtocol;
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
            : base("BlockCache", cacheContext.StorageContext.BlockStorage, maxFlushMemorySize, maxCacheMemorySize, Block.SizeEstimator)
        { }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }
    }
}
