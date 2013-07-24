using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.Firebird;
using BitSharp.Storage.Firebird.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Network;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Data;
using System.Data.SqlClient;
using System.Collections.Immutable;
using FirebirdSql.Data.FirebirdClient;

namespace BitSharp.Storage.Firebird
{
    public class TransactionStorage : SqlDataStorage, ITransactionStorage
    {
        public TransactionStorage(FirebirdStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(UInt256 txHash, out Transaction transaction)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxBytes
                    FROM BlockTransactions
                    WHERE TxHash = @txHash";

                cmd.Parameters.SetValue("@txHash", FbDbType.Char, FbCharset.Octets, 32).Value = txHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var txBytes = reader.GetBytes(0);

                        transaction = StorageEncoder.DecodeTransaction(txBytes.ToMemoryStream(), txHash);
                        return true;
                    }
                    else
                    {
                        transaction = default(Transaction);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Transaction>>> values)
        {
            throw new NotSupportedException();
        }

        public void Truncate()
        {
            throw new NotSupportedException();
        }
    }
}
