using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryChainedBlockStorage : MemoryStorage<UInt256, ChainedBlock>, IChainedBlockStorage
    {
        public MemoryChainedBlockStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> FindMissingBlocks()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ChainedBlock> FindLeafChained()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ChainedBlock> FindChainedByPreviousBlockHash(UInt256 previousBlockHash)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ChainedBlock> FindChainedWhereProceedingUnchainedExists()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<BlockHeader> FindUnchainedWherePreviousBlockExists()
        {
            throw new NotImplementedException();
        }
    }
}
