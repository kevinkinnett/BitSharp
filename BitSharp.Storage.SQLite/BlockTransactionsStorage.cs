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
using System.Collections.Immutable;
using System.Data.SQLite;

namespace BitSharp.Storage.SQLite
{
    public class BlockTransactionsStorage : SqlDataStorage, IBlockTransactionsStorage
    {
        public BlockTransactionsStorage(SQLiteStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(UInt256 blockHash, out ImmutableArray<Transaction> blockTransactions)
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash, TxBytes
                    FROM BlockTransactions
                    WHERE BlockHash = @blockHash
                    ORDER BY TxIndex ASC";

                cmd.Parameters.SetValue("@blockHash", DbType.Binary, 32).Value = blockHash.ToDbByteArray();

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
            using (var conn = this.OpenWriteConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR IGNORE
                    INTO BlockTransactions ( BlockHash, TxIndex, TxHash, TxBytes )
	                VALUES ( @blockHash, @txIndex, @txHash, @txBytes )";

                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@txIndex", DbType = DbType.Int32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@txHash", DbType = DbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@txBytes", DbType = DbType.Binary });

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
                    DELETE FROM Blocks";

                cmd.ExecuteNonQuery();

                conn.Commit();
            }
        }

        public IEnumerable<UInt256> ReadAllBlockHashes()
        {
            using (var conn = this.OpenReadConnection())
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
