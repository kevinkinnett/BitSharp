using BitSharp.Common;
using BitSharp.WireProtocol;
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

        Blockchain GenesisBlockchain { get; }

        BigInteger CalculateWork(BlockHeader blockHeader);

        void ValidateBlock(Block block, Blockchain blockchain, IBlockchainRetriever retriever);

        BlockMetadata SelectWinningBlockchain(IEnumerable<BlockMetadata> candidateBlockchains);
    }
}
