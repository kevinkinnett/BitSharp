using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockStorage : IBlockStorage
    {
        private readonly IStorageContext _storageContext;

        public BlockStorage(IStorageContext storageContext)
        {
            this._storageContext = storageContext;
        }

        public IStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
        }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.StorageContext.BlockTransactionsStorage.ReadAllBlockHashes();
        }

        public IEnumerable<KeyValuePair<UInt256, Block>> ReadAllValues()
        {
            foreach (var blockHeader in this.StorageContext.BlockHeaderStorage.ReadAllValues())
            {
                ImmutableArray<Transaction> blockTransactions;
                if (this.StorageContext.BlockTransactionsStorage.TryReadValue(blockHeader.Value.Hash, out blockTransactions))
                {
                    if (blockHeader.Value.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTransactions))
                    {
                        yield return new KeyValuePair<UInt256, Block>(blockHeader.Value.Hash, new Block(blockHeader.Value, blockTransactions));
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 key, out Block value)
        {
            BlockHeader blockHeader;
            if (this.StorageContext.BlockHeaderStorage.TryReadValue(key, out blockHeader))
            {
                ImmutableArray<Transaction> blockTransactions;
                if (this.StorageContext.BlockTransactionsStorage.TryReadValue(blockHeader.Hash, out blockTransactions))
                {
                    if (blockHeader.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTransactions))
                    {
                        value = new Block(blockHeader, blockTransactions);
                        return true;
                    }
                }
            }

            value = default(Block);
            return false;
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Block>>> values)
        {
            var writeBlockHeaders = new List<KeyValuePair<UInt256, WriteValue<BlockHeader>>>();
            var writeBlockTransactions = new List<KeyValuePair<UInt256, WriteValue<ImmutableArray<Transaction>>>>();

            foreach (var value in values)
            {
                writeBlockHeaders.Add(new KeyValuePair<UInt256, WriteValue<BlockHeader>>(value.Key, new WriteValue<BlockHeader>(value.Value.Value.Header, value.Value.IsCreate)));
                writeBlockTransactions.Add(new KeyValuePair<UInt256, WriteValue<ImmutableArray<Transaction>>>(value.Key, new WriteValue<ImmutableArray<Transaction>>(value.Value.Value.Transactions, value.Value.IsCreate)));
            }

            return this.StorageContext.BlockHeaderStorage.TryWriteValues(writeBlockHeaders)
                && this.StorageContext.BlockTransactionsStorage.TryWriteValues(writeBlockTransactions);
        }
    }
}
