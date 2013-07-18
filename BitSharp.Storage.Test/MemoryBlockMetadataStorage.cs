using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryBlockMetadataStorage : MemoryStorage<UInt256, BlockMetadata>, IBlockMetadataStorage
    {
        public MemoryBlockMetadataStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }
 
        public IEnumerable<BlockMetadata> FindWinningChainedBlocks(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            ReadAllValues().ToList();

            var maxTotalWork = this.Storage.Max(x => x.Value.TotalWork);
            if (maxTotalWork != null)
            {
                return this.Storage.Where(x => x.Value.TotalWork == maxTotalWork).Select(x => x.Value);
            }
            else
            {
                return Enumerable.Empty<BlockMetadata>();
            }
        }

        public Dictionary<UInt256, HashSet<UInt256>> FindUnchainedBlocksByPrevious()
        {
            ReadAllValues().ToList();

            var unchainedBlocksByPrevious = new Dictionary<UInt256, HashSet<UInt256>>();

            foreach (var blockMetadata in this.Storage.Values)
            {
                var blockHash = blockMetadata.BlockHash;
                var previousBlockHash = blockMetadata.PreviousBlockHash;

                HashSet<UInt256> unchainedSet;
                if (!unchainedBlocksByPrevious.TryGetValue(previousBlockHash, out unchainedSet))
                {
                    unchainedSet = new HashSet<UInt256>();
                    unchainedBlocksByPrevious.Add(previousBlockHash, unchainedSet);
                }

                unchainedSet.Add(blockHash);
            }

            return unchainedBlocksByPrevious;
        }

        public Dictionary<BlockMetadata, HashSet<BlockMetadata>> FindChainedWithProceedingUnchained(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            ReadAllValues().ToList();

            var chainedWithProceedingUnchained = new Dictionary<BlockMetadata, HashSet<BlockMetadata>>();

            //TODO pendingMetadata

            foreach (var chained in this.Storage.Values.Where(x => x.Height != null))
            {
                foreach (var proceedingUnchained in this.Storage.Values.Where(x => x.PreviousBlockHash == chained.BlockHash && x.Height == null))
                {
                    HashSet<BlockMetadata> proceedingSet;
                    if (!chainedWithProceedingUnchained.TryGetValue(chained, out proceedingSet))
                    {
                        proceedingSet = new HashSet<BlockMetadata>();
                        chainedWithProceedingUnchained.Add(chained, proceedingSet);
                    }

                    proceedingSet.Add(proceedingUnchained);
                }
            }

            return chainedWithProceedingUnchained;
        }

        public IEnumerable<UInt256> FindMissingPreviousBlocks(IEnumerable<UInt256> knownBlocks, IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            throw new NotImplementedException();
        }


        public IEnumerable<UInt256> FindMissingBlocks()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<BlockMetadata> FindWinningChainedBlocks()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UInt256> FindMissingPreviousBlocks()
        {
            throw new NotImplementedException();
        }
    }
}
