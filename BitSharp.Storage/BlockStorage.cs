using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockStorage : IBoundedStorage<UInt256, Block>
    {
        private readonly CacheContext _cacheContext;

        public BlockStorage(CacheContext cacheContext)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public void Dispose()
        {
        }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.StorageContext.BlockTransactionsStorage.ReadAllBlockHashes();
        }

        public IEnumerable<KeyValuePair<UInt256, Block>> ReadAllValues()
        {
            foreach (var blockHeader in this.CacheContext.BlockHeaderCache.StreamAllValues())
            {
                ImmutableArray<Transaction> blockTransactions;
                if (this.StorageContext.BlockTransactionsStorage.TryReadValue(blockHeader.Value.Hash, out blockTransactions))
                {
                    if (blockHeader.Value.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTransactions))
                    {
                        yield return new KeyValuePair<UInt256, Block>(blockHeader.Value.Hash, new Block(blockHeader.Value, blockTransactions));
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 key, out Block value)
        {
            BlockHeader blockHeader;
            if (this.CacheContext.BlockHeaderCache.TryGetValue(key, out blockHeader))
            {
                ImmutableArray<Transaction> blockTransactions;
                if (this.StorageContext.BlockTransactionsStorage.TryReadValue(blockHeader.Hash, out blockTransactions))
                {
                    if (blockHeader.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTransactions))
                    {
                        value = new Block(blockHeader, blockTransactions);
                        return true;
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }
            }

            value = default(Block);
            return false;
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Block>>> values)
        {
            var writeBlockTransactions = new List<KeyValuePair<UInt256, WriteValue<ImmutableArray<Transaction>>>>();

            foreach (var value in values)
            {
                writeBlockTransactions.Add(
                    new KeyValuePair<UInt256, WriteValue<ImmutableArray<Transaction>>>(value.Key,
                        new WriteValue<ImmutableArray<Transaction>>(value.Value.Value.Transactions, value.Value.IsCreate)));
            }

            return this.StorageContext.BlockTransactionsStorage.TryWriteValues(writeBlockTransactions);
        }
    }
}
