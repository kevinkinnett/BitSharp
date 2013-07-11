using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public interface IBlockchainStorage
    {
        IEnumerable<Tuple<BlockchainKey, BlockchainMetadata>> ListBlockchains();

        Blockchain ReadBlockchain(BlockchainKey blockMetadata);

        BlockchainKey WriteBlockchain(Blockchain blockchain);
    }
}
