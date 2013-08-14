using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.SQLite
{
    public class SQLiteStorageContext : IStorageContext
    {
        private readonly BlockHeaderStorage _blockHeaderStorage;
        private readonly BlockTransactionsStorage _blockTransactionsStorage;
        private readonly ChainedBlockStorage _chainedBlockStorage;
        private readonly BlockchainStorage _blockchainStorage;

        public SQLiteStorageContext()
        {
            this._blockHeaderStorage = new BlockHeaderStorage(this);
            this._blockTransactionsStorage = new BlockTransactionsStorage(this);
            this._chainedBlockStorage = new ChainedBlockStorage(this);
            this._blockchainStorage = new BlockchainStorage(this);
        }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public BlockTransactionsStorage BlockTransactionsStorage { get { return this._blockTransactionsStorage; } }

        public ChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public BlockchainStorage BlockchainStorage { get { return this._blockchainStorage; } }

        IBlockHeaderStorage IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBlockTransactionsStorage IStorageContext.BlockTransactionsStorage { get { return this._blockTransactionsStorage; } }

        IChainedBlockStorage IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return this._blockchainStorage; } }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._blockHeaderStorage,
                this._blockTransactionsStorage,
                this._chainedBlockStorage,
                this._blockchainStorage
            }.DisposeList();
        }
    }
}
