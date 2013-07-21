using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Storage.SQLite.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Blockchain;
using BitSharp.Data;
using System.Data.SQLite;
using System.Data;

namespace BitSharp.Storage.SQLite
{
    public class ChainedBlockStorage : SqlDataStorage, IChainedBlockStorage
    {
        public ChainedBlockStorage(SQLiteStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenReadConnection())
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

            using (var conn = this.OpenReadConnection())
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
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT PreviousBlockHash, Height, TotalWork
                    FROM ChainedBlocks
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", DbType.Binary, 32).Value = blockHash.ToDbByteArray();

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
            using (var conn = this.OpenWriteConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@previousBlockHash", DbType = DbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@height", DbType = DbType.Int32 });
                cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@totalWork", DbType = DbType.Binary, Size = 64 });

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
                    DELETE FROM ChainedBlocks";

                cmd.ExecuteNonQuery();

                conn.Commit();
            }
        }

        public IEnumerable<UInt256> FindMissingBlocks()
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM ChainedBlocks
                    WHERE NOT EXISTS (SELECT * FROM Blocks WHERE Blocks.BlockHash = ChainedBlocks.BlockHash);";

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

        public IEnumerable<ChainedBlock> FindLeafChained()
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, PreviousBlockHash, Height, TotalWork
                    FROM ChainedBlocks Previous
                    WHERE NOT EXISTS (SELECT * FROM ChainedBlocks Next WHERE Next.PreviousBlockHash = Previous.BlockHash)";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var previousBlockHash = reader.GetUInt256(1);
                        var height = reader.GetInt32(2);
                        var totalWork = reader.GetBigInteger(3);

                        yield return new ChainedBlock
                        (
                            blockHash,
                            previousBlockHash,
                            height,
                            totalWork
                        );
                    }
                }
            }
        }

        public IEnumerable<ChainedBlock> FindChainedByPreviousBlockHash(UInt256 previousBlockHash)
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, Height, TotalWork
                    FROM ChainedBlocks
                    WHERE ChainedBlocks.PreviousBlockHash = @previousBlockHash";

                cmd.Parameters.SetValue("@previousBlockHash", DbType.Binary, 32).Value = previousBlockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var height = reader.GetInt32(1);
                        var totalWork = reader.GetBigInteger(2);

                        yield return new ChainedBlock
                        (
                            blockHash,
                            previousBlockHash,
                            height,
                            totalWork
                        );
                    }
                }
            }
        }

        public IEnumerable<ChainedBlock> FindChainedWhereProceedingUnchainedExists()
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT ChainedBlocks.BlockHash, ChainedBlocks.PreviousBlockHash, ChainedBlocks.Height, ChainedBlocks.TotalWork
                    FROM ChainedBlocks
                    INNER JOIN
                    (
                        SELECT BlockHash, PreviousBlockHash
                        FROM Blocks
                        WHERE NOT EXISTS(SELECT * FROM ChainedBlocks WHERE ChainedBlocks.BlockHash = Blocks.BlockHash)
                    ) UnchainedBlocks ON UnchainedBlocks.PreviousBlockHash = ChainedBlocks.BlockHash";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var previousBlockHash = reader.GetUInt256(1);
                        var height = reader.GetInt32(2);
                        var totalWork = reader.GetBigInteger(3);

                        yield return new ChainedBlock
                        (
                            blockHash,
                            previousBlockHash,
                            height,
                            totalWork
                        );
                    }
                }
            }
        }

        public IEnumerable<BlockHeader> FindUnchainedWherePreviousBlockExists()
        {
            using (var conn = this.OpenReadConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT UnchainedBlocks.BlockHash, UnchainedBlocks.BlockHeader
                    FROM
                    (
                        SELECT BlockHash, PreviousBlockHash, SUBSTR(RawBytes, 1, 80) AS BlockHeader
                        FROM Blocks
                        WHERE NOT EXISTS(SELECT * FROM ChainedBlocks WHERE ChainedBlocks.BlockHash = Blocks.BlockHash)
                    ) UnchainedBlocks
                    WHERE EXISTS(SELECT * FROM Blocks WHERE Blocks.BlockHash = UnchainedBlocks.PreviousBlockHash)";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var blockHeaderBytes = reader.GetBytes(1);

                        yield return StorageEncoder.DecodeBlockHeader(blockHeaderBytes.ToMemoryStream(), blockHash);
                    }
                }
            }
        }

        private const string CREATE_QUERY = @"
            INSERT OR IGNORE
            INTO ChainedBlocks (BlockHash, PreviousBlockHash, Height, TotalWork)
	        VALUES (@blockHash, @previousBlockHash, @height, @totalWork);";

        private const string UPDATE_QUERY = @"
            INSERT OR REPLACE
            INTO ChainedBlocks (BlockHash, PreviousBlockHash, Height, TotalWork)
	        VALUES (@blockHash, @previousBlockHash, @height, @totalWork);";
    }
}
