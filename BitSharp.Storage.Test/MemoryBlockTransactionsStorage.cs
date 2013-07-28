using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryBlockTransactionsStorage : MemoryStorage<UInt256, ImmutableArray<Transaction>>, IBlockTransactionsStorage
    {
        public MemoryBlockTransactionsStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllBlockHashes()
        {
            return this.Storage.Keys;
        }
    }
}
