using BitSharp.Common;
using BitSharp.Storage;
using BitSharp.Database.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Database
{
    public class BlockMetadataStorage : SqlDataStorage, IDataStorage<UInt256, BlockMetadata>
    {
        //TODO
        public IEnumerable<BlockMetadata> FindWinningChainedBlocks(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            // get the winning total work amongst pending metadata
            var pendingMaxTotalWork = pendingMetadata.Any(x => x.Value.TotalWork != null) ? pendingMetadata.Select(x => x.Value.TotalWork).Max().Value : 0;

            // get the winners amonst pending blocks
            var pendingWinners = pendingMetadata.Where(x => x.Value.TotalWork >= pendingMaxTotalWork).Select(x => x.Value).ToList();

            // use the list of pending winners in addition to the list from storage, unless the list from storage has higher total work
            var usePendingWinners = true;
            var winners = new List<BlockMetadata>();

            // get the winners amongst storage
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, PreviousBlockHash, Height, TotalWork, IsValid
                    FROM BlockMetadata
                    INNER JOIN
                    (
                        SELECT MaxTotalWork = MAX(TotalWork)
                        FROM BlockMetadata
                        WHERE IsValid IS NULL OR IsValid = 1
                    ) Winner
                        ON Winner.MaxTotalWork = BlockMetadata.TotalWork
                    WHERE
                        (IsValid IS NULL OR IsValid = 1)
                        AND Winner.MaxTotalWork >= @pendingMaxTotalWork";

                cmd.Parameters.Add("@pendingMaxTotalWork", System.Data.SqlDbType.Binary, 64).Value = pendingMaxTotalWork.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // make sure this block doesn't have a newer value in pending, which takes precedence
                        var blockHash = reader.GetUInt256(0);
                        if (!pendingMetadata.ContainsKey(blockHash))
                        {
                            var previousBlockHash = reader.GetUInt256(1);
                            var height = reader.GetInt64Nullable(2);
                            var totalWork = reader.GetBigIntegerNullable(3);
                            var isValid = reader.GetBooleanNullable(4);

                            winners.Add(new BlockMetadata
                            (
                                blockHash,
                                previousBlockHash,
                                height,
                                totalWork,
                                isValid
                            ));
                        }
                    }
                }
            }

            if (usePendingWinners)
                winners.AddRange(pendingWinners);

            return winners;
        }

        public IList<BlockMetadata> FindChainableBlocks(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata, bool includeStorage)
        {
            //TODO this won't find a block in storage that could be chained by something in pending

            //TODO this won't find anything in pending that can be chained by something already in storage

            //TODO slow...

            // get any pending blocks that can chain each other
            var chainable = pendingMetadata.Where(x => pendingMetadata.ContainsKey(x.Value.PreviousBlockHash) && pendingMetadata[x.Value.PreviousBlockHash].Height != null)
                .Select(x => x.Value)
                .ToList();

            // find remaining pending blocks
            //TODO
            //var pendingUnchained = pendingMetadata.Where(x => !chainable.ContainsKey(x.Key));

            if (includeStorage)
            {
                // get chainables from storage
                using (var conn = this.OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                    SELECT BlockMetadata.BlockHash, BlockMetadata.PreviousBlockHash, BlockMetadata.BlockHash, BlockMetadata.Height, BlockMetadata.TotalWork, BlockMetadata.IsValid
                    FROM BlockMetadata
                    INNER JOIN BlockMetadata Chained ON Chained.Height IS NOT NULL AND Chained.BlockHash = BlockMetadata.PreviousBlockHash
                    WHERE BlockMetadata.Height IS NULL";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // make sure this block doesn't have a newer value in pending, which takes precedence
                            var blockHash = reader.GetUInt256(0);
                            if (!pendingMetadata.ContainsKey(blockHash))
                            {
                                var previousBlockHash = reader.GetUInt256(1);
                                var height = reader.GetInt64Nullable(2);
                                var totalWork = reader.GetBigIntegerNullable(3);
                                var isValid = reader.GetBooleanNullable(4);

                                chainable.Add(new BlockMetadata
                                (
                                    blockHash,
                                    previousBlockHash,
                                    height,
                                    totalWork,
                                    isValid
                                ));
                            }
                        }
                    }
                }
            }

            return chainable;
        }

        public IEnumerable<UInt256> GetAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM BlockMetadata";

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

        public IEnumerable<KeyValuePair<UInt256, BlockMetadata>> GetAllValues()
        {
            Debug.WriteLine(new string('*', 80));
            Debug.WriteLine("EXPENSIVE OPERATION: BlockMetadataSqlStorage.GetAllValues");
            Debug.WriteLine(new string('*', 80));

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, PreviousBlockHash, Height, TotalWork, IsValid
                    FROM BlockMetadata";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var previousBlockHash = reader.GetUInt256(1);
                        var height = reader.GetInt64Nullable(2);
                        var totalWork = reader.GetBigIntegerNullable(3);
                        var isValid = reader.GetBooleanNullable(4);

                        yield return new KeyValuePair<UInt256, BlockMetadata>
                        (
                            blockHash,
                            new BlockMetadata
                            (
                                blockHash,
                                previousBlockHash,
                                height,
                                totalWork,
                                isValid
                            ));
                    }
                }
            }
        }

        public bool TryGetValue(UInt256 blockHash, out BlockMetadata blockMetadata)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT PreviousBlockHash, Height, TotalWork, IsValid
                    FROM BlockMetadata
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.Add("@blockHash", System.Data.SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var previousBlockHash = reader.GetUInt256(0);
                        var height = reader.GetInt64Nullable(1);
                        var totalWork = reader.GetBigIntegerNullable(2);
                        var isValid = reader.GetBooleanNullable(3);

                        blockMetadata = new BlockMetadata
                        (
                            blockHash,
                            previousBlockHash,
                            height,
                            totalWork,
                            isValid
                        );
                        return true;
                    }
                    else
                    {
                        blockMetadata = default(BlockMetadata);
                        return false;
                    }
                }
            }
        }

        public void CreateValue(UInt256 blockHash, BlockMetadata blockMetadata)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    MERGE BlockMetadata AS target
                    USING (SELECT @blockHash) AS source (BlockHash)
                    ON (target.BlockHash = source.BlockHash)
	                WHEN NOT MATCHED THEN	
	                    INSERT (BlockHash, PreviousBlockHash, Height, TotalWork, IsValid)
	                    VALUES (@blockHash, @previousBlockHash, @height, @totalWork, @isValid);";

                cmd.Parameters.Add("@blockHash", System.Data.SqlDbType.Binary, 32).Value = blockMetadata.BlockHash.ToDbByteArray();
                cmd.Parameters.Add("@previousBlockHash", System.Data.SqlDbType.Binary, 32).Value = blockMetadata.PreviousBlockHash.ToDbByteArray();
                cmd.Parameters.Add("@height", System.Data.SqlDbType.BigInt).Value = (object)blockMetadata.Height ?? DBNull.Value;
                cmd.Parameters.Add("@totalWork", System.Data.SqlDbType.Binary, 64).Value = blockMetadata.TotalWork != null ? (object)blockMetadata.TotalWork.Value.ToDbByteArray() : DBNull.Value;
                cmd.Parameters.Add("@isValid", System.Data.SqlDbType.Bit).Value = (object)blockMetadata.IsValid ?? DBNull.Value;

                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateValue(UInt256 blockHash, BlockMetadata blockMetadata)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    MERGE BlockMetadata AS target
                    USING (SELECT @blockHash) AS source (BlockHash)
                    ON (target.BlockHash = source.BlockHash)
	                WHEN NOT MATCHED THEN	
	                    INSERT (BlockHash, PreviousBlockHash, Height, TotalWork, IsValid)
	                    VALUES (@blockHash, @previousBlockHash, @height, @totalWork, @isValid)
                    WHEN MATCHED THEN
                        UPDATE SET PreviousBlockHash = @previousBlockHash, Height = @height, TotalWork = @totalWork, IsValid = @isValid;";

                cmd.Parameters.Add("@blockHash", System.Data.SqlDbType.Binary, 32).Value = blockMetadata.BlockHash.ToDbByteArray();
                cmd.Parameters.Add("@previousBlockHash", System.Data.SqlDbType.Binary, 32).Value = blockMetadata.PreviousBlockHash.ToDbByteArray();
                cmd.Parameters.Add("@height", System.Data.SqlDbType.BigInt).Value = (object)blockMetadata.Height ?? DBNull.Value;
                cmd.Parameters.Add("@totalWork", System.Data.SqlDbType.Binary, 64).Value = blockMetadata.TotalWork != null ? (object)blockMetadata.TotalWork.Value.ToDbByteArray() : DBNull.Value;
                cmd.Parameters.Add("@isValid", System.Data.SqlDbType.Bit).Value = (object)blockMetadata.IsValid ?? DBNull.Value;

                cmd.ExecuteNonQuery();
            }
        }

        public void Truncate()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    TRUNCATE TABLE BlockMetadata";

                cmd.ExecuteNonQuery();
            }
        }
    }
}
