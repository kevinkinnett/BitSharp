using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IStorageContext : IDisposable
    {
        IBlockStorage BlockStorage { get; }

        IBlockHeaderStorage BlockHeaderStorage { get; }

        IBlockTransactionsStorage BlockTransactionsStorage { get; }

        ITransactionStorage TransactionStorage { get; }

        IChainedBlockStorage ChainedBlockStorage { get; }

        IBlockchainStorage BlockchainStorage { get; }
    }
}
