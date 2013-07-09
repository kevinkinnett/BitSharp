using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface ITransactionStorage : IDataStorage<UInt256, Transaction>
    {
        IEnumerable<TxOutputKey> ReadUtxo(Guid guid, UInt256 rootBlockHash);

        void WriteUtxo(Guid guid, UInt256 rootBlockHash, IImmutableSet<TxOutputKey> utxo);
    }
}
