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
    public class BlockStorage : SqlDataStorage, IBlockStorage
    {
        public BlockStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM Blocks";

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

        public IEnumerable<KeyValuePair<UInt256, Block>> ReadAllValues()
        {
            Debug.WriteLine(new string('*', 80));
            Debug.WriteLine("EXPENSIVE OPERATION: BlockStorage.GetAllValues");
            Debug.WriteLine(new string('*', 80));

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, RawBytes
                    FROM Blocks";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var rawBytes = reader.GetBytes(1);

                        yield return new KeyValuePair<UInt256, Block>(blockHash, StorageEncoder.DecodeBlock(rawBytes.ToMemoryStream(), blockHash));
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 blockHash, out Block block)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT RawBytes
                    FROM Blocks
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var rawBytes = reader.GetBytes(0);

                        block = StorageEncoder.DecodeBlock(rawBytes.ToMemoryStream(), blockHash);
                        return true;
                    }
                    else
                    {
                        block = default(Block);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Block>>> values)
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction(IsolationLevel.ReadUncommitted))
            using (var cmd = conn.CreateCommand())
            using (var txCmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;
                txCmd.Transaction = trans;

                cmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SqlParameter { ParameterName = "@previousBlockHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SqlParameter { ParameterName = "@rawBytes", SqlDbType = SqlDbType.VarBinary });

                txCmd.CommandText = @"
                    MERGE TransactionLocators AS target
                    USING (SELECT @blockHash, @transactionHash) AS source (BlockHash, TransactionHash)
                    ON (target.BlockHash = source.BlockHash AND target.TransactionHash = source.TransactionHash)
	                WHEN NOT MATCHED THEN
	                    INSERT ( BlockHash, TransactionHash, TransactionIndex )
	                    VALUES ( @blockHash, @transactionHash, @transactionIndex );";

                txCmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                txCmd.Parameters.Add(new SqlParameter { ParameterName = "@transactionHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                txCmd.Parameters.Add(new SqlParameter { ParameterName = "@transactionIndex", SqlDbType = SqlDbType.Binary, Size = 4 });

                cmd.CommandText = CREATE_QUERY;
                foreach (var keyPair in values.Where(x => x.Value.IsCreate))
                {
                    var block = keyPair.Value.Value;

                    var blockBytes = StorageEncoder.EncodeBlock(block);
                    cmd.Parameters["@blockHash"].Value = block.Hash.ToDbByteArray();
                    cmd.Parameters["@previousBlockHash"].Value = block.Header.PreviousBlock.ToDbByteArray();
                    cmd.Parameters["@rawBytes"].Size = blockBytes.Length;
                    cmd.Parameters["@rawBytes"].Value = blockBytes;

                    cmd.ExecuteNonQuery();

                    for (var txIndex = 0; txIndex < block.Transactions.Length; txIndex++)
                    {
                        var tx = block.Transactions[txIndex];
                        txCmd.Parameters["@blockHash"].Value = block.Hash.ToDbByteArray();
                        txCmd.Parameters["@transactionHash"].Value = tx.Hash.ToDbByteArray();
                        txCmd.Parameters["@transactionIndex"].Value = ((UInt32)txIndex).ToDbByteArray();

                        txCmd.ExecuteNonQuery();
                    }
                }

                cmd.CommandText = UPDATE_QUERY;
                foreach (var keyPair in values.Where(x => !x.Value.IsCreate))
                {
                    var block = keyPair.Value.Value;

                    var blockBytes = StorageEncoder.EncodeBlock(block);
                    cmd.Parameters["@blockHash"].Value = block.Hash.ToDbByteArray();
                    cmd.Parameters["@previousBlockHash"].Value = block.Header.PreviousBlock.ToDbByteArray();
                    cmd.Parameters["@rawBytes"].Size = blockBytes.Length;
                    cmd.Parameters["@rawBytes"].Value = blockBytes;

                    cmd.ExecuteNonQuery();

                    for (var txIndex = 0; txIndex < block.Transactions.Length; txIndex++)
                    {
                        var tx = block.Transactions[txIndex];
                        txCmd.Parameters["@blockHash"].Value = block.Hash.ToDbByteArray();
                        txCmd.Parameters["@transactionHash"].Value = tx.Hash.ToDbByteArray();
                        txCmd.Parameters["@transactionIndex"].Value = ((UInt32)txIndex).ToDbByteArray();

                        txCmd.ExecuteNonQuery();
                    }
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
                cmd.CommandText = @"
                    DELETE FROM Blocks";

                cmd.ExecuteNonQuery();
            }
        }

        public IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllBlockHeaders()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, SUBSTR(RawBytes, 1, 80)
                    FROM Blocks";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var rawBytes = reader.GetBytes(1);

                        yield return new KeyValuePair<UInt256, BlockHeader>(blockHash, StorageEncoder.DecodeBlockHeader(rawBytes.ToMemoryStream(), blockHash));
                    }
                }
            }
        }

        public bool TryReadBlockHeader(UInt256 blockHash, out BlockHeader blockHeader)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT SUBSTRING(RawBytes, 1, 80)
                    FROM Blocks
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var rawBytes = reader.GetBytes(0);

                        blockHeader = StorageEncoder.DecodeBlockHeader(rawBytes.ToMemoryStream(), blockHash);
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

        public IEnumerable<UInt256> FindMissingPreviousBlocks()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Next.BlockHash
                    FROM Blocks Next
                    WHERE NOT EXISTS(SELECT * FROM Blocks Previous WHERE Previous.BlockHash = Next.PreviousBlockHash)";

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

        private const string CREATE_QUERY = @"
            MERGE Blocks AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, PreviousBlockHash, RawBytes)
	            VALUES (@blockHash, @previousBlockHash, @rawBytes);";

        private const string UPDATE_QUERY = @"
            MERGE Blocks AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, PreviousBlockHash, RawBytes)
	            VALUES (@blockHash, @previousBlockHash, @rawBytes)
            WHEN MATCHED THEN
                UPDATE SET PreviousBlockHash = @previousBlockHash, RawBytes = @rawBytes;";
    }
}
