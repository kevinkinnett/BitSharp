using BitSharp.Common;
using BitSharp.Storage;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    public class MemoryTransactionStorage : MemoryStorage<UInt256, Transaction>, ITransactionStorage
    {
    }
}
