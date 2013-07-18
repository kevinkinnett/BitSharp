using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Storage.Firebird.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Blockchain;
using FirebirdSql.Data.FirebirdClient;
using BitSharp.Data;

namespace BitSharp.Storage.Firebird
{
    public class BlockMetadataStorage : SqlDataStorage, IBlockMetadataStorage
    {
        public IEnumerable<UInt256> FindMissingBlocks()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
                    FROM BlockMetadata
                    WHERE NOT EXISTS (SELECT * FROM BlockData WHERE BlockData.BlockHash = BlockMetadata.BlockHash);";

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

        public IEnumerable<BlockMetadata> FindWinningChainedBlocks(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            var pendingWinners = new List<BlockMetadata>();
            var usePendingWinners = false;

            // get the winning total work amongst pending metadata
            var pendingMaxTotalWork = pendingMetadata.Select(x => x.Value.TotalWork).Max();

            if (pendingMaxTotalWork != null)
            {
                // get the winners amonst pending blocks
                pendingWinners.AddRange(pendingMetadata.Where(x => x.Value.TotalWork >= pendingMaxTotalWork).Select(x => x.Value));
                usePendingWinners = true;
            }

            // use the list of pending winners in addition to the list from storage, unless the list from storage has higher total work
            var winners = new List<BlockMetadata>();

            // get the winners amongst storage
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, PreviousBlockHash, ""Work"", Height, TotalWork, IsValid
                    FROM
                    (
                        SELECT MAX(TotalWork) AS MaxTotalWork
                        FROM BlockMetadata
                        WHERE IsValid IS NULL OR IsValid = 1
                    ) Winner
                    INNER JOIN BlockMetadata ON BlockMetadata.TotalWork = Winner.MaxTotalWork
                    WHERE
                        (IsValid IS NULL OR IsValid = 1)
                        AND Winner.MaxTotalWork >= @pendingMaxTotalWork";

                cmd.Parameters.SetValue("@pendingMaxTotalWork", FbDbType.Char, FbCharset.Octets, 64).Value = (pendingMaxTotalWork ?? 0).ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // make sure this block doesn't have a newer value in pending, which takes precedence
                        var blockHash = reader.GetUInt256(0);
                        if (!pendingMetadata.ContainsKey(blockHash))
                        {
                            var previousBlockHash = reader.GetUInt256(1);
                            var work = reader.GetBigInteger(2);
                            var height = reader.GetInt64Nullable(3);
                            var totalWork = reader.GetBigIntegerNullable(4);
                            var isValid = reader.GetBooleanNullable(5);

                            if (totalWork > pendingMaxTotalWork)
                                usePendingWinners = false;

                            winners.Add(new BlockMetadata
                            (
                                blockHash,
                                previousBlockHash,
                                work,
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

        public Dictionary<UInt256, HashSet<UInt256>> FindUnchainedBlocksByPrevious()
        {
            var unchainedBlocksByPrevious = new Dictionary<UInt256, HashSet<UInt256>>();

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, PreviousBlockHash
                    FROM BlockMetadata
                    WHERE Height IS NULL";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var previousBlockHash = reader.GetUInt256(1);

                        HashSet<UInt256> unchainedSet;
                        if (!unchainedBlocksByPrevious.TryGetValue(previousBlockHash, out unchainedSet))
                        {
                            unchainedSet = new HashSet<UInt256>();
                            unchainedBlocksByPrevious.Add(previousBlockHash, unchainedSet);
                        }

                        unchainedSet.Add(blockHash);
                    }
                }
            }

            return unchainedBlocksByPrevious;
        }

        public Dictionary<BlockMetadata, HashSet<BlockMetadata>> FindChainedWithProceedingUnchained(IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            var chainedWithProceedingUnchained = new Dictionary<BlockMetadata, HashSet<BlockMetadata>>();
            var remaining = pendingMetadata.ToDictionary(x => x.Key, x => x.Value);

            // first look in pending metadata
            foreach (var chained in pendingMetadata.Values.Where(x => x.Height != null))
            {
                remaining.Remove(chained.BlockHash);

                foreach (var proceedingUnchained in pendingMetadata.Values.Where(x => x.PreviousBlockHash == chained.BlockHash && x.Height == null))
                {
                    remaining.Remove(proceedingUnchained.BlockHash);

                    HashSet<BlockMetadata> proceedingSet;
                    if (!chainedWithProceedingUnchained.TryGetValue(chained, out proceedingSet))
                    {
                        proceedingSet = new HashSet<BlockMetadata>();
                        chainedWithProceedingUnchained.Add(chained, proceedingSet);
                    }

                    proceedingSet.Add(proceedingUnchained);
                }
            }

            // for any pending metadata left over, check if it can be chained from storage
            if (remaining.Count > 0)
            {
                using (var conn1 = this.OpenConnection())
                using (var cmd1 = conn1.CreateCommand())
                {
                    cmd1.CommandText = @"
                    SELECT
                        Previous.BlockHash, Previous.PreviousBlockHash, Previous.""Work"", Previous.Height, Previous.TotalWork, Previous.IsValid
                    FROM BlockMetadata Previous
                    INNER JOIN BlockMetadata Next ON Next.Height IS NULL AND Next.PreviousBlockHash = Previous.BlockHash
                    WHERE
                        Previous.Height IS NOT NULL
                        AND Next.BlockHash = @blockHash";

                    cmd1.Parameters.Add(new FbParameter { ParameterName = "@blockHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });

                    foreach (var unchained in remaining.Values)
                    {
                        cmd1.Parameters["@blockHash"].Value = unchained.BlockHash.ToDbByteArray();

                        using (var reader = cmd1.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var blockHash = reader.GetUInt256(0);
                                var previousBlockHash = reader.GetUInt256(1);
                                var work = reader.GetBigInteger(2);
                                var height = reader.GetInt64Nullable(3);
                                var totalWork = reader.GetBigIntegerNullable(4);
                                var isValid = reader.GetBooleanNullable(5);
                                var prevBlockMetadata = new BlockMetadata(blockHash, previousBlockHash, work, height, totalWork, isValid);

                                HashSet<BlockMetadata> proceedingSet;
                                if (!chainedWithProceedingUnchained.TryGetValue(prevBlockMetadata, out proceedingSet))
                                {
                                    proceedingSet = new HashSet<BlockMetadata>();
                                    chainedWithProceedingUnchained.Add(prevBlockMetadata, proceedingSet);
                                }

                                proceedingSet.Add(unchained);
                            }
                        }
                    }
                }
            }

            using (var conn2 = this.OpenConnection())
            using (var cmd2 = conn2.CreateCommand())
            {
                cmd2.CommandText = @"
                    SELECT
                        Previous.BlockHash, Previous.PreviousBlockHash, Previous.""Work"", Previous.Height, Previous.TotalWork, Previous.IsValid,
                        Next.BlockHash, Next.PreviousBlockHash, Next.""Work"", Next.Height, Next.TotalWork, Next.IsValid
                    FROM BlockMetadata Previous
                    INNER JOIN BlockMetadata Next ON Next.Height IS NULL AND Next.PreviousBlockHash = Previous.BlockHash
                    WHERE
                        Previous.Height IS NOT NULL";

                using (var reader = cmd2.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var previousBlockHash = reader.GetUInt256(1);
                        var work = reader.GetBigInteger(2);
                        var height = reader.GetInt64Nullable(3);
                        var totalWork = reader.GetBigIntegerNullable(4);
                        var isValid = reader.GetBooleanNullable(5);
                        var prevBlockMetadata = new BlockMetadata(blockHash, previousBlockHash, work, height, totalWork, isValid);

                        var nextBlockHash = reader.GetUInt256(6);
                        var nextPreviousBlockHash = reader.GetUInt256(7);
                        var nextWork = reader.GetBigInteger(8);
                        var nextHeight = reader.GetInt64Nullable(9);
                        var nextTotalWork = reader.GetBigIntegerNullable(10);
                        var nextIsValid = reader.GetBooleanNullable(11);
                        var nextBlockMetadata = new BlockMetadata(nextBlockHash, nextPreviousBlockHash, nextWork, nextHeight, nextTotalWork, nextIsValid);

                        HashSet<BlockMetadata> proceedingSet;
                        if (!chainedWithProceedingUnchained.TryGetValue(prevBlockMetadata, out proceedingSet))
                        {
                            proceedingSet = new HashSet<BlockMetadata>();
                            chainedWithProceedingUnchained.Add(prevBlockMetadata, proceedingSet);
                        }

                        // check if it already exists from pending metadata
                        if (!proceedingSet.Contains(nextBlockMetadata))
                            proceedingSet.Add(nextBlockMetadata);
                    }
                }
            }

            return chainedWithProceedingUnchained;
        }

        public IEnumerable<UInt256> FindMissingPreviousBlocks(IEnumerable<UInt256> knownBlocks, IReadOnlyDictionary<UInt256, BlockMetadata> pendingMetadata)
        {
            var knownBlocksDict = knownBlocks.ToDictionary(x => x, x => x);
            var previousBlocksSet = new HashSet<UInt256>();

            // get list of previous blocks from pending
            previousBlocksSet.UnionWith(pendingMetadata.Where(x => x.Value.Height == null).Select(x => x.Value.PreviousBlockHash));

            // get list of previous blocks from storage
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT PreviousBlockHash
                    FROM BlockMetadata
                    WHERE Height IS NULL";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var previousBlockHash = reader.GetUInt256(0);
                        if (!knownBlocksDict.ContainsKey(previousBlockHash))
                            previousBlocksSet.Add(previousBlockHash);
                    }
                }
            }

            // remove previous hash 0
            previousBlocksSet.Remove(new UInt256(0));

            return previousBlocksSet;
        }

        public IEnumerable<UInt256> ReadAllKeys()
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

        public IEnumerable<KeyValuePair<UInt256, BlockMetadata>> ReadAllValues()
        {
            Debug.WriteLine(new string('*', 80));
            Debug.WriteLine("EXPENSIVE OPERATION: BlockMetadataSqlStorage.GetAllValues");
            Debug.WriteLine(new string('*', 80));

            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, PreviousBlockHash, ""Work"", Height, TotalWork, IsValid
                    FROM BlockMetadata";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var previousBlockHash = reader.GetUInt256(1);
                        var work = reader.GetBigInteger(2);
                        var height = reader.GetInt64Nullable(3);
                        var totalWork = reader.GetBigIntegerNullable(4);
                        var isValid = reader.GetBooleanNullable(5);

                        yield return new KeyValuePair<UInt256, BlockMetadata>
                        (
                            blockHash,
                            new BlockMetadata
                            (
                                blockHash,
                                previousBlockHash,
                                work,
                                height,
                                totalWork,
                                isValid
                            ));
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 blockHash, out BlockMetadata blockMetadata)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT PreviousBlockHash, ""Work"", Height, TotalWork, IsValid
                    FROM BlockMetadata
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", FbDbType.Char, FbCharset.Octets, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var previousBlockHash = reader.GetUInt256(0);
                        var work = reader.GetBigInteger(1);
                        var height = reader.GetInt64Nullable(2);
                        var totalWork = reader.GetBigIntegerNullable(3);
                        var isValid = reader.GetBooleanNullable(4);

                        blockMetadata = new BlockMetadata
                        (
                            blockHash,
                            previousBlockHash,
                            work,
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

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BlockMetadata>>> values)
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;

                cmd.Parameters.Add(new FbParameter { ParameterName = "@blockHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@previousBlockHash", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 32 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@work", FbDbType = FbDbType.Char, Charset = FbCharset.Octets, Size = 64 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@height", FbDbType = FbDbType.BigInt });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@totalWork", FbDbType = FbDbType.Char, Size = 64 });
                cmd.Parameters.Add(new FbParameter { ParameterName = "@isValid", FbDbType = FbDbType.Integer });

                foreach (var keyPair in values)
                {
                    cmd.CommandText = keyPair.Value.IsCreate ? CREATE_QUERY : UPDATE_QUERY;

                    var blockMetadata = keyPair.Value.Value;

                    cmd.Parameters["@blockHash"].Value = blockMetadata.BlockHash.ToDbByteArray();
                    cmd.Parameters["@previousBlockHash"].Value = blockMetadata.PreviousBlockHash.ToDbByteArray();
                    cmd.Parameters["@work"].Value = blockMetadata.Work.ToDbByteArray();

                    if (blockMetadata.Height != null)
                        cmd.Parameters["@height"].Value = blockMetadata.Height.Value;
                    else
                        cmd.Parameters["@height"].Value = DBNull.Value;

                    if (blockMetadata.TotalWork != null)
                    {
                        cmd.Parameters["@totalWork"].Charset = FbCharset.Octets;
                        cmd.Parameters["@totalWork"].Value = blockMetadata.TotalWork.Value.ToDbByteArray();
                    }
                    else
                    {
                        cmd.Parameters["@totalWork"].Charset = FbCharset.Default;
                        cmd.Parameters["@totalWork"].Value = DBNull.Value;
                    }

                    if (blockMetadata.IsValid != null)
                        cmd.Parameters["@isValid"].Value = blockMetadata.IsValid.Value ? 1 : 0;
                    else
                        cmd.Parameters["@isValid"].Value = DBNull.Value;

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
                cmd.Transaction = trans;

                cmd.CommandText = @"
                    DELETE FROM BlockMetadata";

                cmd.ExecuteNonQuery();
            }
        }

        private const string CREATE_QUERY = @"
            MERGE INTO BlockMetadata
            USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash FROM RDB$DATABASE) AS Param
            ON (BlockMetadata.BlockHash = Param.BlockHash)
	        WHEN NOT MATCHED THEN	
	            INSERT (BlockHash, PreviousBlockHash, ""Work"", Height, TotalWork, IsValid)
	            VALUES (@blockHash, @previousBlockHash, @work, @height, @totalWork, @isValid);";

        private const string UPDATE_QUERY = @"
            MERGE INTO BlockMetadata
            USING (SELECT CAST(@blockHash AS CHAR(32) CHARACTER SET OCTETS) AS BlockHash FROM RDB$DATABASE) AS Param
            ON (BlockMetadata.BlockHash = Param.BlockHash)
	        WHEN NOT MATCHED THEN	
	            INSERT (BlockHash, PreviousBlockHash, ""Work"", Height, TotalWork, IsValid)
	            VALUES (@blockHash, @previousBlockHash, @work, @height, @totalWork, @isValid)
            WHEN MATCHED THEN
                UPDATE SET PreviousBlockHash = @previousBlockHash, Height = @height, ""Work"" = @work, TotalWork = @totalWork, IsValid = @isValid;";
    }
}
