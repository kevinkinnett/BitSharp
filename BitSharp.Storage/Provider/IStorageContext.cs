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

        IBlockMetadataStorage BlockMetadataStorage { get; }

        ITxKeyStorage TxKeyStorage { get; }

        IBlockchainStorage BlockchainStorage { get; }
    }
}
