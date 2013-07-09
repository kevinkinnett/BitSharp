using BitSharp.Common;
using BitSharp.Storage;
using BitSharp.Database.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.WireProtocol;
using System.Data.SqlClient;
using System.Data.Common;
using System.Data.SQLite;
using System.Collections.Immutable;
using BitSharp.Blockchain;

namespace BitSharp.Database
{
    public class TransactionStorage : SqlDataStorage, ITransactionStorage
    {
        public void WriteUtxo(Guid guid, UInt256 rootBlockHash, IImmutableSet<TxOutputKey> utxo)
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = trans.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT
                    INTO UtxoData (Guid, RootBlockhash, PreviousTransactionHash, PreviousTransactionOutputIndex)
                    VALUES (@guid, @rootBlockHash, @previousTransactionHash, @previousTransactionOutputIndex)";

                foreach (var output in utxo)
                {
                    cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = guid.ToByteArray();
                    cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = rootBlockHash.ToDbByteArray();
                    cmd.Parameters.SetValue("@previousTransactionHash", System.Data.DbType.Binary, 32).Value = output.previousTransactionHash.ToDbByteArray();
                    cmd.Parameters.SetValue("@previousTransactionOutputIndex", System.Data.DbType.Binary, 4).Value = ((UInt32)output.previousOutputIndex).ToDbByteArray();

                    cmd.ExecuteNonQuery();
                }

                trans.Commit();
            }
        }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            Debug.WriteLine(new string('*', 80));
            Debug.WriteLine("EXPENSIVE OPERATION: TransactionStorage.GetAllKeys");
            Debug.WriteLine(new string('*', 80));

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TransactionHash
                    FROM Tranactions";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        yield return blockHash;
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<UInt256, Transaction>> ReadAllValues()
        {
            Debug.WriteLine(new string('*', 80));
            Debug.WriteLine("EXPENSIVE OPERATION: TransactionStorage.GetAllValues");
            Debug.WriteLine(new string('*', 80));

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TransactionHash, TransactionData
                    FROM Transactions";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        var txBytes = reader.GetBytes(1);

                        var tx = Transaction.FromRawBytes(txBytes, txHash);

                        yield return new KeyValuePair<UInt256, Transaction>(txHash, tx);
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 txHash, out Transaction tx)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TransactionData
                    FROM Transactions
                    WHERE TransactionHash = @txHash";

                cmd.Parameters.SetValue("@txHash", System.Data.DbType.Binary, 32).Value = txHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var txBytes = reader.GetBytes(0);
                        tx = Transaction.FromRawBytes(txBytes, txHash);
                        return true;
                    }
                    else
                    {
                        tx = default(Transaction);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Transaction>>> values)
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = trans.CreateCommand())
            {
                foreach (var keyPair in values)
                {
                    cmd.CommandText = keyPair.Value.IsCreate ? CREATE_QUERY : UPDATE_QUERY;

                    var tx = keyPair.Value.Value;

                    var txBytes = tx.ToRawBytes();
                    cmd.Parameters.SetValue("@txHash", System.Data.DbType.Binary, 32).Value = tx.Hash.ToDbByteArray();
                    cmd.Parameters.SetValue("@txBytes", System.Data.DbType.Binary, txBytes.Length).Value = txBytes;

                    cmd.ExecuteNonQuery();
                }

                trans.Commit();
                return true;
            }
        }

#if SQLITE
        private const string CREATE_QUERY = @"
            INSERT OR IGNORE
            INTO TRANSACTIONS (TransactionHash, TransactionData)
	        VALUES (@txHash, @txBytes)";

        private const string UPDATE_QUERY = @"
            INSERT OR REPLACE
            INTO TRANSACTIONS (TransactionHash, TransactionData)
	        VALUES (@txHash, @txBytes)";

#elif SQL_SERVER
        private const string CREATE_QUERY = @"
            MERGE Transactions AS target
            USING (SELECT @txHash) AS source (TransactionHash)
            ON (target.TransactionHash = source.TransactionHash)
	        WHEN NOT MATCHED THEN	
	            INSERT (TransactionHash, TransactionData)
	            VALUES (@txHash, @txBytes);";

        private const string UPDATE_QUERY = @"
            MERGE Transactions AS target
            USING (SELECT @txHash) AS source (TransactionHash)
            ON (target.TransactionHash = source.TransactionHash)
	        WHEN NOT MATCHED THEN	
	            INSERT (TransactionHash, TransactionData)
	            VALUES (@txHash, @txBytes)
	        WHEN MATCHED THEN	
	            UPDATE SET TransactionHash = @txHash, TransactionData = @txBytes;";
#endif
    }
}
