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
using BitSharp.Storage.SqlServer.ExtensionMethods;
using System.Threading;
using System.Diagnostics;

namespace BitSharp.Storage.SqlServer
{
    public abstract class SqlDataStorage : IDisposable
    {
        private readonly string connString;
        private readonly SqlServerStorageContext _storageContext;

        public SqlDataStorage(SqlServerStorageContext storageContext)
        {
            this.connString = @"Server=localhost; Database=BitSharp; Trusted_Connection=true;";
        }

        public SqlServerStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
        }

        protected SqlConnection OpenConnection()
        {
            CreateDatabase();

            var connection = new SqlConnection(this.connString);
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

        protected IEnumerable<T> IgnoreSqlErrors<T>(IEnumerable<T> results)
        {
            IEnumerator<T> enumerator;
            try
            {
                enumerator = results.GetEnumerator();
            }
            catch (SqlException e)
            {
                if (e.IsDeadlock() || e.IsTimeout())
                    yield break;
                else
                    throw;
            }
            try
            {
                while (true)
                {
                    bool read;
                    try
                    {
                        read = enumerator.MoveNext();
                    }
                    catch (SqlException e)
                    {
                        if (e.IsDeadlock() || e.IsTimeout())
                            yield break;
                        else
                            throw;
                    }

                    if (!read)
                        break;

                    yield return enumerator.Current;
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        private void CreateDatabase()
        {
            //try
            //{
            //    var assembly = Assembly.GetExecutingAssembly();
            //    using (var conn = new SqlConnection(this.connString))
            //    using (var stream = assembly.GetManifestResourceStream("BitSharp.Storage.SqlServer.Sql.CreateDatabase.sql"))
            //    using (var reader = new StreamReader(stream))
            //    {
            //        conn.Open();

            //        //TODO
            //        //var script = new FbScript(reader.ReadToEnd());
            //        //script.Parse();

            //        //new FbBatchExecution(conn, script).Execute();
            //    }
            //}
            //catch (Exception e)
            //{
            //    Debug.WriteLine("Database create failed: {0}".Format2(e.Message));
            //    Debugger.Break();

            //    throw;
            //}
        }
    }
}
