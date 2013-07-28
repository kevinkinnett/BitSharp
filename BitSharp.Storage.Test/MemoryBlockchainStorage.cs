using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryBlockchainStorage : IBlockchainStorage
    {
        private readonly MemoryStorageContext _storageContext;

        public MemoryBlockchainStorage(MemoryStorageContext storageContext)
        {
            this._storageContext = storageContext;
        }

        public MemoryStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
        }

        public IEnumerable<Tuple<BlockchainKey, BlockchainMetadata>> ListBlockchains()
        {
            yield break;
        }

        public Blockchain ReadBlockchain(BlockchainKey chainedBlock)
        {
            throw new NotImplementedException();
        }

        public BlockchainKey WriteBlockchain(Blockchain blockchain)
        {
            throw new NotImplementedException();
        }

        public void RemoveBlockchains(BigInteger lessThanTotalWork)
        {
            throw new NotImplementedException();
        }
    }
}
