using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Storage.Firebird.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.WireProtocol;
using System.Data.SqlClient;
using System.Data.Common;
using BitSharp.Blockchain;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using System.Globalization;
using System.Collections.Immutable;
using BitSharp.Data;

namespace BitSharp.Storage.Firebird
{
    public class TxKeyStorage : SqlDataStorage, ITxKeyStorage
    {
        public TxKeyStorage(FirebirdStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(TxKeySearch txKeySearch, out TxKey txKey)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, TransactionIndex
                    FROM TransactionLocators
                    WHERE TransactionHash = @txHash";

                cmd.Parameters.SetValue("@txHash", FbDbType.Char, FbCharset.Octets, 32).Value = txKeySearch.TxHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var txIndex = reader.GetUInt32(1);

                        if (txKeySearch.BlockHashes.Contains(blockHash))
                        {
                            txKey = new TxKey(txKeySearch.TxHash, blockHash, txIndex);
                            return true;
                        }
                    }

                    txKey = default(TxKey);
                    return false;
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<TxKeySearch, WriteValue<TxKey>>> txKeys)
        {
            throw new NotSupportedException();
        }
    }
}
