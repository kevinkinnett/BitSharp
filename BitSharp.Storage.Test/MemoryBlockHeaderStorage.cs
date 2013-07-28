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
    public class MemoryBlockHeaderStorage : MemoryStorage<UInt256, BlockHeader>, IBlockHeaderStorage
    {
        public MemoryBlockHeaderStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }
    }
}
