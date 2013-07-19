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
using BitSharp.WireProtocol;
using System.Data.SqlClient;
using System.Data.Common;
using BitSharp.Blockchain;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using BitSharp.Data;
using System.Data.SQLite;
using System.Data;

namespace BitSharp.Storage.SQLite
{
    public class BlockchainStorage : IBlockchainStorage
    {
        private readonly string dbFolderPath;
        private readonly SQLiteStorageContext _storageContext;

        public BlockchainStorage(SQLiteStorageContext storageContext)
        {
            this.dbFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BitSharp");
            this._storageContext = storageContext;
        }

        public SQLiteStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
        }

        public IEnumerable<Tuple<BlockchainKey, BlockchainMetadata>> ListBlockchains()
        {
            CheckDatabaseFolder();

            foreach (var file in Directory.EnumerateFiles(this.dbFolderPath, "Blockchain_*.sqlite"))
            {
                var results = new List<Tuple<BlockchainKey, BlockchainMetadata>>();
                try
                {
                    var connString = @"Data Source=""{0}"";".Format2(file);
                    using (var conn = new SQLiteConnection(connString))
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

                                    results.Add(Tuple.Create(new BlockchainKey(guid, rootBlockHash), new BlockchainMetadata(guid, rootBlockHash, totalWork)));
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error reading blockchain database: {0}: {1}".Format2(Path.GetFileName(file), e.Message));
                }

                foreach (var result in results)
                    yield return result;
            }
        }

        public Data.Blockchain ReadBlockchain(BlockchainKey blockchainKey)
        {
            CheckDatabaseFolder();

            var blockListBuilder = ImmutableList.CreateBuilder<BlockMetadata>();
            var utxoBuilder = ImmutableHashSet.CreateBuilder<TxOutputKey>();

            var connString = @"Data Source=""Blockchain_{0}.sqlite"";".Format2(blockchainKey.Guid);
            using (var conn = new SQLiteConnection(connString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT BlockHash, PreviousBlockHash, ""Work"", Height, TotalWork, IsValid
                        FROM BlockMetadata
                        WHERE Guid = @guid AND RootBlockHash = @rootBlockHash
                        ORDER BY Height ASC";

                    cmd.Parameters.SetValue("@guid", DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                    cmd.Parameters.SetValue("@rootBlockHash", DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

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

                    cmd.Parameters.SetValue("@guid", DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                    cmd.Parameters.SetValue("@rootBlockHash", DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var chunkBytes = reader.GetBytes(0);
                            var chunkStream = new MemoryStream(chunkBytes);
                            using (var chunkReader = new BinaryReader(chunkStream))
                            {
                                var chunkLength = chunkReader.Read4Bytes().ToIntChecked();

                                var outputs = new TxOutputKey[chunkLength];

                                for (var i = 0; i < chunkLength; i++)
                                {
                                    var prevTxHash = chunkReader.Read32Bytes();
                                    var prevTxOutputIndex = chunkReader.Read4Bytes();

                                    outputs[i] = new TxOutputKey(prevTxHash, prevTxOutputIndex);
                                }

                                utxoBuilder.UnionWith(outputs);
                            }
                        }
                    }
                }
            }

            return new Data.Blockchain(blockListBuilder.ToImmutable(), blockListBuilder.Select(x => x.BlockHash).ToImmutableHashSet(), utxoBuilder.ToImmutable());
        }

        public BlockchainKey WriteBlockchain(Data.Blockchain blockchain)
        {
            var guid = Guid.NewGuid();
            var dbPath = CreateDatabase(guid);
            var blockchainKey = new BlockchainKey(guid, blockchain.RootBlockHash);

            var connString = @"Data Source=""{0}"";".Format2(dbPath);
            using (var conn = new SQLiteConnection(connString))
            {
                conn.Open();

                using (var trans = conn.BeginTransaction())
                {
                    // write out the metadata for the blockchain
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        cmd.CommandText = @"
                            INSERT INTO BlockchainMetadata (Guid, RootBlockHash, TotalWork, IsComplete)
                            VALUES (@guid, @rootBlockHash, @totalWork, 0);";

                        cmd.Parameters.SetValue("@guid", DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();
                        cmd.Parameters.SetValue("@totalWork", DbType.Binary, 64).Value = blockchain.TotalWork.ToDbByteArray();

                        cmd.ExecuteNonQuery();
                    }

                    // write out the block metadata comprising the blockchain
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        cmd.CommandText = @"
                            INSERT INTO BlockMetadata (Guid, RootBlockHash, BlockHash, PreviousBlockHash, ""Work"", Height, TotalWork, IsValid)
                            VALUES (@guid, @rootBlockHash, @blockHash, @previousBlockHash, @work, @height, @totalWork, @isValid)";

                        cmd.Parameters.SetValue("@guid", DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();
                        cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                        cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@previousBlockHash", DbType = DbType.Binary, Size = 32 });
                        cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@work", DbType = DbType.Binary, Size = 64 });
                        cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@height", DbType = DbType.Int64 });
                        cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@totalWork", DbType = DbType.Binary, Size = 64 });
                        cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@isValid", DbType = DbType.Int32 });

                        foreach (var blockMetadata in blockchain.BlockList)
                        {
                            cmd.Parameters["@blockHash"].Value = blockMetadata.BlockHash.ToDbByteArray();
                            cmd.Parameters["@previousBlockHash"].Value = blockMetadata.PreviousBlockHash.ToDbByteArray();
                            cmd.Parameters["@work"].Value = blockMetadata.Work.ToDbByteArray();
                            cmd.Parameters["@height"].Value = blockMetadata.Height.Value;
                            cmd.Parameters["@totalWork"].Value = blockMetadata.TotalWork.Value.ToDbByteArray();
                            cmd.Parameters["@isValid"].Value = (object)blockMetadata.IsValid ?? DBNull.Value;

                            cmd.ExecuteNonQuery();
                        }
                    }

                    // write out the utxo
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        cmd.CommandText = @"
                            INSERT INTO UtxoData (Guid, RootBlockhash, UtxoChunkBytes)
                            VALUES (@guid, @rootBlockHash, @utxoChunkBytes)";

                        cmd.Parameters.SetValue("@guid", DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();
                        cmd.Parameters.Add(new SQLiteParameter { ParameterName = "@utxoChunkBytes", DbType = DbType.Binary });

                        var chunkSize = 100000;
                        var currentOffset = 0;
                        var chunkBytes = new byte[4 + (36 * chunkSize)];

                        using (var utxoEnumerator = blockchain.Utxo.GetEnumerator())
                        {
                            // chunk outer loop
                            while (currentOffset < blockchain.Utxo.Count)
                            {
                                var chunkLength = Math.Min(chunkSize, blockchain.Utxo.Count - currentOffset);

                                var chunkStream = new MemoryStream(chunkBytes);
                                using (var chunkWriter = new BinaryWriter(chunkStream))
                                {
                                    chunkWriter.Write4Bytes((UInt32)chunkLength);

                                    // chunk inner loop
                                    for (var i = 0; i < chunkLength; i++)
                                    {
                                        // get the next output from the utxo
                                        if (!utxoEnumerator.MoveNext())
                                            throw new Exception();

                                        var output = utxoEnumerator.Current;
                                        chunkWriter.Write32Bytes(output.TxHash);
                                        chunkWriter.Write4Bytes((UInt32)output.TxOutputIndex);
                                    }

                                    cmd.Parameters["@utxoChunkBytes"].Size = chunkBytes.Length;
                                    cmd.Parameters["@utxoChunkBytes"].Value = chunkBytes;
                                }

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
                        cmd.Transaction = trans;

                        cmd.CommandText = @"
                            UPDATE BlockchainMetadata SET IsComplete = 1
                            WHERE Guid = @guid AND RootBlockHash = @rootBlockHash;";

                        cmd.Parameters.SetValue("@guid", DbType.Binary, 16).Value = blockchainKey.Guid.ToByteArray();
                        cmd.Parameters.SetValue("@rootBlockHash", DbType.Binary, 32).Value = blockchainKey.RootBlockHash.ToDbByteArray();

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
                    File.Delete("Blockchain_{0}.sqlite".Format2(tuple.Item1.Guid));
                }
            }

            // find any files that are named like a blockchain but not included in the list above, these are invalid and can be cleaned up
            var validFiles = new HashSet<string>(blockchains.Select(x => "Blockchain_{0}.sqlite".Format2(x.Item1.Guid)));
            foreach (var file in Directory.EnumerateFiles(this.dbFolderPath, "Blockchain_*.sqlite"))
            {
                if (!validFiles.Contains(file))
                {
                    File.Delete(file);
                }
            }
        }

        private void CheckDatabaseFolder()
        {
            if (!Directory.Exists(this.dbFolderPath))
                Directory.CreateDirectory(this.dbFolderPath);
        }

        private string CreateDatabase(Guid guid)
        {
            var dbFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp");
            var dbPath = Path.Combine(dbFolderPath, "Blockchain_{0}.sqlite".Format2(guid.ToString()));
            var connString = @"Data Source=""{0}"";".Format2(dbPath);

            // create db folder
            if (!Directory.Exists(dbFolderPath))
                Directory.CreateDirectory(dbFolderPath);

            if (!File.Exists(dbPath))
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    using (var conn = new SQLiteConnection(connString))
                    using (var stream = assembly.GetManifestResourceStream("BitSharp.Storage.SQLite.Sql.CreateBlockchainDatabase.sql"))
                    using (var reader = new StreamReader(stream))
                    using (var cmd = conn.CreateCommand())
                    {
                        conn.Open();

                        cmd.CommandText = reader.ReadToEnd();
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Database create failed: {0}".Format2(e.Message));
                    Debugger.Break();

                    if (File.Exists(dbPath))
                        File.Delete(dbPath);

                    throw;
                }
            }

            return dbPath;
        }
    }
}
