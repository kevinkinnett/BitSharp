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

namespace BitSharp.Storage.SqlServer
{
    public class BlockHeaderStorage : SqlDataStorage, IBlockHeaderStorage
    {
        public BlockHeaderStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM BlockHeaders";

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

        public IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllValues()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, HeaderBytes
                    FROM BlockHeaders";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var headerBytes = reader.GetBytes(1);

                        yield return new KeyValuePair<UInt256, BlockHeader>(blockHash, StorageEncoder.DecodeBlockHeader(headerBytes.ToMemoryStream(), blockHash));
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 blockHash, out BlockHeader blockHeader)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT HeaderBytes
                    FROM BlockHeaders
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var headerBytes = reader.GetBytes(0);

                        blockHeader = StorageEncoder.DecodeBlockHeader(headerBytes.ToMemoryStream(), blockHash);
                        return true;
                    }
                    else
                    {
                        blockHeader = default(BlockHeader);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BlockHeader>>> values)
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
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@headerBytes", SqlDbType = SqlDbType.Binary, Size = 80 });

                    cmd.CommandText = CREATE_QUERY;
                    foreach (var keyPair in values.Where(x => x.Value.IsCreate))
                    {
                        var blockHeader = keyPair.Value.Value;

                        var blockBytes = StorageEncoder.EncodeBlockHeader(blockHeader);
                        cmd.Parameters["@blockHash"].Value = blockHeader.Hash.ToDbByteArray();
                        cmd.Parameters["@headerBytes"].Value = blockBytes;

                        cmd.ExecuteNonQuery();
                    }

                    cmd.CommandText = UPDATE_QUERY;
                    foreach (var keyPair in values.Where(x => !x.Value.IsCreate))
                    {
                        var blockHeader = keyPair.Value.Value;

                        var blockBytes = StorageEncoder.EncodeBlockHeader(blockHeader);
                        cmd.Parameters["@blockHash"].Value = blockHeader.Hash.ToDbByteArray();
                        cmd.Parameters["@headerBytes"].Value = blockBytes;

                        cmd.ExecuteNonQuery();
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
                    DELETE FROM BlockHeaders";

                cmd.ExecuteNonQuery();

                trans.Commit();
            }
        }

        private const string CREATE_QUERY = @"
            MERGE BlockHeaders AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, HeaderBytes)
	            VALUES (@blockHash, @headerBytes);";

        private const string UPDATE_QUERY = @"
            MERGE BlockHeaders AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, HeaderBytes)
	            VALUES (@blockHash, @headerBytes)
            WHEN MATCHED THEN
                UPDATE SET HeaderBytes = @headerBytes;";
    }
}
