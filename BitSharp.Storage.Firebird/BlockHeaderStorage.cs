using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.Firebird;
using BitSharp.Storage.Firebird.ExtensionMethods;
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
using FirebirdSql.Data.FirebirdClient;

namespace BitSharp.Storage.Firebird
{
    public class BlockHeaderStorage : SqlDataStorage, IBlockHeaderStorage
    {
        public BlockHeaderStorage(FirebirdStorageContext storageContext)
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
                        var headerBytes = reader.GetCharBytes(1);

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

                cmd.Parameters.SetValue("@blockHash", FbDbType.Char, FbCharset.Octets, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var headerBytes = reader.GetCharBytes(0);

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
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;

                cmd.Parameters.Add(new FbParameter { ParameterName = "@blockHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@headerBytes", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 80 });

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
            MERGE INTO BlockHeaders
            USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash FROM RDB$DATABASE) AS Param
            ON (BlockHeaders.BlockHash = Param.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, HeaderBytes)
	            VALUES (@blockHash, @headerBytes);";

        private const string UPDATE_QUERY = @"
            MERGE INTO BlockHeaders
            USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash FROM RDB$DATABASE) AS Param
            ON (BlockHeaders.BlockHash = Param.BlockHash)
	        WHEN NOT MATCHED THEN
	            INSERT (BlockHash, HeaderBytes)
	            VALUES (@blockHash, @headerBytes)
            WHEN MATCHED THEN
                UPDATE SET HeaderBytes = @headerBytes;";
    }
}
