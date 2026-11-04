using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Data
{
    /// <summary>
    /// A SQLite database file with its schema migrations, in WAL mode. Connections are pooled; open one per
    /// operation. The server's srs.db is one; a plugin can keep its own in its data directory.
    /// </summary>
    /// <remarks>
    /// Migrations run in order; migration N takes the database from version N to N + 1. A migration is a list of
    /// SQL statements, one per string, run as written.
    /// <para>
    /// Every statement must be safe to run again (CREATE ... IF NOT EXISTS, INSERT OR IGNORE; ADD COLUMN is
    /// skipped when the column exists): whenever the database's version differs from the number of migrations,
    /// all of them run, so databases numbered by an older build, a newer one or a fork with its own migrations
    /// still end up with every table and column. A database numbered higher is set back to this build's
    /// version, so a build with more migrations runs them all again rather than skipping its own. So a
    /// migration must never change existing data in a way that is wrong to repeat, such as converting a value.
    /// </para>
    /// </remarks>
    public class SqliteDatabase
    {
        private static readonly Regex AddColumn = new(@"^\s*ALTER\s+TABLE\s+(\w+)\s+ADD\s+COLUMN\s+(\w+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly string _connectionString;

        public SqliteDatabase(string databasePath, IReadOnlyList<IReadOnlyList<string>> migrations)
        {
            ArgumentNullException.ThrowIfNull(migrations);
            DatabasePath = databasePath;
            _connectionString = ConnectionString(databasePath);

            Initialize(migrations);
        }

        public string DatabasePath { get; }

        /// <summary>True when this process created the database; used to run one-off imports.</summary>
        public bool WasCreated { get; private set; }

        public SqliteConnection Open()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Closes the pooled connections to the database at <paramref name="databasePath"/>, so the file can be
        /// deleted or replaced. Pools of other databases are left alone.
        /// </summary>
        public static void ClearPool(string databasePath)
        {
            using var connection = new SqliteConnection(ConnectionString(databasePath));
            SqliteConnection.ClearPool(connection);
        }

        public static string FormatTimestamp(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        public static DateTime ParseTimestamp(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
        }

        public static DateTime? ParseNullableTimestamp(object value)
        {
            return value is string text && text.Length > 0 ? ParseTimestamp(text) : (DateTime?)null;
        }

        private static string ConnectionString(string databasePath)
        {
            return new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = true,
                DefaultTimeout = 30
            }.ToString();
        }

        private void Initialize(IReadOnlyList<IReadOnlyList<string>> migrations)
        {
            using var connection = Open();

            using (var pragma = connection.CreateCommand())
            {
                // WAL lets the admin UI read while the server writes.
                pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                pragma.ExecuteNonQuery();
            }

            var version = ExecuteScalarInt(connection, "PRAGMA user_version;");
            if (version == migrations.Count)
            {
                return;
            }

            using var transaction = connection.BeginTransaction();
            foreach (var migration in migrations)
            {
                foreach (var statement in migration)
                {
                    if (HasColumnAlready(connection, transaction, statement))
                    {
                        continue;
                    }

                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = statement;
                    command.ExecuteNonQuery();
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "PRAGMA user_version = " + migrations.Count.ToString(CultureInfo.InvariantCulture) + ";";
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            WasCreated = version == 0;
        }

        /// <summary>True for an ALTER TABLE ... ADD COLUMN whose column is already there.</summary>
        private static bool HasColumnAlready(SqliteConnection connection, SqliteTransaction transaction, string statement)
        {
            var match = AddColumn.Match(statement);
            if (!match.Success)
            {
                return false;
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT name FROM pragma_table_info($table);";
            command.Parameters.AddWithValue("$table", match.Groups[1].Value);
            using var reader = command.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                columns.Add(reader.GetString(0));
            }

            return columns.Contains(match.Groups[2].Value);
        }

        private static int ExecuteScalarInt(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }
}
