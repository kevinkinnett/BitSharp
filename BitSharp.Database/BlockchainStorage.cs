using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Database.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.WireProtocol;
using System.Data.SqlClient;
using System.Data.Common;
using System.Data.SQLite;
using BitSharp.Blockchain;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Immutable;
using System.Numerics;

namespace BitSharp.Database
{
    public class BlockchainStorage : SqlDataStorage, IBlockchainStorage
    {
        private readonly string dbFolderPath;

        public BlockchainStorage()
        {
            this.dbFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BitSharp");
        }

        public IEnumerable<Tuple<BlockchainKey, BlockchainMetadata>> ListBlockchains()
        {
            CheckDatabaseFolder();

            foreach (var file in Directory.EnumerateFiles(this.dbFolderPath, "Blockchain_*.sqlite"))
            {
                var results = new List<Tuple<BlockchainKey, BlockchainMetadata>>();
                try
                {
                    using (var conn = new SQLiteConnection(@"Data Source=""{0}"";".Format2(file)))
                    {
                        conn.Open();

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                            SELECT Guid, RootBlockHash, TotalWork
                            FROM BlockchainMetadata
                            WHERE IsComplete = 1";

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var guid = new Guid(reader.GetBytes(0));
                                    var rootBlockHash = reader.GetUInt256(1);
                                    var totalWork = reader.GetBigInteger(2);

                                    results.Add(Tuple.Create(new BlockchainKey(file, guid, rootBlockHash), new BlockchainMetadata(guid, rootBlockHash, totalWork)));
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error reading blockchain database: {0}: {1}".Format2(Path.GetFileName(file), e.Message));
                    Debugger.Break();
                }

                foreach (var result in results)
                    yield return result;
            }
        }

        public Blockchain.Blockchain ReadBlockchain(BlockchainKey blockchainKey)
        {
            CheckDatabaseFolder();

            var blockListBuilder = ImmutableList.CreateBuilder<BlockMetadata>();
            var utxoBuilder = ImmutableHashSet.CreateBuilder<TxOutputKey>();

            using (var conn = new SQLiteConnection(@"Data Source=""{0}"";".Format2(blockchainKey.FilePath)))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT BlockHash, PreviousBlockHash, Work, Height, TotalWork, IsValid
                        FROM BlockMetadata
                        WHERE Guid = @guid AND RootBlockHash = @rootBlockHash
                        ORDER BY Height ASC";

                    cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                    cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

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

                            blockListBuilder.Add(new BlockMetadata(blockHash, previousBlockHash, work, height, totalWork, isValid));
                        }
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT UtxoChunkBytes
                        FROM UtxoData
                        WHERE Guid = @guid AND RootBlockHash = @rootBlockHash";

                    cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                    cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var chunkBytes = reader.GetBytes(0);
                            var chunkStream = new MemoryStream(chunkBytes);
                            var chunkReader = new WireReader(chunkStream);

                            var chunkLength = chunkReader.ReadVarInt().ToIntChecked();

                            var outputs = new TxOutputKey[chunkLength];

                            for (var i = 0; i < chunkLength; i++)
                            {
                                var prevTxHash = chunkReader.Read32Bytes();
                                var prevTxOutputIndex = chunkReader.Read4Bytes();

                                outputs[i] = new TxOutputKey(prevTxHash, prevTxOutputIndex.ToIntChecked());
                            }

                            utxoBuilder.UnionWith(outputs);
                        }
                    }
                }
            }

            return new Blockchain.Blockchain(blockListBuilder.ToImmutable(), utxoBuilder.ToImmutable());
        }

        public BlockchainKey WriteBlockchain(Blockchain.Blockchain blockchain)
        {
            CheckDatabaseFolder();

            var guid = Guid.NewGuid();
            var filePath = Path.Combine(this.dbFolderPath, "Blockchain_{0}.sqlite".Format2(guid.ToString()));
            var blockchainKey = new BlockchainKey(filePath, guid, blockchain.RootBlockHash);

            using (var conn = new SQLiteConnection(@"Data Source=""{0}"";".Format2(blockchainKey.FilePath)))
            {
                conn.Open();

                // create blockchain schema
                using (var cmd = conn.CreateCommand())
                {
                    // TODO no checksums or resiliency at all on this data

                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS [BlockchainMetadata](
                            [Guid] [binary](16) NOT NULL,
                            [RootBlockHash] [binary](32) NOT NULL,
                            [TotalWork] [binary](64) NOT NULL,
                            [IsComplete] [bit] NOT NULL,
	                        CONSTRAINT [PK_BlockchainMetaData] PRIMARY KEY
	                        (
                                [Guid] ASC,
                                [RootBlockHash] ASC
	                        )
                        );

                        CREATE TABLE IF NOT EXISTS [BlockMetadata](
                            [Guid] [binary](16) NOT NULL,
                            [RootBlockHash] [binary](32) NOT NULL,
	                        [BlockHash] [binary](32) NOT NULL,
	                        [PreviousBlockHash] [binary](32) NOT NULL,
	                        [Work] [binary](64) NOT NULL,
	                        [Height] [bigint] NULL,
	                        [TotalWork] [binary](64) NULL,
	                        [IsValid] [bit] NULL,
	                        CONSTRAINT [PK_BlockMetaData] PRIMARY KEY
	                        (
                                [Guid] ASC,
                                [RootBlockHash] ASC,
		                        [BlockHash] ASC
	                        )
                        );
                        CREATE INDEX IF NOT EXISTS IX_BlockMetadata_Guid_RootBlockHash ON BlockMetadata ( Guid, RootBlockHash );

                        CREATE TABLE IF NOT EXISTS
                        UtxoData
                        (
	                        [Guid] [binary](16) NOT NULL,
	                        [RootBlockHash] [binary](32) NOT NULL,
	                        [UtxoChunkBytes] [varbinary](1000000) NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS IX_UtxoData_Guid_RootBlockHash ON UtxoData ( Guid, RootBlockHash );";

                    cmd.ExecuteNonQuery();
                }

                using (var trans = conn.BeginTransaction())
                {
                    // write out the metadata for the blockchain
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO BlockchainMetadata (Guid, RootBlockHash, TotalWork, IsComplete)
                            VALUES (@guid, @rootBlockHash, @totalWork, 0);";

                        cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();
                        cmd.Parameters.SetValue("@totalWork", System.Data.DbType.Binary, 64).Value = blockchain.TotalWork.ToDbByteArray();

                        cmd.ExecuteNonQuery();
                    }

                    // write out the block metadata comprising the blockchain
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO BlockMetadata (Guid, RootBlockHash, BlockHash, PreviousBlockHash, Work, Height, TotalWork, IsValid)
                            VALUES (@guid, @rootBlockHash, @blockHash, @previousBlockHash, @work, @height, @totalWork, @isValid)";

                        cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

                        foreach (var blockMetadata in blockchain.BlockList)
                        {
                            cmd.Parameters.SetValue("@blockHash", System.Data.DbType.Binary, 32).Value = blockMetadata.BlockHash.ToDbByteArray();
                            cmd.Parameters.SetValue("@previousBlockHash", System.Data.DbType.Binary, 32).Value = blockMetadata.PreviousBlockHash.ToDbByteArray();
                            cmd.Parameters.SetValue("@work", System.Data.DbType.Binary, 64).Value = (object)blockMetadata.Work.ToDbByteArray();
                            cmd.Parameters.SetValue("@height", System.Data.DbType.Int64).Value = (object)blockMetadata.Height ?? DBNull.Value;
                            cmd.Parameters.SetValue("@totalWork", System.Data.DbType.Binary, 64).Value = blockMetadata.TotalWork != null ? (object)blockMetadata.TotalWork.Value.ToDbByteArray() : DBNull.Value;
                            cmd.Parameters.SetValue("@isValid", System.Data.DbType.Boolean).Value = (object)blockMetadata.IsValid ?? DBNull.Value;

                            cmd.ExecuteNonQuery();
                        }
                    }

                    // write out the utxo
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO UtxoData (Guid, RootBlockhash, UtxoChunkBytes)
                            VALUES (@guid, @rootBlockHash, @utxoChunkBytes)";

                        cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

                        var chunkSize = 1820; // target 65,529 byte size
                        var currentOffset = 0;

                        using (var utxoEnumerator = blockchain.Utxo.GetEnumerator())
                        {
                            // chunk outer loop
                            while (currentOffset < blockchain.Utxo.Count)
                            {
                                var chunkLength = Math.Min(chunkSize, blockchain.Utxo.Count - currentOffset);

                                // varint is up to 9 bytes and txoutputkey is 36 bytes
                                var chunkBytes = new byte[9 + (36 * chunkSize)];
                                var chunkStream = new MemoryStream(chunkBytes);
                                var chunkWriter = new WireWriter(chunkStream);
                                chunkWriter.WriteVarInt((UInt32)chunkLength);

                                // chunk inner loop
                                for (var i = 0; i < chunkLength; i++)
                                {
                                    // get the next output from the utxo
                                    if (!utxoEnumerator.MoveNext())
                                        throw new Exception();

                                    var output = utxoEnumerator.Current;
                                    chunkWriter.Write32Bytes(output.previousTransactionHash);
                                    chunkWriter.Write4Bytes((UInt32)output.previousOutputIndex);
                                }

                                cmd.Parameters.SetValue("@utxoChunkBytes", System.Data.DbType.Binary, chunkBytes.Length).Value = chunkBytes;

                                // write the chunk
                                cmd.ExecuteNonQuery();

                                currentOffset += chunkLength;
                            }

                            // there should be no items left in utxo at this point
                            if (utxoEnumerator.MoveNext())
                                throw new Exception();
                        }
                    }

                    // mark write as complete
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE BlockchainMetadata SET IsComplete = 1
                            WHERE Guid = @guid AND RootBlockHash = @rootBlockHash;";

                        cmd.Parameters.SetValue("@guid", System.Data.DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", System.Data.DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

                        cmd.ExecuteNonQuery();
                    }


                    trans.Commit();
                }
            }

            return blockchainKey;
        }

        public void RemoveBlockchains(BigInteger lessThanTotalWork)
        {
            var blockchains = ListBlockchains().ToList();
            var removedCount = 0;
            foreach (var tuple in blockchains)
            {
                // always leave at least one blockchain
                if (removedCount + 1 == blockchains.Count)
                    break;

                if (tuple.Item2.TotalWork < lessThanTotalWork)
                {
                    removedCount++;

                    //TODO delete individual blockchain from database, and then the whole file if nothing else is left
                    File.Delete(tuple.Item1.FilePath);
                }
            }
        }

        private void CheckDatabaseFolder()
        {
            if (!Directory.Exists(this.dbFolderPath))
                Directory.CreateDirectory(this.dbFolderPath);
        }
    }
}
