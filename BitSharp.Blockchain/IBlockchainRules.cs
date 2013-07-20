using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public interface IBlockchainRules
    {
        UInt256 HighestTarget { get; }

        UInt32 HighestTargetBits { get; }

        Block GenesisBlock { get; }

        BlockMetadata GenesisBlockMetadata { get; }

        Data.Blockchain GenesisBlockchain { get; }

        void ValidateBlock(Block block, Data.Blockchain blockchain, ImmutableDictionary<UInt256, Transaction> transactions);

        BlockMetadata SelectWinningBlockchain(IEnumerable<BlockMetadata> candidateBlockchains);
    }
}
