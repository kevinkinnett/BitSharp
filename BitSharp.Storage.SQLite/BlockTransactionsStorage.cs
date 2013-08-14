using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Storage.SQLite.ExtensionMethods;
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
using System.Data.SQLite;
using System.IO;

namespace BitSharp.Storage.SQLite
{
    public class BlockTransactionsStorage : SqlDataStorage, IBlockTransactionsStorage
    {
        public BlockTransactionsStorage(SQLiteStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(UInt256 blockHash, out ImmutableArray<Transaction> blockTransactions)
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash, TxBytes
                    FROM BlockTransactions
                    WHERE BlockHash = @blockHash
                    ORDER BY TxIndex ASC";

                cmd.Parameters.SetValue("@blockHash", DbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    var blockTransactionsBuilder = ImmutableArray.CreateBuilder<Transaction>();

                    while (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        var txBytes = reader.GetBytes(1);

                        blockTransactionsBuilder.Add(StorageEncoder.DecodeTransaction(txBytes.ToMemoryStream(), txHash));
                    }

                    blockTransactions = blockTransactionsBuilder.ToImmutable();
                    return blockTransactions.Length > 0;
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<ImmutableArray<Transaction>>>> values)
        {
            var stopwatch = new Stopwatch();
            var count = 0;
            try
            {
                using (var conn = this.OpenWriteConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                    INSERT OR IGNORE
                    INTO BlockTransactions ( BlockHash, TxIndex, TxHash, TxBytes )
	                VALUES ( @blockHash, @txIndex, @txHash, @txBytes )";

                    cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@txIndex", DbType = DbType.Int32 });
                    cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@txHash", DbType = DbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@txBytes", DbType = DbType.Binary });

                    foreach (var keyPair in values)
                    {
                        var blockHash = keyPair.Key;

                        cmd.Parameters["@blockHash"].Value = blockHash.ToDbByteArray();

                        for (var txIndex = 0; txIndex < keyPair.Value.Value.Length; txIndex++)
                        {
                            var tx = keyPair.Value.Value[txIndex];
                            var txBytes = StorageEncoder.EncodeTransaction(tx);

                            cmd.Parameters["@txIndex"].Value = txIndex;
                            cmd.Parameters["@txHash"].Value = tx.Hash.ToDbByteArray();
                            cmd.Parameters["@txBytes"].Size = txBytes.Length;
                            cmd.Parameters["@txBytes"].Value = txBytes;

                            count++;
                            cmd.ExecuteNonQuery();
                        }
                    }

                    stopwatch.Start();
                    conn.Commit();
                    stopwatch.Stop();

                    return true;
                }
            }
            finally
            {
                Debug.WriteLine("flushed {0,5}: {1:#,##0.000000}s @ {2:#,##0.000}/s".Format2(count, stopwatch.ElapsedSecondsFloat(), count / stopwatch.ElapsedSecondsFloat()));
            }
        }

        public void Truncate()
        {
            using (var conn = this.OpenWriteConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    DELETE FROM BlockTransactions";

                cmd.ExecuteNonQuery();

                conn.Commit();
            }
        }

        public IEnumerable<UInt256> ReadAllBlockHashes()
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT DISTINCT BlockHash
                    FROM BlockTransactions";

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

        public bool TryReadTransaction(TxKey txKey, out Transaction transaction)
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash, TxBytes
                    FROM BlockTransactions
                    WHERE BlockHash = @blockHash AND TxIndex = @txIndex";

                cmd.Parameters.SetValue("@blockHash", DbType.Binary, 32).Value = txKey.BlockHash.ToDbByteArray();
                cmd.Parameters.SetValue("@txIndex", DbType.Int32).Value = txKey.TxIndex.ToIntChecked();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        var txBytes = reader.GetBytes(1);

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
    }

    /*
    public class BlockTransactionsStorage : SqlDataStorage, IBlockTransactionsStorage
    {
        public BlockTransactionsStorage(SQLiteStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(UInt256 blockHash, out ImmutableArray<Transaction> blockTransactions)
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT MinTxIndex, MaxTxIndex, TxChunkBytes
                    FROM BlockTransactionsChunked
                    WHERE BlockHash = @blockHash
                    ORDER BY MinTxIndex ASC";

                cmd.Parameters.SetValue("@blockHash", DbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    var blockTransactionsBuilder = ImmutableArray.CreateBuilder<Transaction>();

                    while (reader.Read())
                    {
                        var minTxIndex = reader.GetInt32(0);
                        var maxTxIndex = reader.GetInt32(1);
                        var txChunkBytes = reader.GetBytes(2);

                        var txChunkStream = txChunkBytes.ToMemoryStream();
                        for (var i = minTxIndex; i <= maxTxIndex; i++)
                        {
                            blockTransactionsBuilder.Add(StorageEncoder.DecodeTransaction(txChunkStream));
                        }
                    }

                    blockTransactions = blockTransactionsBuilder.ToImmutable();
                    return blockTransactions.Length > 0;
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<ImmutableArray<Transaction>>>> values)
        {
            using (var conn = this.OpenWriteConnection())
            using (var cmd = conn.CreateCommand())
            using (var deleteCmd = conn.CreateCommand())
            {
                deleteCmd.CommandText = @"
                    DELETE FROM BlockTransactionsChunked
                    WHERE BlockHash = @blockHash";
                
                deleteCmd.Parameters.Add(new SQLiteParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });

                cmd.CommandText = @"
                    INSERT
                    INTO BlockTransactionsChunked ( BlockHash, MinTxIndex, MaxTxIndex, TxChunkBytes )
	                VALUES ( @blockHash, @minTxIndex, @maxTxIndex, @txChunkBytes );";

                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@minTxIndex", DbType = DbType.Int32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@maxTxIndex", DbType = DbType.Int32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@txChunkBytes", DbType = DbType.Binary });

                var chunkSize = 10.THOUSAND();
                var maxChunkSize = 1.MILLION();
                var chunk = new byte[maxChunkSize];

                foreach (var keyPair in values)
                {
                    var blockHash = keyPair.Key;

                    deleteCmd.Parameters["@blockHash"].Value = blockHash.ToDbByteArray();
                    deleteCmd.ExecuteNonQuery();

                    cmd.Parameters["@blockHash"].Value = blockHash.ToDbByteArray();

                    var minTxIndex = 0;
                    var maxTxIndex = 0;
                    var chunkOffset = 0;
                    for (var txIndex = 0; txIndex < keyPair.Value.Value.Length; txIndex++)
                    {
                        var tx = keyPair.Value.Value[txIndex];
                        var txBytes = StorageEncoder.EncodeTransaction(tx);

                        if (txBytes.Length > maxChunkSize)
                            throw new Exception();

                        if (chunkOffset + txBytes.Length > chunkSize && chunkOffset > 0)
                        {
                            var dbChunk1 = new byte[chunkOffset];
                            Buffer.BlockCopy(chunk, 0, dbChunk1, 0, chunkOffset);
                            cmd.Parameters["@minTxIndex"].Value = minTxIndex;
                            cmd.Parameters["@maxTxIndex"].Value = maxTxIndex;
                            cmd.Parameters["@txChunkBytes"].Size = dbChunk1.Length;
                            cmd.Parameters["@txChunkBytes"].Value = dbChunk1;
                            cmd.ExecuteNonQuery();

                            chunkOffset = 0;
                            minTxIndex = txIndex;
                        }

                        maxTxIndex = txIndex;
                        Buffer.BlockCopy(txBytes, 0, chunk, chunkOffset, txBytes.Length);
                        chunkOffset += txBytes.Length;
                    }

                    var dbChunk2 = new byte[chunkOffset];
                    Buffer.BlockCopy(chunk, 0, dbChunk2, 0, chunkOffset);
                    cmd.Parameters["@minTxIndex"].Value = minTxIndex;
                    cmd.Parameters["@maxTxIndex"].Value = maxTxIndex;
                    cmd.Parameters["@txChunkBytes"].Size = dbChunk2.Length;
                    cmd.Parameters["@txChunkBytes"].Value = dbChunk2;
                    cmd.ExecuteNonQuery();
                }

                conn.Commit();

                return true;
            }
        }

        public void Truncate()
        {
            using (var conn = this.OpenWriteConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    DELETE FROM BlockTransactionsChunked";

                cmd.ExecuteNonQuery();

                conn.Commit();
            }
        }

        public IEnumerable<UInt256> ReadAllBlockHashes()
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM BlockHeaders
                    WHERE EXISTS(SELECT * FROM BlockTransactionsChunked WHERE BlockTransactionsChunked.BlockHash = BlockHeaders.BlockHash)";

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
    }
    */
}
