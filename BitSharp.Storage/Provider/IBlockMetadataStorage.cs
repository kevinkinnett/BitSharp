using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockMetadataStorage : IBoundedStorage<UInt256, BlockMetadata>
    {
        IEnumerable<BlockMetadata> FindWinningChainedBlocks();

        Dictionary<UInt256, HashSet<UInt256>> FindUnchainedBlocksByPrevious();

        Dictionary<BlockMetadata, HashSet<BlockMetadata>> FindChainedWithProceedingUnchained(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata);

        IEnumerable<UInt256> FindMissingPreviousBlocks();

        IEnumerable<UInt256> FindMissingBlocks();
    }
}
