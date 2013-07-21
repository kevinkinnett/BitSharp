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
using BitSharp.Blockchain;
using BitSharp.Data;
using System.Data.SqlClient;
using System.Data;

namespace BitSharp.Storage.SqlServer
{
    public class ChainedBlockStorage : SqlDataStorage, IChainedBlockStorage
    {
        public ChainedBlockStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM ChainedBlocks";

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

        public IEnumerable<KeyValuePair<UInt256, ChainedBlock>> ReadAllValues()
        {
            Debug.WriteLine(new string('*', 80));
            Debug.WriteLine("EXPENSIVE OPERATION: ChainedBlockSqlStorage.GetAllValues");
            Debug.WriteLine(new string('*', 80));

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, PreviousBlockHash, Height, TotalWork
                    FROM ChainedBlocks";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var previousBlockHash = reader.GetUInt256(1);
                        var height = reader.GetInt32(2);
                        var totalWork = reader.GetBigInteger(3);

                        yield return new KeyValuePair<UInt256, ChainedBlock>
                        (
                            blockHash,
                            new ChainedBlock
                            (
                                blockHash,
                                previousBlockHash,
                                height,
                                totalWork
                            ));
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 blockHash, out ChainedBlock chainedBlock)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT PreviousBlockHash, Height, TotalWork
                    FROM ChainedBlocks
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var previousBlockHash = reader.GetUInt256(0);
                        var height = reader.GetInt32(1);
                        var totalWork = reader.GetBigInteger(2);

                        chainedBlock = new ChainedBlock
                        (
                            blockHash,
                            previousBlockHash,
                            height,
                            totalWork
                        );
                        return true;
                    }
                    else
                    {
                        chainedBlock = default(ChainedBlock);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<ChainedBlock>>> values)
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

                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@previousBlockHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@height", SqlDbType = SqlDbType.Int });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@totalWork", SqlDbType = SqlDbType.Binary, Size = 64 });

                    foreach (var keyPair in values)
                    {
                        cmd.CommandText = keyPair.Value.IsCreate ? CREATE_QUERY : UPDATE_QUERY;

                        var chainedBlock = keyPair.Value.Value;

                        cmd.Parameters["@blockHash"].Value = chainedBlock.BlockHash.ToDbByteArray();
                        cmd.Parameters["@previousBlockHash"].Value = chainedBlock.PreviousBlockHash.ToDbByteArray();
                        cmd.Parameters["@height"].Value = chainedBlock.Height;
                        cmd.Parameters["@totalWork"].Value = chainedBlock.TotalWork.ToDbByteArray();

                        cmd.ExecuteNonQuery();
                    }

                    trans.Commit();
                    return true;
                }
            }
            catch (SqlException e)
            {
                if (e.IsDeadlock())
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
                cmd.Transaction = trans;

                cmd.CommandText = @"
                    DELETE FROM ChainedBlocks";

                cmd.ExecuteNonQuery();
            }
        }

        private const string CREATE_QUERY = @"
            MERGE ChainedBlocks AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN	
	            INSERT (BlockHash, PreviousBlockHash, Height, TotalWork)
	            VALUES (@blockHash, @previousBlockHash, @height, @totalWork);";

        private const string UPDATE_QUERY = @"
            MERGE ChainedBlocks AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN	
	            INSERT (BlockHash, PreviousBlockHash, Height, TotalWork)
	            VALUES (@blockHash, @previousBlockHash, @height, @totalWork)
            WHEN MATCHED THEN
                UPDATE SET PreviousBlockHash = @previousBlockHash, Height = @height, TotalWork = @totalWork;";
    }
}
