using BitSharp.Common;
using BitSharp.Storage.SqlServer;
using BitSharp.Storage.SqlServer.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Network;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace BitSharp.Storage.SqlServer
{
    public struct KnownAddressKey
    {
        public readonly ImmutableArray<byte> IPv6Address;
        public readonly UInt16 Port;
        private readonly int _hashCode;

        public KnownAddressKey(ImmutableArray<byte> IPv6Address, UInt16 Port)
        {
            this.IPv6Address = IPv6Address;
            this.Port = Port;

            this._hashCode = Port.GetHashCode() ^ new BigInteger(IPv6Address.ToArray()).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is KnownAddressKey))
                return false;

            var other = (KnownAddressKey)obj;
            return other.IPv6Address.SequenceEqual(this.IPv6Address) && other.Port == this.Port;
        }

        public override int GetHashCode()
        {
            return this._hashCode;
        }
    }

    public class KnownAddressStorage : SqlDataStorage, IBoundedStorage<KnownAddressKey, NetworkAddressWithTime>
    {
        public KnownAddressStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<KnownAddressKey> ReadAllKeys()
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
                        var ipAddress = reader.GetBytes(0).ToImmutableArray();
                        var port = reader.GetUInt16(1);
                        yield return new KnownAddressKey(ipAddress, port);
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<KnownAddressKey, NetworkAddressWithTime>> ReadAllValues()
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
                        var ipAddress = reader.GetBytes(0).ToImmutableArray();
                        var port = reader.GetUInt16(1);
                        var services = reader.GetUInt64(2);
                        var time = reader.GetUInt32(3);

                        var key = new KnownAddressKey(ipAddress, port);
                        var knownAddress = new NetworkAddressWithTime(time, new NetworkAddress(services, ipAddress, port));

                        yield return new KeyValuePair<KnownAddressKey, NetworkAddressWithTime>(key, knownAddress);
                    }
                }
            }
        }

        public bool TryReadValue(KnownAddressKey key, out NetworkAddressWithTime knownAddress)
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
                        var ipAddress = reader.GetBytes(0).ToImmutableArray();
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

        public bool TryWriteValues(IEnumerable<KeyValuePair<KnownAddressKey, WriteValue<NetworkAddressWithTime>>> values)
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

                    cmd.Parameters.SetValue("@ipAddress", SqlDbType.Binary, 16).Value = knownAddress.NetworkAddress.IPv6Address.ToArray();
                    cmd.Parameters.SetValue("@port", SqlDbType.Binary, 2).Value = knownAddress.NetworkAddress.Port.ToDbByteArray();
                    cmd.Parameters.SetValue("@services", SqlDbType.Binary, 8).Value = knownAddress.NetworkAddress.Services.ToDbByteArray();
                    cmd.Parameters.SetValue("@time", SqlDbType.Binary, 4).Value = knownAddress.Time.ToDbByteArray();

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
            MERGE KnownAddresses AS target
            USING (SELECT @ipAddress, @port) AS source (IPAddress, Port)
            ON (target.IPAddress = source.IPAddress AND target.Port = source.Port)
	        WHEN NOT MATCHED THEN	
	            INSERT (IPAddress, Port, Services, Time)
	            VALUES (@ipAddress, @port, @services, @time);";

        private const string UPDATE_QUERY = @"
            MERGE KnownAddresses AS target
            USING (SELECT @ipAddress, @port) AS source (IPAddress, Port)
            ON (target.IPAddress = source.IPAddress AND target.Port = source.Port)
	        WHEN NOT MATCHED THEN	
	            INSERT (IPAddress, Port, Services, Time)
	            VALUES (@ipAddress, @port, @services, @time)
            WHEN MATCHED THEN 
                UPDATE SET Services = @services, Time = @time;";
    }
}
