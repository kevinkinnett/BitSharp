using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryStorageContext : IStorageContext
    {
        private readonly MemoryBlockStorage _blockStorage;
        private readonly MemoryChainedBlockStorage _chainedBlockStorage;
        private readonly MemoryTxKeyStorage _txKeyStorage;
        private readonly MemoryBlockchainStorage _blockchainStorage;

        public MemoryStorageContext()
        {
            this._blockStorage = new MemoryBlockStorage(this);
            this._chainedBlockStorage = new MemoryChainedBlockStorage(this);
            this._txKeyStorage = new MemoryTxKeyStorage(this);
            this._blockchainStorage = new MemoryBlockchainStorage(this);
        }

        public MemoryBlockStorage BlockStorage { get { return this._blockStorage; } }

        public MemoryChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public MemoryTxKeyStorage TxKeyStorage { get { return this._txKeyStorage; } }

        public MemoryBlockchainStorage BlockchainStorage { get { return this._blockchainStorage; } }

        IBlockStorage IStorageContext.BlockStorage { get { return this._blockStorage; } }

        IChainedBlockStorage IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        ITxKeyStorage IStorageContext.TxKeyStorage { get { return this._txKeyStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return this._blockchainStorage; } }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._blockStorage,
                this._chainedBlockStorage,
                this._txKeyStorage,
                this._blockchainStorage
            }.DisposeList();
        }
    }
}
