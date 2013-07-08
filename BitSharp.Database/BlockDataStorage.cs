using BitSharp.Common;
using BitSharp.Database;
using BitSharp.Database.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.WireProtocol;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Database
{
    public class BlockDataStorage : SqlDataStorage, IBlockDataStorage
    {
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
                        var rawBytes = reader.GetBytes(2);

                        yield return new KeyValuePair<UInt256, Block>(blockHash, Block.FromRawBytes(rawBytes, blockHash));
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

                cmd.Parameters.SetValue("@blockHash", System.Data.DbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var rawBytes = reader.GetBytes(0);

                        block = Block.FromRawBytes(rawBytes, blockHash);
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
            using (var trans = conn.BeginTransaction())
            using (var cmd = trans.CreateCommand())
            {
                foreach (var keyPair in values)
                {
                    cmd.CommandText = keyPair.Value.IsCreate ? CREATE_QUERY : UPDATE_QUERY;

                    var block = keyPair.Value.Value;

                    var blockBytes = block.ToRawBytes();
                    cmd.Parameters.SetValue("@blockHash", System.Data.DbType.Binary, 32).Value = block.Hash.ToDbByteArray();
                    cmd.Parameters.SetValue("@rawBytes", System.Data.DbType.Binary, blockBytes.Length).Value = blockBytes;

                    cmd.ExecuteNonQuery();
                }

                trans.Commit();
                return true;
            }
        }

        public void Truncate()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    DELETE FROM BlockData";

                cmd.ExecuteNonQuery();
            }
        }

#if SQLITE
        private const string CREATE_QUERY = @"
            INSERT OR IGNORE
            INTO BlockData (BlockHash, RawBytes)
            VALUES (@blockHash, @rawBytes)";

        private const string UPDATE_QUERY = @"
            INSERT OR REPLACE
            INTO BlockData (BlockHash, RawBytes)
            VALUES (@blockHash, @rawBytes)";

#elif SQL_SERVER
        private const string CREATE_QUERY = @"
            MERGE BlockData AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, RawBytes)
	            VALUES (@blockHash, @rawBytes);";

        private const string UPDATE_QUERY = @"
            MERGE BlockData AS target
            USING (SELECT @blockHash) AS source (BlockHash)
            ON (target.BlockHash = source.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, RawBytes)
	            VALUES (@blockHash, @rawBytes)
            WHEN MATCHED THEN
                UPDATE SET RawBytes = @rawBytes;";
#endif

    }
}
