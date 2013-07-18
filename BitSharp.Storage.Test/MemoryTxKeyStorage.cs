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
    public class MemoryTxKeyStorage : MemoryStorage<TxKeySearch, TxKey>, ITxKeyStorage
    {
        public MemoryTxKeyStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }
   }
}
