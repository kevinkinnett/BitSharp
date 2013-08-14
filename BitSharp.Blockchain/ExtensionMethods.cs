using BitSharp.Common;
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

        public static ChainedBlock GetChainedBlock(this CacheContext cacheContext, UInt256 blockHash, bool saveInCache = true)
        {
            ChainedBlock chainedBlock;
            if (!cacheContext.ChainedBlockCache.TryGetValue(blockHash, out chainedBlock, saveInCache))
            {
                throw new MissingDataException(DataType.ChainedBlock, blockHash);
            }

            return chainedBlock;
        }

        public static Transaction GetTransaction(this CacheContext cacheContext, TxKey txKey, bool saveInCache = true)
        {
            Transaction transaction;
            if (!cacheContext.TransactionCache.TryGetValue(txKey, out transaction, saveInCache))
            {
                throw new MissingDataException(DataType.Transaction, txKey.TxHash);
            }

            return transaction;
        }
    }
}
