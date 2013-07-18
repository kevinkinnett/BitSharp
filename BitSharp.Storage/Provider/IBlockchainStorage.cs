using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockchainStorage : IDisposable
    {
        IEnumerable<Tuple<BlockchainKey, BlockchainMetadata>> ListBlockchains();

        Data.Blockchain ReadBlockchain(BlockchainKey blockMetadata);

        BlockchainKey WriteBlockchain(Data.Blockchain blockchain);

        void RemoveBlockchains(BigInteger lessThanTotalWork);
    }
}
