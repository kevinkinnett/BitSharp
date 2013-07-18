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
    public class MemoryBlockStorage : MemoryStorage<UInt256, Block>, IBlockStorage
    {
        public MemoryBlockStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllBlockHeaders()
        {
            throw new NotImplementedException();
        }

        public bool TryReadBlockHeader(UInt256 blockHash, out BlockHeader blockHeader)
        {
            throw new NotImplementedException();
        }
    }
}
