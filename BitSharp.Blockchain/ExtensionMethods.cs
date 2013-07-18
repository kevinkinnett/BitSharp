using BitSharp.Common;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Blockchain;
using BitSharp.Data;
using BitSharp.Storage;

namespace BitSharp.Blockchain.ExtensionMethods
{
    internal static class BlockchainExtensionMethods
    {
        public static Block GetBlock(this CacheContext cacheContext, UInt256 blockHash, bool saveInCache = true)
        {
            Block block;
            if (!cacheContext.BlockCache.TryGetValue(blockHash, out block, saveInCache))
            {
                throw new MissingDataException(DataType.Block, blockHash);
            }

            return block;
        }

        public static BlockHeader GetBlockHeader(this CacheContext cacheContext, UInt256 blockHash, bool saveInCache = true)
        {
            BlockHeader blockHeader;
            if (!cacheContext.BlockHeaderCache.TryGetValue(blockHash, out blockHeader, saveInCache))
            {
                throw new MissingDataException(DataType.BlockHeader, blockHash);
            }

            return blockHeader;
        }

        public static BlockMetadata GetBlockMetadata(this CacheContext cacheContext, UInt256 blockHash, bool saveInCache = true)
        {
            BlockMetadata blockMetadata;
            if (!cacheContext.BlockMetadataCache.TryGetValue(blockHash, out blockMetadata, saveInCache))
            {
                throw new MissingDataException(DataType.BlockMetadata, blockHash);
            }

            return blockMetadata;
        }

        public static Transaction GetTransaction(this CacheContext cacheContext, TxKeySearch txKeySearch, bool saveInCache = true)
        {
            Transaction transaction;
            if (!cacheContext.TransactionCache.TryGetValue(txKeySearch, out transaction, saveInCache))
            {
                throw new MissingDataException(DataType.Transaction, txKeySearch.TxHash);
            }

            return transaction;
        }
    }
}
