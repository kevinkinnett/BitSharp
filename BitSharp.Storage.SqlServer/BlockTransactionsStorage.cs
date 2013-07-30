using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.SqlServer;
using BitSharp.Storage.SqlServer.ExtensionMethods;
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
                    SELECT TxHash, TxBytes
                    FROM BlockTransactions
                    WHERE BlockHash = @blockHash
                    ORDER BY TxIndex ASC";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

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
            try
            {
                using (var conn = this.OpenConnection())
                using (var trans = conn.BeginTransaction())
                using (var cmd = conn.CreateCommand())
                {
                    // give writes low deadlock priority, a flush can always be retried
                    using (var deadlockCmd = conn.CreateCommand())
                    {
                        deadlockCmd.Transaction = trans;
                        deadlockCmd.CommandText = "SET DEADLOCK_PRIORITY LOW";
                        deadlockCmd.ExecuteNonQuery();
                    }

                    cmd.Transaction = trans;

                    cmd.CommandText = @"
                        MERGE BlockTransactions AS target
                        USING (SELECT @blockHash, @txHash) AS source (BlockHash, TxHash)
                        ON (target.BlockHash = source.BlockHash AND target.TxHash = source.TxHash)
	                    WHEN NOT MATCHED THEN
	                        INSERT ( BlockHash, TxIndex, TxHash, TxBytes )
	                        VALUES ( @blockHash, @txIndex, @txHash, @txBytes );";

                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@txIndex", SqlDbType = SqlDbType.Int });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@txHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@txBytes", SqlDbType = SqlDbType.VarBinary });

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

                            cmd.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();

                    return true;
                }
            }
            catch (SqlException e)
            {
                if (e.IsDeadlock() || e.IsTimeout())
                    return false;
                else
                    throw;
            }
        }

        public void Truncate()
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    DELETE FROM Blocks";

                cmd.ExecuteNonQuery();
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
                    WHERE EXISTS(SELECT * FROM BlockTransactions WHERE BlockTransactions.BlockHash = BlockHeaders.BlockHash)";

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
}
