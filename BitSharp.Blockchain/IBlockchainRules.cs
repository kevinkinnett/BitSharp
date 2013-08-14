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

        Block GenesisBlock { get; }

        ChainedBlock GenesisChainedBlock { get; }

        Data.Blockchain GenesisBlockchain { get; }

        void ValidateBlock(Block block, Data.Blockchain blockchain, ImmutableDictionary<UInt256, UnspentTx> utxo, ImmutableDictionary<UInt256, ImmutableHashSet<int>> newTransactions /*, ImmutableDictionary<UInt256, Transaction> transactions*/);

        ChainedBlock SelectWinningChainedBlock(IList<ChainedBlock> leafChainedBlocks);
    }
}
