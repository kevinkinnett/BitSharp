using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
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
using System.IO;
using System.ServiceModel.Channels;
using System.Threading;

namespace BitSharp.Database
{
    public class TransactionStorage : SqlDataStorage, ITransactionStorage
    {
        public IImmutableDictionary<TxOutputKey, object> ReadUtxo(Guid guid, UInt256 rootBlockHash)
        {
            var utxoBuilder = ImmutableDictionary.CreateBuilder<TxOutputKey, object>();

            foreach (var chunkBytes in ReadUtxoChunkBytes(guid, rootBlockHash))
            {
                using (var chunkStream = new MemoryStream(chunkBytes))
                {
                    var chunkReader = new WireReader(chunkStream);

                    var chunkLength = chunkReader.ReadVarInt().ToIntChecked();

                    var outputs = new KeyValuePair<TxOutputKey, object>[chunkLength];

                    for (var i = 0; i < chunkLength; i++)
                    {
                        var prevTxHash = chunkReader.Read32Bytes();
                        var prevTxOutputIndex = chunkReader.Read4Bytes();

                        outputs[i] = new KeyValuePair<TxOutputKey, object>(new TxOutputKey(prevTxHash, prevTxOutputIndex.ToIntChecked()), null);
                    }

                    utxoBuilder.AddRange(outputs);
                }
            }

            return utxoBuilder.ToImmutable();
        }

        public IEnumerable<byte[]> ReadUtxoChunkBytes(Guid guid, UInt256 rootBlockHash)
        {
#if SQLITE
            using (var conn = this.OpenUtxoConnection(guid))
#elif SQL_SERVER
            using (var conn = this.OpenConnection())
#endif
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT UtxoChunkBytes
                    FROM UtxoData
                    WHERE Guid = @guid AND RootBlockHash = @rootBlockHash";

                //cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = guid.ToByteArray();
                cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = rootBlockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return reader.GetBytes(0);
                    }
                }
            }
        }

        public void WriteUtxo(Guid guid, UInt256 rootBlockHash, IImmutableDictionary<TxOutputKey, object> utxo)
        {
#if SQLITE
            using (var conn = this.OpenUtxoConnection(guid))
#elif SQL_SERVER
            using (var conn = this.OpenConnection())
#endif
            using (var trans = conn.BeginTransaction())
            using (var cmd = trans.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT
                    INTO UtxoData (Guid, RootBlockhash, UtxoChunkBytes)
                    VALUES (@guid, @rootBlockHash, @utxoChunkBytes)";

                cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = guid.ToByteArray();
                cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = rootBlockHash.ToDbByteArray();

                var chunkSize = 1820; // target 65,529 byte size
                var currentOffset = 0;

                using (var utxoEnumerator = utxo.GetEnumerator())
                {
                    // chunk outer loop
                    while (currentOffset < utxo.Count)
                    {
                        var chunkLength = Math.Min(chunkSize, utxo.Count - currentOffset);

                        // varint is up to 9 bytes and txoutputkey is 36 bytes
                        var chunkBytes = new byte[9 + (36 * chunkSize)];
                        var chunkStream = new MemoryStream(chunkBytes);
                        var chunkWriter = new WireWriter(chunkStream);
                        chunkWriter.WriteVarInt((UInt32)chunkLength);

                        // chunk inner loop
                        for (var i = 0; i < chunkLength; i++)
                        {
                            // get the next output from the utxo
                            if (!utxoEnumerator.MoveNext())
                                throw new Exception();

                            var output = utxoEnumerator.Current.Key;
                            chunkWriter.Write32Bytes(output.previousTransactionHash);
                            chunkWriter.Write4Bytes((UInt32)output.previousOutputIndex);
                        }

                        cmd.Parameters.SetValue("@utxoChunkBytes", System.Data.DbType.Binary, chunkBytes.Length).Value = chunkBytes;

                        // write the chunk
                        cmd.ExecuteNonQuery();

                        currentOffset += chunkLength;
                    }

                    // there should be no items left in utxo at this point
                    if (utxoEnumerator.MoveNext())
                        throw new Exception();
                }

                // commit the transaction
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
