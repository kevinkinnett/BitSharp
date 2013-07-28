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
        private readonly MemoryChainedBlockStorage _chainedBlockStorage;
        private readonly MemoryBlockchainStorage _blockchainStorage;

        public MemoryStorageContext()
        {
            this._chainedBlockStorage = new MemoryChainedBlockStorage(this);
            this._blockchainStorage = new MemoryBlockchainStorage(this);
        }

        public MemoryChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public MemoryBlockchainStorage BlockchainStorage { get { return this._blockchainStorage; } }

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


        public IBlockHeaderStorage BlockHeaderStorage
        {
            get { throw new NotImplementedException(); }
        }

        public IBlockTransactionsStorage BlockTransactionsStorage
        {
            get { throw new NotImplementedException(); }
        }

        public ITransactionStorage TransactionStorage
        {
            get { throw new NotImplementedException(); }
        }
    }
}
