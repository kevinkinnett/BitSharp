using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
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
    public class CacheContext : IDisposable
    {
        private readonly IStorageContext _storageContext;

        private readonly BlockCache _blockCache;
        private readonly BlockHeaderCache _blockHeaderCache;
        private readonly ChainedBlockCache _chainedBlockCache;
        private readonly TxKeyCache _txKeyCache;
        private readonly TransactionCache _transactionCache;

        public CacheContext(IStorageContext storageContext)
        {
            this._storageContext = storageContext;

            this._blockCache = new BlockCache
            (
                cacheContext: this,
                maxFlushMemorySize: 1.MILLION(),
                maxCacheMemorySize: 1.MILLION()
            );

            this._blockHeaderCache = new BlockHeaderCache
            (
                cacheContext: this,
                maxFlushMemorySize: 0,
                maxCacheMemorySize: 1.MILLION()
            );

            this._chainedBlockCache = new ChainedBlockCache
            (
                cacheContext: this,
                maxFlushMemorySize: 1.MILLION(),
                maxCacheMemorySize: 1.MILLION()
            );

            this._txKeyCache = new TxKeyCache
            (
                cacheContext: this,
                maxFlushMemorySize: 0,
                maxCacheMemorySize: 1.MILLION()
            );

            this._transactionCache = new TransactionCache
            (
                cacheContext: this,
                maxCacheMemorySize: 1.MILLION()
            );
        }

        public IStorageContext StorageContext { get { return this._storageContext; } }

        public BlockCache BlockCache { get { return this._blockCache; } }

        public BlockHeaderCache BlockHeaderCache { get { return this._blockHeaderCache; } }

        public ChainedBlockCache ChainedBlockCache { get { return this._chainedBlockCache; } }

        public TxKeyCache TxKeyCache { get { return this._txKeyCache; } }

        public TransactionCache TransactionCache { get { return this._transactionCache; } }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._blockCache,
                this._blockHeaderCache,
                this._chainedBlockCache,
                this._txKeyCache,
                this._transactionCache
            }.DisposeList();
        }

        //TODO
        public void WaitForFlush()
        {
        }
    }
}
