using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Microsoft.Data.Sqlite;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// The plugin's SQLite database (integrations.db in its data directory): integrations, their runs and the
    /// values actions keep between runs.
    /// </summary>
    public sealed class IntegrationDatabase : SqliteDatabase
    {
        public const string FileName = "integrations.db";

        /// <summary>The tables, which servers before the plugin kept in srs.db.</summary>
        internal static readonly string[] Tables = { "integrations", "integration_executions", "integration_state" };

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>Schema changes, in order, one statement per string (see <see cref="SqliteDatabase"/>).</summary>
        private static readonly string[][] Migrations =
        {
            // 1: integrations and their runs (the outbox the dispatcher works through).
            new[]
            {
                @"
CREATE TABLE IF NOT EXISTS integrations (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    name          TEXT NOT NULL,
    description   TEXT,
    enabled       INTEGER NOT NULL,
    trigger_type  TEXT NOT NULL,
    conditions    TEXT NOT NULL,
    action_type   TEXT NOT NULL,
    action_config TEXT NOT NULL,
    retry_mode    TEXT NOT NULL,
    max_retries   INTEGER NOT NULL,
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
)",
                @"
CREATE TABLE IF NOT EXISTS integration_executions (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    integration_id   INTEGER NOT NULL,
    integration_name TEXT NOT NULL,
    action_type      TEXT NOT NULL,
    event_type       TEXT NOT NULL,
    event_message    TEXT,
    data             TEXT NOT NULL,
    status           TEXT NOT NULL,
    attempts         INTEGER NOT NULL,
    created_at       TEXT NOT NULL,
    next_attempt_at  TEXT,
    finished_at      TEXT,
    last_error       TEXT,
    result           TEXT
)",
                "CREATE INDEX IF NOT EXISTS ix_integration_executions_due ON integration_executions (status, next_attempt_at)",
                "CREATE INDEX IF NOT EXISTS ix_integration_executions_integration ON integration_executions (integration_id)"
            },
            // 2: values an action keeps for its integration between runs (e.g. the Discord message it updates).
            new[]
            {
                @"
CREATE TABLE IF NOT EXISTS integration_state (
    integration_id INTEGER NOT NULL,
    key            TEXT NOT NULL,
    value          TEXT NOT NULL,
    updated_at     TEXT NOT NULL,
    PRIMARY KEY (integration_id, key)
)"
            },
            // 3: finds an integration's oldest pending run without reading its finished ones.
            new[]
            {
                @"CREATE INDEX IF NOT EXISTS ix_integration_executions_pending ON integration_executions (integration_id, id)
    WHERE status IN ('queued', 'running')"
            }
        };

        public IntegrationDatabase(string databasePath)
            : base(databasePath, Migrations)
        {
        }

        /// <summary>
        /// Opens the plugin's database, first moving integrations a server from before the plugin kept in
        /// <paramref name="serverDataDirectory"/>/srs.db.
        /// </summary>
        public static IntegrationDatabase Open(string dataDirectory, string serverDataDirectory)
        {
            var database = new IntegrationDatabase(Path.Combine(dataDirectory, FileName));
            if (serverDataDirectory != null)
            {
                database.MoveFrom(Path.Combine(serverDataDirectory, "srs.db"));
            }

            return database;
        }

        /// <summary>
        /// Copies the integration tables of an older database into this one, keeping their ids, then drops them
        /// there. Rows already here are kept, so a move that was interrupted completes on the next start.
        /// </summary>
        internal void MoveFrom(string olderDatabasePath)
        {
            if (!File.Exists(olderDatabasePath))
            {
                return;
            }

            using var connection = Open();
            using (var attach = connection.CreateCommand())
            {
                attach.CommandText = "ATTACH DATABASE $path AS older;";
                attach.Parameters.AddWithValue("$path", olderDatabasePath);
                attach.ExecuteNonQuery();
            }

            try
            {
                var present = Tables.Where(table => Exists(connection, table)).ToList();
                if (present.Count == 0)
                {
                    return;
                }

                // Copy and drop in separate transactions: in WAL mode SQLite does not commit attached databases
                // atomically, so a crash in a single commit could drop the old tables without keeping the copy.
                // Dropping only after the copy is committed means a crash leaves the old tables to copy again.
                var moved = new List<string>();
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var table in present)
                    {
                        var columns = string.Join(", ", Columns(connection, transaction, "main", table)
                            .Intersect(Columns(connection, transaction, "older", table)));
                        using var copy = connection.CreateCommand();
                        copy.Transaction = transaction;
                        copy.CommandText =
                            $"INSERT OR IGNORE INTO main.{table} ({columns}) SELECT {columns} FROM older.{table};";
                        moved.Add($"{copy.ExecuteNonQuery()} {table}");
                    }

                    transaction.Commit();
                }

                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var table in present)
                    {
                        using var drop = connection.CreateCommand();
                        drop.Transaction = transaction;
                        drop.CommandText = $"DROP TABLE older.{table};";
                        drop.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }

                Logger.Info($"Moved integrations from {olderDatabasePath} to {DatabasePath}: {string.Join(", ", moved)}");
            }
            finally
            {
                using var detach = connection.CreateCommand();
                detach.CommandText = "DETACH DATABASE older;";
                detach.ExecuteNonQuery();
            }
        }

        private static bool Exists(SqliteConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM older.sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);
            return (long)command.ExecuteScalar() > 0;
        }

        private static List<string> Columns(SqliteConnection connection, SqliteTransaction transaction, string schema,
            string table)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT name FROM pragma_table_info($table, $schema);";
            command.Parameters.AddWithValue("$table", table);
            command.Parameters.AddWithValue("$schema", schema);
            using var reader = command.ExecuteReader();
            var columns = new List<string>();
            while (reader.Read())
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }
    }
}
