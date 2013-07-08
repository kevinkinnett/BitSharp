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
        private readonly WrappedConnection connection = new WrappedConnection();

        internal WrappedConnection OpenConnection()
        {
            // for SQLite, reuse the same connection on open
            return this.connection;
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
