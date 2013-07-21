using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Storage.SqlServer.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Network;
using System.Data.SqlClient;
using System.Data.Common;
using BitSharp.Blockchain;
using System.Data;
using System.Globalization;
using System.Collections.Immutable;
using BitSharp.Data;

namespace BitSharp.Storage.SqlServer
{
    public class TxKeyStorage : SqlDataStorage, ITxKeyStorage
    {
        public TxKeyStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(UInt256 txHash, out HashSet<TxKey> txKeySet)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, TransactionIndex
                    FROM TransactionLocators
                    WHERE TransactionHash = @txHash";

                cmd.Parameters.SetValue("@txHash", SqlDbType.Binary, 32).Value = txHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    txKeySet = new HashSet<TxKey>();

                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var txIndex = reader.GetUInt32(1);

                        txKeySet.Add(new TxKey(txHash, blockHash, txIndex));
                    }

                    return txKeySet.Count > 0;
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<HashSet<TxKey>>>> txKeys)
        {
            throw new NotSupportedException();
        }
    }
}
