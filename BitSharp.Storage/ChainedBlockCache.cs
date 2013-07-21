using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class ChainedBlockCache : BoundedCache<UInt256, ChainedBlock>
    {
        private readonly CacheContext _cacheContext;

        public ChainedBlockCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("ChainedBlockCache", cacheContext.StorageContext.ChainedBlockStorage, maxFlushMemorySize, maxCacheMemorySize, ChainedBlock.SizeEstimator)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public bool IsChainIntact(ChainedBlock chainedBlock)
        {
            // look backwards until height 0 is reached
            while (chainedBlock.Height != 0)
            {
                // if a missing link occurrs before height 0, the chain isn't intact
                if (!TryGetValue(chainedBlock.PreviousBlockHash, out chainedBlock))
                {
                    return false;
                }
            }

            // height 0 reached, chain is intact
            return true;
        }

        public IEnumerable<ChainedBlock> FindLeafChained()
        {
            var pendingChainedBlocks = GetPendingValues().ToDictionary(x => x.Key, x => x.Value);
            var pendingPreviousHashes = new HashSet<UInt256>(pendingChainedBlocks.Values.Select(x => x.PreviousBlockHash));

            foreach (var chainedBlock in this.StorageContext.ChainedBlockStorage.FindLeafChained())
            {
                // check that there isn't a pending chained block which lists the leaf chained block as its previous block
                if (!pendingPreviousHashes.Contains(chainedBlock.BlockHash)
                    && IsChainIntact(chainedBlock))
                {
                    yield return chainedBlock;
                }
            }

            // find any pending blocks which aren't proceeded by a chained block in pending or storage
            foreach (var chainedBlock in pendingChainedBlocks.Values)
            {
                if (!pendingPreviousHashes.Contains(chainedBlock.BlockHash)
                    && IsChainIntact(chainedBlock)
                    && FindChainedByPreviousBlockHash(chainedBlock.BlockHash).Count() == 0)
                {
                    yield return chainedBlock;
                }
            }
        }

        public IEnumerable<ChainedBlock> FindChainedByPreviousBlockHash(UInt256 previousBlockHash)
        {
            var pendingChainedBlocks = GetPendingValues().ToDictionary(x => x.Key, x => x.Value);
            var returned = new HashSet<UInt256>();

            foreach (var chainedBlock in pendingChainedBlocks.Values.Where(x => x.PreviousBlockHash == previousBlockHash))
            {
                returned.Add(chainedBlock.BlockHash);
                yield return chainedBlock;
            }

            foreach (var chainedBlock in this.StorageContext.ChainedBlockStorage.FindChainedByPreviousBlockHash(previousBlockHash))
            {
                if (!returned.Contains(chainedBlock.BlockHash))
                    yield return chainedBlock;
            }
        }

        public IEnumerable<ChainedBlock> FindChainedWhereProceedingUnchainedExists()
        {
            var pendingBlocks = this.CacheContext.BlockCache.GetPendingValues().ToDictionary(x => x.Key, x => x.Value);
            var returned = new HashSet<UInt256>();

            // find pending blocks that aren't chained and whose previous block is chained
            foreach (var block in pendingBlocks.Values)
            {
                if (this.ContainsKey(block.Header.PreviousBlock) && !this.ContainsKey(block.Hash))
                {
                    ChainedBlock chainedBlock;
                    if (this.TryGetValue(block.Header.PreviousBlock, out chainedBlock))
                    {
                        returned.Add(chainedBlock.BlockHash);
                        yield return chainedBlock;
                    }
                }
            }

            // finding chained blocks in storage with proceeding unchained blocks
            foreach (var chainedBlock in this.StorageContext.ChainedBlockStorage.FindChainedWhereProceedingUnchainedExists())
            {
                if (!returned.Contains(chainedBlock.BlockHash))
                    yield return chainedBlock;
            }
        }

        public IEnumerable<BlockHeader> FindUnchainedWherePreviousBlockExists()
        {
            var pendingBlocks = this.CacheContext.BlockCache.GetPendingValues().ToDictionary(x => x.Key, x => x.Value);
            var returned = new HashSet<UInt256>();

            // find pending blocks which aren't chained
            var unchainedPendingBlocks = pendingBlocks.ToDictionary();
            unchainedPendingBlocks.RemoveRange(this.GetAllKeys());

            // finding unchained pending blocks whose previous block exists
            foreach (var unchainedBlock in unchainedPendingBlocks.Values)
            {
                if (this.CacheContext.BlockCache.ContainsKey(unchainedBlock.Header.PreviousBlock))
                {
                    returned.Add(unchainedBlock.Hash);
                    yield return unchainedBlock.Header;
                }
            }

            // find unchained blocks in storage whose previous block exists
            foreach (var unchainedBlock in this.StorageContext.ChainedBlockStorage.FindUnchainedWherePreviousBlockExists())
            {
                if (!returned.Contains(unchainedBlock.Hash))
                    yield return unchainedBlock;

            }
        }

        public IEnumerable<UInt256> FindMissingBlocks()
        {
            var pendingBlockHashes = new HashSet<UInt256>(this.GetPendingValues().Select(x => x.Value.BlockHash));

            // find all pending chained blocks which don't have a block entry
            pendingBlockHashes.ExceptWith(this.CacheContext.BlockCache.GetAllKeys());

            // return missing blocks found via pending
            foreach (var blockHash in pendingBlockHashes)
                yield return blockHash;

            // find missing blocks from storage
            foreach (var blockHash in this.StorageContext.ChainedBlockStorage.FindMissingBlocks())
            {
                if (!pendingBlockHashes.Contains(blockHash))
                {
                    yield return blockHash;
                }
            }
        }
    }
}
