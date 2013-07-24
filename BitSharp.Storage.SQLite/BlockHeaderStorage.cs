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
using System.Data.SQLite;

namespace BitSharp.Storage.SQLite
{
    public class BlockHeaderStorage : SqlDataStorage, IBlockHeaderStorage
    {
        public BlockHeaderStorage(SQLiteStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenReadConnection())
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
            using (var conn = this.OpenReadConnection())
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
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT HeaderBytes
                    FROM BlockHeaders
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", DbType.Binary, 32).Value = blockHash.ToDbByteArray();

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
            using (var conn = this.OpenWriteConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@headerBytes", DbType = DbType.Binary, Size = 80 });

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
                    DELETE FROM BlockHeaders";

                cmd.ExecuteNonQuery();

                conn.Commit();
            }
        }

        private const string CREATE_QUERY = @"
            INSERT OR IGNORE
            INTO BlockHeaders (BlockHash, HeaderBytes)
	        VALUES (@blockHash, @headerBytes);";

        private const string UPDATE_QUERY = @"
            INSERT OR REPLACE
            INTO BlockHeaders (BlockHash, HeaderBytes)
	        VALUES (@blockHash, @headerBytes);";
    }
}
