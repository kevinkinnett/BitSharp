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
using System.Collections.Immutable;
using FirebirdSql.Data.FirebirdClient;

namespace BitSharp.Storage.Firebird
{
    public class BlockTransactionsStorage : SqlDataStorage, IBlockTransactionsStorage
    {
        public BlockTransactionsStorage(FirebirdStorageContext storageContext)
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

                cmd.Parameters.SetValue("@blockHash", FbDbType.Char, FbCharset.Octets, 32).Value = blockHash.ToDbByteArray();

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
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;

                cmd.CommandText = @"
                    MERGE INTO BlockTransactions
                    USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash, CAST(@txIndex AS INTEGER) AS TxIndex FROM RDB$DATABASE) AS Param
                    ON (BlockTransactions.BlockHash = Param.BlockHash AND BlockTransactions.TxIndex = Param.TxIndex)
	                WHEN NOT MATCHED THEN
	                    INSERT ( BlockHash, TxIndex, TxHash, TxBytes )
	                    VALUES ( @blockHash, @txIndex, @txHash, @txBytes );";

                cmd.Parameters.Add(new FbParameter { ParameterName = "@blockHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@txIndex", FbDbType = FbDbType.Integer });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@txHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@txBytes", FbDbType = FbDbType.Binary });

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
