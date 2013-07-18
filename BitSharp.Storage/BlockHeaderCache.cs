using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
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
    public class BlockHeaderCache : BoundedCache<UInt256, BlockHeader>
    {
        private readonly CacheContext _cacheContext;

        public BlockHeaderCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("BlockHeaderCache", new BlockHeaderStorage(cacheContext), maxFlushMemorySize, maxCacheMemorySize, BlockHeader.SizeEstimator)
        { }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public class BlockHeaderStorage : IBoundedStorage<UInt256, BlockHeader>
        {
            private readonly CacheContext _cacheContext;

            public BlockHeaderStorage(CacheContext cacheContext)
            {
                this._cacheContext = cacheContext;
            }

            public CacheContext CacheContext { get { return this._cacheContext; } }

            public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

            public void Dispose()
            {
            }

            public IEnumerable<UInt256> ReadAllKeys()
            {
                return this.CacheContext.BlockCache.GetAllKeys();
            }

            public IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllValues()
            {
                var pendingBlocks = this.CacheContext.BlockCache.GetPendingValues().ToDictionary();

                foreach (var block in pendingBlocks.Values)
                    yield return new KeyValuePair<UInt256, BlockHeader>(block.Hash, block.Header);

                foreach (var blockHeader in this.StorageContext.BlockStorage.ReadAllBlockHeaders())
                {
                    if (!pendingBlocks.ContainsKey(blockHeader.Value.Hash))
                        yield return new KeyValuePair<UInt256, BlockHeader>(blockHeader.Value.Hash, blockHeader.Value);
                }
            }

            public bool TryReadValue(UInt256 blockHash, out BlockHeader blockHeader)
            {
                var pendingBlocks = this.CacheContext.BlockCache.GetPendingValues().ToDictionary();

                if (pendingBlocks.ContainsKey(blockHash))
                {
                    blockHeader = pendingBlocks[blockHash].Header;
                    return true;
                }
                else
                {
                    return this.StorageContext.BlockStorage.TryReadBlockHeader(blockHash, out blockHeader);
                }
            }

            public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BlockHeader>>> values)
            {
                throw new NotSupportedException();
            }
        }
    }
}
