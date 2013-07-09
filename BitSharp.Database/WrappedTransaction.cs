using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Database
{
    internal class WrappedTransaction : IDisposable
    {
#if SQLITE
        private readonly SQLiteConnection connection;
        private readonly SQLiteTransaction transaction;
        private bool dispose;

        public WrappedTransaction(SQLiteConnection connection, bool dispose)
        {
            this.connection = connection;
            this.dispose = dispose;
            try
            {
                this.transaction = this.connection.BeginTransaction();
            }
            catch (Exception)
            {
                if (dispose)
                    this.connection.Dispose();
                throw;
            }
        }

        public SQLiteCommand CreateCommand()
        {
            return this.connection.CreateCommand();
        }

        public void Commit()
        {
            this.transaction.Commit();
        }

        public void Rollback()
        {
            this.transaction.Rollback();
        }

        public void Dispose()
        {
            try
            {
                this.transaction.Dispose();
            }
            finally
            {
                if (this.dispose)
                    this.connection.Dispose();
            }
        }

#elif SQL_SERVER
        private readonly SqlTransaction transaction;

        public WrappedTransaction(SqlConnection connection)
        {
            this.transaction = connection.BeginTransaction();
        }

        public SqlCommand CreateCommand()
        {
            var command = this.transaction.Connection.CreateCommand();
            command.Transaction = this.transaction;
            return command;
        }

        public void Commit()
        {
            this.transaction.Commit();
        }

        public void Rollback()
        {
            this.transaction.Rollback();
        }

        public void Dispose()
        {
            this.transaction.Dispose();
        }
#endif
    }
}
