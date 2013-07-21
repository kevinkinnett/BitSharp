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
            : base("BlockCache", cacheContext.StorageContext.BlockStorage, maxFlushMemorySize, maxCacheMemorySize, Block.SizeEstimator)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public IEnumerable<UInt256> FindMissingPreviousBlocks()
        {
            // find pending blocks whose previous block doesn't exist
            var previousBlocksSet = new HashSet<UInt256>(GetPendingValues().Select(x => x.Value.Header.PreviousBlock));
            previousBlocksSet.ExceptWith(GetAllKeys());
            previousBlocksSet.Remove(UInt256.Zero);

            // return list from pending
            foreach (var blockHash in previousBlocksSet)
                yield return blockHash;

            // find blocks in storage whose previous block doesn't exist
            foreach (var blockHash in this.StorageContext.BlockStorage.FindMissingPreviousBlocks())
            {
                if (blockHash != 0 && !previousBlocksSet.Contains(blockHash))
                    yield return blockHash;
            }
        }
    }
}
