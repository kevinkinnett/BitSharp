using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Storage;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    public class MemoryBlockMetadataStorage : /*MemoryStorage<UInt256, BlockMetadata>,*/ IBlockMetadataStorage
    {
        private readonly MemoryBlockDataStorage blockStorage;
        private readonly ConcurrentDictionary<UInt256, BlockMetadata> storage;
        private readonly IBlockchainRules rules;

        public MemoryBlockMetadataStorage(MemoryBlockDataStorage blockStorage, IBlockchainRules rules)
        {
            this.blockStorage = blockStorage;
            this.storage = new ConcurrentDictionary<UInt256, BlockMetadata>();
            this.rules = rules;
        }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.blockStorage.ReadAllKeys();
        }

        public IEnumerable<KeyValuePair<UInt256, BlockMetadata>> ReadAllValues()
        {
            foreach (var key in this.blockStorage.ReadAllKeys())
            {
                BlockMetadata blockMetadata;
                if (TryReadValue(key, out blockMetadata))
                {
                    yield return new KeyValuePair<UInt256, BlockMetadata>(key, blockMetadata);
                }
            }
        }

        public bool TryReadValue(UInt256 key, out BlockMetadata blockMetadata)
        {
            // read from block data if metadata doesn't exist yet
            if (!this.storage.TryGetValue(key, out blockMetadata))
            {
                Block block;
                if (!this.blockStorage.TryReadValue(key, out block))
                {
                    return false;
                }

                blockMetadata = new BlockMetadata
                (
                    BlockHash: block.Hash,
                    PreviousBlockHash: block.Header.PreviousBlock,
                    Work: this.rules.CalculateWork(block.Header),
                    Height: null,
                    TotalWork: null,
                    IsValid: null
                );
            }

            // genesis block special case
            if (blockMetadata.PreviousBlockHash == new UInt256(0))
            {
                blockMetadata = new BlockMetadata
                (
                    BlockHash: blockMetadata.BlockHash,
                    PreviousBlockHash: blockMetadata.PreviousBlockHash,
                    Work: blockMetadata.Work,
                    Height: 0,
                    TotalWork: blockMetadata.Work,
                    IsValid: true
                );
            }

            // see if metadata needs to be chained and attempt to chain it
            if (blockMetadata.Height == null)
            {
                //TODO infinite recursion
                BlockMetadata prevBlockMetadata;
                if (
                    TryReadValue(blockMetadata.PreviousBlockHash, out prevBlockMetadata)
                    && prevBlockMetadata.Height != null)
                {
                    blockMetadata = new BlockMetadata
                    (
                        BlockHash: blockMetadata.BlockHash,
                        PreviousBlockHash: blockMetadata.PreviousBlockHash,
                        Work: blockMetadata.Work,
                        Height: prevBlockMetadata.Height + 1,
                        TotalWork: prevBlockMetadata.TotalWork + blockMetadata.Work,
                        IsValid: null
                    );
                }
            }

            // store updated metadata
            var blockMetadataLocal = blockMetadata;
            this.storage.AddOrUpdate(key, blockMetadata, (existingKey, existingValue) => blockMetadataLocal);
            return true;
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BlockMetadata>>> values)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<BlockMetadata> FindWinningChainedBlocks(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            ReadAllValues().ToList();

            var maxTotalWork = this.storage.Max(x => x.Value.TotalWork);
            if (maxTotalWork != null)
            {
                return this.storage.Where(x => x.Value.TotalWork == maxTotalWork).Select(x => x.Value);
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

            foreach (var blockMetadata in this.storage.Values)
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

            foreach (var chained in this.storage.Values.Where(x => x.Height != null))
            {
                foreach (var proceedingUnchained in this.storage.Values.Where(x => x.PreviousBlockHash == chained.BlockHash && x.Height == null))
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
    }
}
