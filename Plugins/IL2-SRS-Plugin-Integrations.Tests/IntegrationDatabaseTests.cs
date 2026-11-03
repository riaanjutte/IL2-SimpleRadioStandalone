using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations.Tests
{
    public class IntegrationDatabaseTests
    {
        [Fact]
        public void RunningEveryMigrationAgainChangesNothing()
        {
            using var temp = new TempDirectory();
            var (store, database) = IntegrationStoreTests.CreateStore(temp);
            store.Save(IntegrationStoreTests.Integration(new JsonObject(), name: "Kept"));
            var before = Snapshot(database);

            // As when a database numbered by another build is opened: every migration runs again.
            using (var connection = database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version = 0;";
                command.ExecuteNonQuery();
            }

            SqliteDatabase.ClearPool(database.DatabasePath);
            var reopened = temp.CreateIntegrationDatabase();

            Assert.Equal(before, Snapshot(reopened));
        }

        /// <summary>Every table's definition and rows, as text.</summary>
        private static string Snapshot(SqliteDatabase database)
        {
            using var connection = database.Open();
            var tables = new List<string>();
            var text = new StringBuilder();
            using (var schema = connection.CreateCommand())
            {
                schema.CommandText = "SELECT type, name, sql FROM sqlite_master ORDER BY type, name;";
                using var reader = schema.ExecuteReader();
                while (reader.Read())
                {
                    text.AppendLine($"{reader.GetString(0)} {reader.GetString(1)}: {(reader.IsDBNull(2) ? null : reader.GetString(2))}");
                    if (reader.GetString(0) == "table")
                    {
                        tables.Add(reader.GetString(1));
                    }
                }
            }

            foreach (var table in tables)
            {
                using var rows = connection.CreateCommand();
                rows.CommandText = $"SELECT * FROM \"{table}\" ORDER BY rowid;";
                using var reader = rows.ExecuteReader();
                while (reader.Read())
                {
                    var values = new object[reader.FieldCount];
                    reader.GetValues(values);
                    text.AppendLine(table + ": " + string.Join(" | ", values.Select(v => v is byte[] b ? Convert.ToBase64String(b) : v)));
                }
            }

            return text.ToString();
        }
    }
}
