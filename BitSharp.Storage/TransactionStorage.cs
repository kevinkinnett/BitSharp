using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class TransactionStorage : IUnboundedStorage<TxKey, Transaction>
    {
        private readonly CacheContext _cacheContext;

        public TransactionStorage(CacheContext cacheContext)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public void Dispose()
        {
        }

        public bool TryReadValue(TxKey key, out Transaction value)
        {
            return this.StorageContext.BlockTransactionsStorage.TryReadTransaction(key, out value);

            //Block block;
            //if (this.CacheContext.BlockCache.TryGetValue(key.BlockHash, out block))
            //{
            //    this.CacheContext.TransactionCache.CacheBlock(block);

            //    if (key.TxIndex >= block.Transactions.Length)
            //        throw new Exception();

            //    var tx = block.Transactions[key.TxIndex.ToIntChecked()];
            //    if (tx.Hash != key.TxHash)
            //        throw new Exception();

            //    value = tx;
            //    return true;
            //}
            //else
            //{
            //    throw new MissingDataException(DataType.Block, key.BlockHash);
            //}
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<TxKey, WriteValue<Transaction>>> values)
        {
            throw new NotImplementedException();
        }
    }
}
