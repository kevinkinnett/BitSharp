using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Data.Common;
using BitSharp.Storage;

namespace BitSharp.Database
{
    public abstract class SqlDataStorage : IDisposable
    {
#if SQLITE
        private readonly WrappedConnection connection;

        public SqlDataStorage()
        {
            SQLiteConnection connection = WrappedConnection.OpenNewConnection();
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("BitSharp.Database.Sql.CreateDatabase.sql"))
                using (var reader = new StreamReader(stream))
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = reader.ReadToEnd();

                    cmd.ExecuteNonQuery();
                }

                this.connection = new WrappedConnection(connection, dispose: false);
            }
            catch (Exception)
            {
                connection.Dispose();
                throw;
            }
        }

        internal WrappedConnection OpenConnection()
        {
            // for SQLite, reuse the same connection on open
            return this.connection;
        }

        internal WrappedConnection OpenUtxoConnection(Guid guid)
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp/UTXO/{0}.sqlite".Format2(guid.ToString()));
            var connection = new SQLiteConnection(@"Data Source=""{0}"";".Format2(dbPath));
            try
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
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

                return new WrappedConnection(connection, dispose: true);
            }
            catch (Exception)
            {
                connection.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            this.connection.CloseConnection();
        }
#elif SQL_SERVER
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["BitSharpDb"].ConnectionString;

        internal WrappedConnection OpenConnection()
        {
            // for SQL Server, open a new connection each time
            return new WrappedConnection(connectionString);
        }

        public void Dispose()
        {
        }
#endif
    }
}
