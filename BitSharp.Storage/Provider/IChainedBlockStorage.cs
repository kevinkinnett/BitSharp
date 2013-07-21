using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IChainedBlockStorage : IBoundedStorage<UInt256, ChainedBlock>
    {
        IEnumerable<UInt256> FindMissingBlocks();

        IEnumerable<ChainedBlock> FindLeafChained();

        IEnumerable<ChainedBlock> FindChainedByPreviousBlockHash(UInt256 previousBlockHash);

        IEnumerable<ChainedBlock> FindChainedWhereProceedingUnchainedExists();

        IEnumerable<BlockHeader> FindUnchainedWherePreviousBlockExists();
    }
}
