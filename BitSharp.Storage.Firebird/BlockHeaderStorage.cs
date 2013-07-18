using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.Firebird;
using BitSharp.Storage.Firebird.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.WireProtocol;
using FirebirdSql.Data.FirebirdClient;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Data;

namespace BitSharp.Storage
{
    public class BlockHeaderStorage : SqlDataStorage, IBlockHeaderStorage
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

        public IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllValues()
        {
            Debug.WriteLine(new string('*', 80));
            Debug.WriteLine("EXPENSIVE OPERATION: BlockHeaderStorage.GetAllValues");
            Debug.WriteLine(new string('*', 80));

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

        public bool TryReadValue(UInt256 blockHash, out BlockHeader blockHeader)
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

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BlockHeader>>> values)
        {
            throw new NotSupportedException();
        }
    }
}
