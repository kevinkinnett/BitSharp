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
        private readonly ConcurrentDictionary<UInt256, ConcurrentSet<UInt256>> chainedBlocksByPrevious;

        public ChainedBlockCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("ChainedBlockCache", cacheContext.StorageContext.ChainedBlockStorage, maxFlushMemorySize, maxCacheMemorySize, ChainedBlock.SizeEstimator)
        {
            this._cacheContext = cacheContext;

            this.chainedBlocksByPrevious = new ConcurrentDictionary<UInt256, ConcurrentSet<UInt256>>();

            this.OnAddition += blockHash => UpdatePreviousIndex(blockHash);
            this.OnModification += (blockHash, chainedBlock) => UpdatePreviousIndex(chainedBlock);
            this.OnRetrieved += (blockHash, chainedBlock) => UpdatePreviousIndex(chainedBlock);

            foreach (var value in this.StreamAllValues())
                UpdatePreviousIndex(value.Value);
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public bool IsChainIntact(UInt256 blockHash)
        {
            ChainedBlock chainedBlock;
            if (TryGetValue(blockHash, out chainedBlock))
            {
                return IsChainIntact(chainedBlock);
            }
            else
            {
                return false;
            }
        }

        public bool IsChainIntact(ChainedBlock chainedBlock)
        {
            List<ChainedBlock> chain;
            return TryGetChain(chainedBlock, out chain);
        }

        public bool TryGetChain(UInt256 blockHash, out List<ChainedBlock> chain)
        {
            ChainedBlock chainedBlock;
            if (TryGetValue(blockHash, out chainedBlock))
            {
                return TryGetChain(chainedBlock, out chain);
            }
            else
            {
                chain = null;
                return false;
            }
        }

        public bool TryGetChain(ChainedBlock chainedBlock, out List<ChainedBlock> chain)
        {
            chain = new List<ChainedBlock>(chainedBlock.Height);

            // look backwards until height 0 is reached
            var expectedHeight = chainedBlock.Height;
            while (chainedBlock.Height != 0)
            {
                chain.Add(chainedBlock);

                // if a missing link occurrs before height 0, the chain isn't intact
                if (!this.ContainsKey(chainedBlock.PreviousBlockHash)
                    || !TryGetValue(chainedBlock.PreviousBlockHash, out chainedBlock))
                {
                    chain = null;
                    return false;
                }

                expectedHeight--;
                if (chainedBlock.Height != expectedHeight)
                {
                    Debugger.Break();
                    chain = null;
                    return false;
                }
            }
            chain.Add(chainedBlock);
            chain.Reverse();

            // height 0 reached, chain is intact
            return true;
        }

        public IEnumerable<ChainedBlock> FindLeafChainedBlocks()
        {
            var leafChainedBlocks = new HashSet<UInt256>(this.GetAllKeys());
            leafChainedBlocks.ExceptWith(this.chainedBlocksByPrevious.Keys);

            foreach (var leafChainedBlockHash in leafChainedBlocks)
            {
                ChainedBlock leafChainedBlock;
                if (this.TryGetValue(leafChainedBlockHash, out leafChainedBlock)
                    && this.IsChainIntact(leafChainedBlock))
                {
                    yield return leafChainedBlock;
                }
            }
        }

        public IEnumerable<List<ChainedBlock>> FindLeafChains()
        {
            var leafChainedBlocks = new HashSet<UInt256>(this.GetAllKeys());
            leafChainedBlocks.ExceptWith(this.chainedBlocksByPrevious.Keys);

            foreach (var leafChainedBlock in leafChainedBlocks)
            {
                List<ChainedBlock> leafChain;
                if (this.TryGetChain(leafChainedBlock, out leafChain))
                {
                    yield return leafChain;
                }
            }
        }

        public HashSet<UInt256> FindByPreviousBlockHash(UInt256 previousBlockHash)
        {
            ConcurrentSet<UInt256> set;
            if (this.chainedBlocksByPrevious.TryGetValue(previousBlockHash, out set))
            {
                return new HashSet<UInt256>(set);
            }
            else
            {
                return new HashSet<UInt256>();
            }
        }

        private void UpdatePreviousIndex(UInt256 blockHash)
        {
            ChainedBlock chainedBlock;
            if (this.TryGetValue(blockHash, out chainedBlock))
                UpdatePreviousIndex(chainedBlock);
        }

        private void UpdatePreviousIndex(ChainedBlock chainedBlock)
        {
            this.chainedBlocksByPrevious.AddOrUpdate
            (
                chainedBlock.PreviousBlockHash,
                newKey => new ConcurrentSet<UInt256>(),
                (existingKey, existingValue) => existingValue
            )
            .Add(chainedBlock.BlockHash);
        }
    }
}
