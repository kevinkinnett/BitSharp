using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.Firebird;
using BitSharp.Storage.Firebird.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.WireProtocol;
using FirebirdSql.Data.FirebirdClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Data;

namespace BitSharp.Storage.Firebird
{
    public class BlockStorage : SqlDataStorage, IBlockStorage
    {
        public BlockStorage(FirebirdStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM BlockData";

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
            Debug.WriteLine("EXPENSIVE OPERATION: BlockDataStorage.GetAllValues");
            Debug.WriteLine(new string('*', 80));

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, RawBytes
                    FROM BlockData";

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
                    FROM BlockData
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", FbDbType.Char, FbCharset.Octets, 32).Value = blockHash.ToDbByteArray();

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

                cmd.Parameters.Add(new FbParameter { ParameterName = "@blockHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@rawBytes", FbDbType = FbDbType.Binary });

                txCmd.CommandText = @"
                    MERGE INTO TransactionLocators
                    USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash, CAST(@transactionHash AS CHAR(32) CHARACTER SET OCTETS) AS TransactionHash FROM RDB$DATABASE) AS Param
                    ON (TransactionLocators.BlockHash = Param.BlockHash AND TransactionLocators.TransactionHash = Param.TransactionHash)
	                WHEN NOT MATCHED THEN
	                    INSERT ( BlockHash, TransactionHash, TransactionIndex )
	                    VALUES ( @blockHash, @transactionHash, @transactionIndex );";

                txCmd.Parameters.Add(new FbParameter { ParameterName = "@blockHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                txCmd.Parameters.Add(new FbParameter { ParameterName = "@transactionHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                txCmd.Parameters.Add(new FbParameter { ParameterName = "@transactionIndex", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 4 });

                cmd.CommandText = CREATE_QUERY;
                foreach (var keyPair in values.Where(x => x.Value.IsCreate))
                {
                    var block = keyPair.Value.Value;

                    var blockBytes = StorageEncoder.EncodeBlock(block);
                    cmd.Parameters["@blockHash"].Value = block.Hash.ToDbByteArray();
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
                    DELETE FROM BlockData";

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
                    FROM BlockData";

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
                    SELECT SUBSTRING(RawBytes FROM 1 FOR 80)
                    FROM BlockData
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", FbDbType.Char, FbCharset.Octets, 32).Value = blockHash.ToDbByteArray();

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

        private const string CREATE_QUERY = @"
            MERGE INTO BlockData
            USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash FROM RDB$DATABASE) AS Param
            ON (BlockData.BlockHash = Param.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, RawBytes)
	            VALUES (@blockHash, @rawBytes);";

        private const string UPDATE_QUERY = @"
            MERGE INTO BlockData
            USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash FROM RDB$DATABASE) AS Param
            ON (BlockData.BlockHash = Param.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, RawBytes)
	            VALUES (@blockHash, @rawBytes)
            WHEN MATCHED THEN
                UPDATE SET RawBytes = @rawBytes;";
    }
}
