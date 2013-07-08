using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Database
{
    internal class WrappedConnection : IDisposable
    {
#if SQLITE
        private readonly SQLiteConnection connection;

        public WrappedConnection()
        {
            this.connection = OpenNewConnection();

            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("BitSharp.Database.Sql.CreateDatabase.sql"))
            using (var reader = new StreamReader(stream))
            using (var cmd = this.connection.CreateCommand())
            {
                cmd.CommandText = reader.ReadToEnd();

                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
        }

        public void CloseConnection()
        {
            this.connection.Dispose();
        }

        public SQLiteCommand CreateCommand()
        {
            return this.connection.CreateCommand();
        }

        public WrappedTransaction BeginTransaction()
        {
            return new WrappedTransaction();
        }

        internal static SQLiteConnection OpenNewConnection()
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp/BitSharp.sqlite");
            var connection = new SQLiteConnection(@"Data Source=""{0}"";".Format2(dbPath));
            try
            {
                connection.Open();
                return connection;
            }
            catch (Exception)
            {
                connection.Dispose();
                throw;
            }
        }

#elif SQL_SERVER
        private readonly SqlConnection connection;

        public WrappedConnection(string connectionString)
        {
            this.connection = new SqlConnection(connectionString);
            this.connection.Open();
        }

        public void Dispose()
        {
            this.connection.Dispose();
        }

        public SqlCommand CreateCommand()
        {
            return this.connection.CreateCommand();
        }

        public WrappedTransaction BeginTransaction()
        {
            return new WrappedTransaction(this.connection);
        }
#endif
    }
}
