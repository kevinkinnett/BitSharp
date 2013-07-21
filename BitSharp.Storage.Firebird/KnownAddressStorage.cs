using BitSharp.Common;
using BitSharp.Storage.Firebird;
using BitSharp.Storage.Firebird.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Network;
using FirebirdSql.Data.FirebirdClient;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Firebird
{
    public class KnownAddressStorage : SqlDataStorage, IBoundedStorage<NetworkAddressKey, NetworkAddressWithTime>
    {
        public KnownAddressStorage(FirebirdStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<NetworkAddressKey> ReadAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT IPAddress, Port
                    FROM KnownAddresses";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var ipAddress = reader.GetCharBytes(0).ToImmutableArray();
                        var port = reader.GetUInt16(1);
                        yield return new NetworkAddressKey(ipAddress, port);
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>> ReadAllValues()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT IPAddress, Port, Services, ""Time""
                    FROM KnownAddresses";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var ipAddress = reader.GetCharBytes(0).ToImmutableArray();
                        var port = reader.GetUInt16(1);
                        var services = reader.GetUInt64(2);
                        var time = reader.GetUInt32(3);

                        var key = new NetworkAddressKey(ipAddress, port);
                        var knownAddress = new NetworkAddressWithTime(time, new NetworkAddress(services, ipAddress, port));

                        yield return new KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>(key, knownAddress);
                    }
                }
            }
        }

        public bool TryReadValue(NetworkAddressKey key, out NetworkAddressWithTime knownAddress)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT IPAddress, Port, Services, ""Time""
                    FROM KnownAddresses";

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var ipAddress = reader.GetCharBytes(0).ToImmutableArray();
                        var port = reader.GetUInt16(1);
                        var services = reader.GetUInt64(2);
                        var time = reader.GetUInt32(3);

                        knownAddress = new NetworkAddressWithTime(time, new NetworkAddress(services, ipAddress, port));

                        return true;
                    }
                    else
                    {
                        knownAddress = default(NetworkAddressWithTime);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<NetworkAddressKey, WriteValue<NetworkAddressWithTime>>> values)
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans;

                foreach (var keyPair in values)
                {
                    cmd.CommandText = keyPair.Value.IsCreate ? CREATE_QUERY : UPDATE_QUERY;

                    var knownAddress = keyPair.Value.Value;

                    cmd.Parameters.SetValue("@ipAddress", FbDbType.Char, FbCharset.Octets, 16).Value = knownAddress.NetworkAddress.IPv6Address.ToArray();
                    cmd.Parameters.SetValue("@port", FbDbType.Char, FbCharset.Octets, 2).Value = knownAddress.NetworkAddress.Port.ToDbByteArray();
                    cmd.Parameters.SetValue("@services", FbDbType.Char, FbCharset.Octets, 8).Value = knownAddress.NetworkAddress.Services.ToDbByteArray();
                    cmd.Parameters.SetValue("@time", FbDbType.Char, FbCharset.Octets, 4).Value = knownAddress.Time.ToDbByteArray();

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
                    DELETE FROM KnownAddresses";

                cmd.ExecuteNonQuery();
            }
        }

        private const string CREATE_QUERY = @"
            MERGE INTO KnownAddresses
            USING (SELECT CAST(@ipAddress AS CHAR(16) CHARACTER SET OCTETS) AS IpAddress, CAST(@port AS CHAR(4) CHARACTER SET OCTETS) AS Port FROM RDB$DATABASE) AS Param
            ON (KnownAddresses.IPAddress = Param.IPAddress AND KnownAddresses.Port = Param.Port)
	        WHEN NOT MATCHED THEN	
	            INSERT (IPAddress, Port, Services, ""Time"")
	            VALUES (@ipAddress, @port, @services, @time);";

        private const string UPDATE_QUERY = @"
            MERGE INTO KnownAddresses
            USING (SELECT CAST(@ipAddress AS CHAR(16) CHARACTER SET OCTETS) AS IpAddress, CAST(@port AS CHAR(4) CHARACTER SET OCTETS) AS Port FROM RDB$DATABASE) AS Param
            ON (KnownAddresses.IPAddress = Param.IPAddress AND KnownAddresses.Port = Param.Port)
	        WHEN NOT MATCHED THEN	
	            INSERT (IPAddress, Port, Services, ""Time"")
	            VALUES (@ipAddress, @port, @services, @time)
            WHEN MATCHED THEN 
                UPDATE SET Services = @services, ""Time"" = @time;";
    }
}
