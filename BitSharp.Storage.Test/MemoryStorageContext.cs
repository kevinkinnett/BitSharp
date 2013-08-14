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
        private readonly MemoryBlockHeaderStorage _blockHeaderStorage;
        private readonly MemoryBlockTransactionsStorage _blockTransactionsStorage;
        private readonly MemoryChainedBlockStorage _chainedBlockStorage;
        private readonly MemoryBlockchainStorage _blockchainStorage;

        public MemoryStorageContext()
        {
            this._blockHeaderStorage = new MemoryBlockHeaderStorage(this);
            this._blockTransactionsStorage = new MemoryBlockTransactionsStorage(this);
            this._chainedBlockStorage = new MemoryChainedBlockStorage(this);
            this._blockchainStorage = new MemoryBlockchainStorage(this);
        }

        public MemoryBlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public MemoryBlockTransactionsStorage BlockTransactionsStorage { get { return this._blockTransactionsStorage; } }

        public MemoryChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public MemoryBlockchainStorage BlockchainStorage { get { return this._blockchainStorage; } }

        IBlockHeaderStorage IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBlockTransactionsStorage IStorageContext.BlockTransactionsStorage { get { return this._blockTransactionsStorage; } }

        IChainedBlockStorage IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return this._blockchainStorage; } }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._chainedBlockStorage,
                this._blockchainStorage
            }.DisposeList();
        }
    }
}
