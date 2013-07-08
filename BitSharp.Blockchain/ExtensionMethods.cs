using BitSharp.Common;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.ExtensionMethods
{
    internal static class BlockchainExtensionMethods
    {
        public static Block GetBlock(this IBlockchainRetriever retriever, UInt256 blockHash, bool saveInCache = true)
        {
            Block block;
            if (!retriever.TryGetBlock(blockHash, out block, saveInCache))
            {
                throw new MissingDataException(DataType.Block, blockHash);
            }

            return block;
        }

        public static BlockHeader GetBlockHeader(this IBlockchainRetriever retriever, UInt256 blockHash, bool saveInCache = true)
        {
            BlockHeader blockHeader;
            if (!retriever.TryGetBlockHeader(blockHash, out blockHeader, saveInCache))
            {
                throw new MissingDataException(DataType.BlockHeader, blockHash);
            }

            return blockHeader;
        }

        public static BlockMetadata GetBlockMetadata(this IBlockchainRetriever retriever, UInt256 blockHash, bool saveInCache = true)
        {
            BlockMetadata blockMetadata;
            if (!retriever.TryGetBlockMetadata(blockHash, out blockMetadata, saveInCache))
            {
                throw new MissingDataException(DataType.BlockMetadata, blockHash);
            }

            return blockMetadata;
        }

        public static Transaction GetTransaction(this IBlockchainRetriever retriever, UInt256 transactionHash, bool saveInCache = true)
        {
            Transaction transaction;
            if (!retriever.TryGetTransaction(transactionHash, out transaction, saveInCache))
            {
                throw new MissingDataException(DataType.Transaction, transactionHash);
            }

            return transaction;
        }

    }
}
