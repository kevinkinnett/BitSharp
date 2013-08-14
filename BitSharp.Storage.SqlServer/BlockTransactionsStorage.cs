using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Storage.SqlServer.ExtensionMethods;
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
using System.IO;

namespace BitSharp.Storage.SqlServer
{
    public class BlockTransactionsStorage : SqlDataStorage, IBlockTransactionsStorage
    {
        public BlockTransactionsStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(UInt256 blockHash, out ImmutableArray<Transaction> blockTransactions)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT MinTxIndex, MaxTxIndex, TxChunkBytes
                    FROM BlockTransactionsChunked
                    WHERE BlockHash = @blockHash
                    ORDER BY MinTxIndex ASC";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

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
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            using (var deleteCmd = conn.CreateCommand())
            {
                // give writes low deadlock priority, a flush can always be retried
                using (var deadlockCmd = conn.CreateCommand())
                {
                    deadlockCmd.Transaction = trans;
                    deadlockCmd.CommandText = "SET DEADLOCK_PRIORITY LOW";
                    deadlockCmd.ExecuteNonQuery();
                }

                deleteCmd.Transaction = trans;
                cmd.Transaction = trans;

                deleteCmd.CommandText = @"
                    DELETE FROM BlockTransactionsChunked
                    WHERE BlockHash = @blockHash";

                deleteCmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });

                cmd.CommandText = @"
                    INSERT
                    INTO BlockTransactionsChunked ( BlockHash, MinTxIndex, MaxTxIndex, TxChunkBytes )
	                VALUES ( @blockHash, @minTxIndex, @maxTxIndex, @txChunkBytes );";

                cmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SqlParameter { ParameterName = "@minTxIndex", DbType = DbType.Int32 });
                cmd.Parameters.Add(new SqlParameter { ParameterName = "@maxTxIndex", DbType = DbType.Int32 });
                cmd.Parameters.Add(new SqlParameter { ParameterName = "@txChunkBytes", DbType = DbType.Binary });

                var chunkSize = 100.THOUSAND();
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

                trans.Commit();

                return true;
            }
        }

        public void Truncate()
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;

                cmd.CommandText = @"
                    DELETE FROM BlockTransactionsChunked";

                cmd.ExecuteNonQuery();

                trans.Commit();
            }
        }

        public IEnumerable<UInt256> ReadAllBlockHashes()
        {
            using (var conn = this.OpenConnection())
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


        public bool TryReadTransaction(TxKey txKey, out Transaction transaction)
        {
            throw new NotImplementedException();
        }
    }
}
