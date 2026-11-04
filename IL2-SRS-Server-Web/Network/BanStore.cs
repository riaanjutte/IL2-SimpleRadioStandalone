using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Microsoft.Data.Sqlite;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Network
{
    public sealed class BanRecord
    {
        public long Id { get; init; }
        public string IpAddress { get; init; }
        public string PlayerName { get; init; }
        public string Reason { get; init; }
        public string CreatedBy { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? ExpiresAtUtc { get; init; }

        public bool IsActive(DateTime utcNow) => ExpiresAtUtc == null || ExpiresAtUtc > utcNow;
    }

    /// <summary>
    /// Banned IP addresses with reason and optional expiry. Active bans are cached in memory
    /// because every new TCP connection is checked against them.
    /// </summary>
    public sealed class BanStore
    {
        private readonly SrsDatabase _database;
        private readonly object _lock = new object();
        // Refreshes read the table and replace the cache one at a time, so an older read never replaces a newer one.
        private readonly object _refreshLock = new object();
        private Dictionary<IPAddress, DateTime?> _active = new Dictionary<IPAddress, DateTime?>();

        public BanStore(SrsDatabase database)
        {
            _database = database;
            RefreshCache();
        }

        public int ActiveCount
        {
            get
            {
                var now = DateTime.UtcNow;
                lock (_lock)
                {
                    return _active.Values.Count(expires => expires == null || expires > now);
                }
            }
        }

        public bool Contains(IPAddress address)
        {
            if (address == null)
            {
                return false;
            }

            lock (_lock)
            {
                return _active.TryGetValue(Normalize(address), out var expires) &&
                       (expires == null || expires > DateTime.UtcNow);
            }
        }

        public BanRecord Add(IPAddress address, string playerName, string reason, string createdBy, DateTime? expiresAtUtc)
        {
            address = Normalize(address);
            var record = new BanRecord
            {
                IpAddress = address.ToString(),
                PlayerName = Clean(playerName),
                Reason = Clean(reason),
                CreatedBy = createdBy ?? "unknown",
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = expiresAtUtc?.ToUniversalTime()
            };

            long id;
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO bans (ip, player_name, reason, created_by, created_at, expires_at)
VALUES ($ip, $name, $reason, $by, $created, $expires);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("$ip", record.IpAddress);
                command.Parameters.AddWithValue("$name", (object)record.PlayerName ?? DBNull.Value);
                command.Parameters.AddWithValue("$reason", (object)record.Reason ?? DBNull.Value);
                command.Parameters.AddWithValue("$by", record.CreatedBy);
                command.Parameters.AddWithValue("$created", SrsDatabase.FormatTimestamp(record.CreatedAtUtc));
                command.Parameters.AddWithValue("$expires",
                    record.ExpiresAtUtc.HasValue ? SrsDatabase.FormatTimestamp(record.ExpiresAtUtc.Value) : DBNull.Value);
                id = (long)command.ExecuteScalar();
            }

            RefreshCache();
            return new BanRecord
            {
                Id = id,
                IpAddress = record.IpAddress,
                PlayerName = record.PlayerName,
                Reason = record.Reason,
                CreatedBy = record.CreatedBy,
                CreatedAtUtc = record.CreatedAtUtc,
                ExpiresAtUtc = record.ExpiresAtUtc
            };
        }

        public BanRecord Remove(long id)
        {
            var record = List().FirstOrDefault(ban => ban.Id == id);
            if (record == null)
            {
                return null;
            }

            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM bans WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }

            RefreshCache();
            return record;
        }

        public IReadOnlyList<BanRecord> List()
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT id, ip, player_name, reason, created_by, created_at, expires_at FROM bans ORDER BY id DESC;";
            using var reader = command.ExecuteReader();
            var bans = new List<BanRecord>();
            while (reader.Read())
            {
                bans.Add(Read(reader));
            }

            return bans;
        }

        private void RefreshCache()
        {
            lock (_refreshLock)
            {
                var active = new Dictionary<IPAddress, DateTime?>();
                var now = DateTime.UtcNow;
                foreach (var ban in List())
                {
                    if (!ban.IsActive(now) || !IPAddress.TryParse(ban.IpAddress, out var address))
                    {
                        continue;
                    }

                    address = Normalize(address);
                    // A permanent ban wins over a temporary one; otherwise keep the latest expiry.
                    if (!active.TryGetValue(address, out var existing) ||
                        (existing != null && (ban.ExpiresAtUtc == null || ban.ExpiresAtUtc > existing)))
                    {
                        active[address] = ban.ExpiresAtUtc;
                    }
                }

                lock (_lock)
                {
                    _active = active;
                }
            }
        }

        private static BanRecord Read(SqliteDataReader reader)
        {
            return new BanRecord
            {
                Id = reader.GetInt64(0),
                IpAddress = reader.GetString(1),
                PlayerName = reader.IsDBNull(2) ? null : reader.GetString(2),
                Reason = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedBy = reader.GetString(4),
                CreatedAtUtc = SrsDatabase.ParseTimestamp(reader.GetString(5)),
                ExpiresAtUtc = reader.IsDBNull(6) ? null : SrsDatabase.ParseTimestamp(reader.GetString(6))
            };
        }

        private static string Clean(string value)
        {
            value = value?.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        internal static IPAddress Normalize(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }
    }
}
