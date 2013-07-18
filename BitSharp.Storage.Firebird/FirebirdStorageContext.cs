using BitSharp.Common.ExtensionMethods;
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
        private readonly BlockMetadataStorage _blockMetadataStorage;
        private readonly TxKeyStorage _txKeyStorage;
        private readonly BlockchainStorage _blockchainStorage;

        public FirebirdStorageContext()
        {
            this._blockStorage = new BlockStorage(this);
            this._blockMetadataStorage = new BlockMetadataStorage(this);
            this._txKeyStorage = new TxKeyStorage(this);
            this._blockchainStorage = new BlockchainStorage(this);
        }

        public BlockStorage BlockStorage { get { return this._blockStorage; } }

        public BlockMetadataStorage BlockMetadataStorage { get { return this._blockMetadataStorage; } }

        public TxKeyStorage TxKeyStorage { get { return this._txKeyStorage; } }

        public BlockchainStorage BlockchainStorage { get { return this._blockchainStorage; } }

        IBlockStorage IStorageContext.BlockStorage { get { return this._blockStorage; } }

        IBlockMetadataStorage IStorageContext.BlockMetadataStorage { get { return this._blockMetadataStorage; } }

        ITxKeyStorage IStorageContext.TxKeyStorage { get { return this._txKeyStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return this._blockchainStorage; } }
        
        public void Dispose()
        {
            new IDisposable[]
            {
                this._blockStorage,
                this._blockMetadataStorage,
                this._txKeyStorage,
                this._blockchainStorage
            }.DisposeList();
        }
    }
}
