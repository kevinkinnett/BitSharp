using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockMetadataStorage : IDataStorage<UInt256, BlockMetadata>
    {
        IEnumerable<BlockMetadata> FindWinningChainedBlocks(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata);

        Dictionary<UInt256, HashSet<UInt256>> FindUnchainedBlocksByPrevious();

        Dictionary<BlockMetadata, HashSet<BlockMetadata>> FindChainedWithProceedingUnchained(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata);

        IEnumerable<UInt256> FindMissingPreviousBlocks(IEnumerable<UInt256> knownBlocks, IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata);
    }
}
