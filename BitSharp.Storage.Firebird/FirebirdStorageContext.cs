using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.Firebird;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Firebird
{
    public class FirebirdStorageContext : IStorageContext
    {
        private readonly BlockStorage _blockStorage;
        private readonly BlockHeaderStorage _blockHeaderStorage;
        private readonly BlockTransactionsStorage _blockTransactionsStorage;
        private readonly TransactionStorage _transactionStorage;
        private readonly ChainedBlockStorage _chainedBlockStorage;
        private readonly BlockchainStorage _blockchainStorage;

        public FirebirdStorageContext()
        {
            this._blockStorage = new BlockStorage(this);
            this._blockHeaderStorage = new BlockHeaderStorage(this);
            this._blockTransactionsStorage = new BlockTransactionsStorage(this);
            this._transactionStorage = new TransactionStorage(this);
            this._chainedBlockStorage = new ChainedBlockStorage(this);
            this._blockchainStorage = new BlockchainStorage(this);
        }

        public BlockStorage BlockStorage { get { return this._blockStorage; } }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public BlockTransactionsStorage BlockTransactionsStorage { get { return this._blockTransactionsStorage; } }

        public TransactionStorage TransactionStorage { get { return this._transactionStorage; } }

        public ChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public BlockchainStorage BlockchainStorage { get { return this._blockchainStorage; } }

        IBlockStorage IStorageContext.BlockStorage { get { return this._blockStorage; } }

        IBlockHeaderStorage IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBlockTransactionsStorage IStorageContext.BlockTransactionsStorage { get { return this._blockTransactionsStorage; } }

        ITransactionStorage IStorageContext.TransactionStorage { get { return this._transactionStorage; } }

        IChainedBlockStorage IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return this._blockchainStorage; } }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._blockStorage,
                this._blockHeaderStorage,
                this._blockTransactionsStorage,
                this._transactionStorage,
                this._chainedBlockStorage,
                this._blockchainStorage
            }.DisposeList();
        }
    }
}
