using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockHeaderCache : BoundedCache<UInt256, BlockHeader>
    {
        private readonly CacheContext _cacheContext;
        private readonly BlockHeaderStorage _blockHeaderStorage;


        public BlockHeaderCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("BlockHeaderCache", new BlockHeaderStorage(cacheContext), maxFlushMemorySize, maxCacheMemorySize, BlockHeader.SizeEstimator)
        {
            this._cacheContext = cacheContext;
            this._blockHeaderStorage = (BlockHeaderStorage)this.DataStorage;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }
    }
}
