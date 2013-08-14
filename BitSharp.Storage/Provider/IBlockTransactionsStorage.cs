using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockTransactionsStorage : IUnboundedStorage<UInt256, ImmutableArray<Transaction>>
    {
        IEnumerable<UInt256> ReadAllBlockHashes();

        bool TryReadTransaction(TxKey txKey, out Transaction transaction);
    }
}
