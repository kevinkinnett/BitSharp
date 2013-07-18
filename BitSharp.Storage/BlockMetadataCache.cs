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
    public class BlockMetadataCache : BoundedCache<UInt256, BlockMetadata>
    {
        private readonly CacheContext _cacheContext;

        public BlockMetadataCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("BlockMetadataCache", cacheContext.StorageContext.BlockMetadataStorage, maxFlushMemorySize, maxCacheMemorySize, BlockMetadata.SizeEstimator)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public IEnumerable<BlockMetadata> FindWinningChainedBlocks()
        {
            var pendingMetadata = GetPendingValues().ToDictionary(x => x.Key, x => x.Value);

            var pendingWinners = new List<BlockMetadata>();
            var usePendingWinners = false;

            // get the winning total work amongst pending metadata
            var pendingMaxTotalWork = pendingMetadata.Select(x => x.Value.TotalWork).Max();

            if (pendingMaxTotalWork != null)
            {
                // get the winners amonst pending blocks
                pendingWinners.AddRange(pendingMetadata.Where(x => x.Value.TotalWork >= pendingMaxTotalWork).Select(x => x.Value));
                usePendingWinners = true;
            }

            // use the list of pending winners in addition to the list from storage, unless the list from storage has higher total work
            var winners = new List<BlockMetadata>();

            // get the winners amongst storage
            foreach (var blockMetadata in this.StorageContext.BlockMetadataStorage.FindWinningChainedBlocks())
            {
                if (!pendingMetadata.ContainsKey(blockMetadata.BlockHash) // make sure this block doesn't have a newer value in pending, which takes precedence
                    && (pendingMaxTotalWork == null || blockMetadata.TotalWork >= pendingMaxTotalWork))
                {
                    winners.Add(blockMetadata);

                    if (blockMetadata.TotalWork > pendingMaxTotalWork)
                        usePendingWinners = false;
                }
            }

            if (usePendingWinners)
                winners.AddRange(pendingWinners);

            return winners;
        }

        public Dictionary<UInt256, HashSet<UInt256>> FindUnchainedBlocksByPrevious()
        {
            return this.StorageContext.BlockMetadataStorage.FindUnchainedBlocksByPrevious();
        }

        public Dictionary<BlockMetadata, HashSet<BlockMetadata>> FindChainedWithProceedingUnchained()
        {
            var pendingMetadata = GetPendingValues().ToDictionary(x => x.Key, x => x.Value);

            //TODO pendingMetadata shouldn't be passed to backing storage
            return this.StorageContext.BlockMetadataStorage.FindChainedWithProceedingUnchained(pendingMetadata);
        }

        public IEnumerable<UInt256> FindMissingPreviousBlocks()
        {
            var knownBlocksSet = new HashSet<UInt256>(this.CacheContext.BlockCache.GetAllKeys());
            var previousBlocksSet = new HashSet<UInt256>();

            // get list of previous blocks from pending
            previousBlocksSet.UnionWith(GetPendingValues().Where(x => x.Value.Height == null).Select(x => x.Value.PreviousBlockHash));

            foreach (var previousBlockHash in this.StorageContext.BlockMetadataStorage.FindMissingPreviousBlocks())
            {
                if (!knownBlocksSet.Contains(previousBlockHash))
                    previousBlocksSet.Add(previousBlockHash);
            }

            // remove previous hash 0
            previousBlocksSet.Remove(UInt256.Zero);

            return previousBlocksSet;
        }

        public IEnumerable<UInt256> FindMissingBlocks()
        {
            var pendingBlockHashes = new HashSet<UInt256>(this.CacheContext.BlockCache.GetPendingValues().Select(x => x.Value.Hash));

            foreach (var blockHash in this.StorageContext.BlockMetadataStorage.FindMissingBlocks())
            {
                if (!pendingBlockHashes.Contains(blockHash))
                {
                    yield return blockHash;
                }
            }
        }
    }
}
