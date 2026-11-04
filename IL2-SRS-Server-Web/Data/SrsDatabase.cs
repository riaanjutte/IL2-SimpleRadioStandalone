
namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Data
{
    /// <summary>
    /// The server's SQLite database (srs.db in the data directory): settings, channel names,
    /// bans, the event log and API keys. Connections are pooled; open one per operation.
    /// </summary>
    public sealed class SrsDatabase : SqliteDatabase
    {
        public const string FileName = "srs.db";

        /// <summary>Schema changes, in order, one statement per string (see <see cref="SqliteDatabase"/>).</summary>
        private static readonly string[][] Migrations =
        {
            // 1: initial schema.
            new[]
            {
                @"
CREATE TABLE IF NOT EXISTS settings (
    key        TEXT PRIMARY KEY,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL
)",
                @"
CREATE TABLE IF NOT EXISTS channel_names (
    channel INTEGER PRIMARY KEY,
    name    TEXT NOT NULL
)",
                @"
CREATE TABLE IF NOT EXISTS bans (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ip          TEXT NOT NULL,
    player_name TEXT,
    reason      TEXT,
    created_by  TEXT NOT NULL,
    created_at  TEXT NOT NULL,
    expires_at  TEXT
)",
                "CREATE INDEX IF NOT EXISTS ix_bans_ip ON bans (ip)",
                @"
CREATE TABLE IF NOT EXISTS events (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    occurred_at TEXT NOT NULL,
    category    TEXT NOT NULL,
    actor       TEXT NOT NULL,
    message     TEXT NOT NULL
)",
                "CREATE INDEX IF NOT EXISTS ix_events_occurred_at ON events (occurred_at)",
                @"
CREATE TABLE IF NOT EXISTS api_keys (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    name         TEXT NOT NULL,
    scope        TEXT NOT NULL,
    prefix       TEXT NOT NULL UNIQUE,
    key_hash     BLOB NOT NULL UNIQUE,
    created_at   TEXT NOT NULL,
    last_used_at TEXT,
    revoked_at   TEXT
)"
            },
            // 2: per-coalition channel names (upstream 1.0.5.0).
            new[]
            {
                @"
CREATE TABLE IF NOT EXISTS coalition_channel_names (
    coalition INTEGER NOT NULL,
    channel   INTEGER NOT NULL,
    name      TEXT NOT NULL,
    PRIMARY KEY (coalition, channel)
)"
            },
            // 3: typed events (type, client and JSON payload; null for older rows).
            new[]
            {
                "ALTER TABLE events ADD COLUMN type TEXT",
                "ALTER TABLE events ADD COLUMN client_guid TEXT",
                "ALTER TABLE events ADD COLUMN payload TEXT",
                "CREATE INDEX IF NOT EXISTS ix_events_type ON events (type)",
                "CREATE INDEX IF NOT EXISTS ix_events_client_guid ON events (client_guid)"
            }
            // 4 and 5 were integration tables, now kept by the integrations plugin in its own database;
            // existing ones are moved there when the plugin first starts.
        };

        public static int SchemaVersion => Migrations.Length;

        public SrsDatabase(string databasePath)
            : base(databasePath, Migrations)
        {
        }
    }
}
