using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class StorageManager : IDisposable
    {
        private readonly IBlockDataStorage blockDataStorage;
        private readonly IBlockHeaderStorage blockHeaderStorage;
        private readonly IBlockMetadataStorage blockMetadataStorage;
        private readonly ITransactionStorage txStorage;

        private readonly StorageCache<UInt256, Block> blockDataCache;
        private readonly StorageCache<UInt256, BlockHeader> blockHeaderCache;
        private readonly StorageCache<UInt256, BlockMetadata> blockMetadataCache;

        public StorageManager(IBlockDataStorage blockDataStorage, IBlockHeaderStorage blockHeaderStorage, IBlockMetadataStorage blockMetadataStorage, ITransactionStorage txStorage)
        {
            this.blockDataStorage = blockDataStorage;
            this.blockHeaderStorage = blockHeaderStorage;
            this.blockMetadataStorage = blockMetadataStorage;
            this.txStorage = txStorage;

            this.blockDataCache = new StorageCache<UInt256, Block>
            (
                name: "BlockDataCache",
                dataStorage: this.blockDataStorage,
                maxCacheMemorySize: 5.MILLION(),
                maxFlushMemorySize: 1.MILLION(),
                sizeEstimator: Block.SizeEstimator
            );

            this.blockHeaderCache = new StorageCache<UInt256, BlockHeader>
            (
                name: "BlockHeaderCache",
                dataStorage: this.blockHeaderStorage,
                maxCacheMemorySize: 5.MILLION(),
                maxFlushMemorySize: 0,
                sizeEstimator: BlockHeader.SizeEstimator
            );

            this.blockMetadataCache = new StorageCache<UInt256, BlockMetadata>
            (
                name: "BlockMetadataCache",
                dataStorage: this.blockMetadataStorage,
                maxCacheMemorySize: 5.MILLION(),
                maxFlushMemorySize: 1.MILLION(),
                sizeEstimator: BlockMetadata.SizeEstimator
            );
        }

        public IBlockDataStorage BlockDataStorage { get { return this.blockDataStorage; } }

        public IBlockHeaderStorage BlockHeaderStorage { get { return this.blockHeaderStorage; } }

        public IBlockMetadataStorage BlockMetadataStorage { get { return this.blockMetadataStorage; } }

        public ITransactionStorage TransactionStorage { get { return this.txStorage; } }

        public StorageCache<UInt256, Block> BlockDataCache { get { return this.blockDataCache; } }

        public StorageCache<UInt256, BlockHeader> BlockHeaderCache { get { return this.blockHeaderCache; } }

        public StorageCache<UInt256, BlockMetadata> BlockMetadataCache { get { return this.blockMetadataCache; } }

        public void Dispose()
        {
            this.blockDataCache.Dispose();
            this.blockHeaderCache.Dispose();
            this.blockMetadataCache.Dispose();
        }

        //TODO
        public void WaitForFlush()
        {
        }
    }
}
