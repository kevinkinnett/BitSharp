using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.IO;
using System.Reflection;
using System.Data.Common;
using BitSharp.Storage;
using System.Threading;
using System.Diagnostics;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Services;
using FirebirdSql.Data.Isql;

namespace BitSharp.Storage.Firebird
{
    public abstract class SqlDataStorage : IDisposable
    {
        private readonly string dbPath;
        private readonly string connString;

        public SqlDataStorage()
        {
            var dbFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp");
            this.dbPath = Path.Combine(dbFolderPath, "BitSharp.fdb");
            this.connString = @"ServerType=1; DataSource=localhost; Database={0}; Pooling=true; MaxPoolSize=100; User=SYSDBA; Password=NA;".Format2(this.dbPath);
        }

        public void Dispose()
        {
        }

        protected FbConnection OpenConnection()
        {
            CreateDatabase();

            var connection = new FbConnection(this.connString);
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

        private void CreateDatabase()
        {
            // create db folder
            var dbFolderPath = Path.GetDirectoryName(this.dbPath);

            if (!Directory.Exists(dbFolderPath))
                Directory.CreateDirectory(dbFolderPath);

            if (!File.Exists(this.dbPath))
            {
                try
                {
                    var connString = @"ServerType=1; DataSource=localhost; Database={0}; Pooling=false; User=SYSDBA; Password=NA;".Format2(this.dbPath);

                    FbConnection.CreateDatabase(connString);

                    var assembly = Assembly.GetExecutingAssembly();
                    using (var conn = new FbConnection(connString))
                    using (var cmd = conn.CreateCommand())
                    using (var stream = assembly.GetManifestResourceStream("BitSharp.Storage.Firebird.Sql.CreateDatabase.sql"))
                    using (var reader = new StreamReader(stream))
                    {
                        conn.Open();

                        var script = new FbScript(reader.ReadToEnd());
                        script.Parse();
                        
                        new FbBatchExecution(conn, script).Execute();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Database create failed: {0}".Format2(e.Message));
                    Debugger.Break();

                    if (File.Exists(this.dbPath))
                        File.Delete(this.dbPath);

                    throw;
                }
            }
        }
    }
}
