using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Tests
{
    public class EventCatalogTests
    {
        [Fact]
        public void EveryTypeIsUniqueAndInAKnownCategory()
        {
            Assert.Equal(SrsEventTypes.All.Count, SrsEventTypes.All.Select(t => t.Type).Distinct().Count());
            Assert.All(SrsEventTypes.All, t =>
            {
                Assert.Contains(t.Category, EventCategory.All);
                Assert.StartsWith(t.Category + ".", t.Type, StringComparison.Ordinal);
                Assert.Same(t, SrsEventTypes.Find(t.Type));
                Assert.Equal(t.Fields.Count, t.Fields.Select(f => f.Path).Distinct().Count());
            });
            Assert.Null(SrsEventTypes.Find("client.unknown"));
        }

        [Fact]
        public void ClientPayloadMatchesTheDocumentedFields()
        {
            var client = new SRClient
            {
                ClientGuid = "guid-1", Name = "Pilot", Coalition = 1, AssignedCallsign = "---", AssignedVehicle = "Bf 109"
            };

            var srsEvent = SrsEvent.Create(SrsEventTypes.ClientConnected, null, "connected",
                EventPayload.Client(client, "203.0.113.7"));

            Assert.Equal("system", srsEvent.Actor);
            Assert.Equal("guid-1", srsEvent.ClientGuid);
            var payload = srsEvent.Payload.GetProperty("client");
            Assert.Equal("Red", payload.GetProperty("coalitionName").GetString());
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("callsign").ValueKind);
            Assert.Equal("Bf 109", payload.GetProperty("vehicle").GetString());
            Assert.Equal("203.0.113.7", payload.GetProperty("ip").GetString());
            Assert.All(SrsEventTypes.ClientConnected.Fields, field =>
                Assert.True(Resolve(srsEvent.Payload, field.Path).HasValue, field.Path));
        }

        private static JsonElement? Resolve(JsonElement element, string path)
        {
            foreach (var part in path.Split('.'))
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(part, out element))
                {
                    return null;
                }
            }

            return element;
        }
    }

    public class DatabaseMigrationTests
    {
        [Fact]
        public void UpgradesAVersionOneDatabaseAndKeepsItsEvents()
        {
            using var temp = new TempDirectory();
            var path = temp.File(SrsDatabase.FileName);
            using (var connection = new SqliteConnection("Data Source=" + path + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
CREATE TABLE events (id INTEGER PRIMARY KEY AUTOINCREMENT, occurred_at TEXT NOT NULL, category TEXT NOT NULL,
    actor TEXT NOT NULL, message TEXT NOT NULL);
INSERT INTO events (occurred_at, category, actor, message) VALUES ('2026-01-01T00:00:00.0000000Z', 'admin', 'admin', 'Old event');
PRAGMA user_version = 1;";
                command.ExecuteNonQuery();
            }

            var database = new SrsDatabase(path);
            Assert.False(database.WasCreated);

            var audit = new Audit.AuditLog(database, new Audit.AuditRetention(), new ServerEvents());
            var legacy = Assert.Single(audit.Query());
            Assert.Equal("Old event", legacy.Message);
            Assert.Null(legacy.Type);
            Assert.Null(legacy.Payload);

            using var check = database.Open();
            using var version = check.CreateCommand();
            version.CommandText = "PRAGMA user_version;";
            Assert.Equal((long)SrsDatabase.SchemaVersion, (long)version.ExecuteScalar());
        }

        [Theory]
        // Upstream 1.0.5.0: version 2 is the coalition channel names, without typed events.
        [InlineData(2, false)]
        // This fork's databases before the integrations plugin: version 5, with typed events and integration
        // tables (which the plugin moves out), here without the coalition names.
        [InlineData(5, true)]
        // A newer build or another branch numbered it past this build's migrations, without some of its tables.
        [InlineData(99, false)]
        public void UpgradesDatabasesNumberedByOtherBuilds(int userVersion, bool hasTypedEvents)
        {
            using var temp = new TempDirectory();
            var path = temp.File(SrsDatabase.FileName);
            using (var connection = new SqliteConnection("Data Source=" + path + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
CREATE TABLE events (id INTEGER PRIMARY KEY AUTOINCREMENT, occurred_at TEXT NOT NULL, category TEXT NOT NULL,
    actor TEXT NOT NULL, message TEXT NOT NULL" + (hasTypedEvents ? ", type TEXT, client_guid TEXT, payload TEXT" : "") + @");
CREATE TABLE channel_names (channel INTEGER PRIMARY KEY, name TEXT NOT NULL);
" + (hasTypedEvents
                    ? "CREATE TABLE integrations (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL);"
                    : "CREATE TABLE coalition_channel_names (coalition INTEGER NOT NULL, channel INTEGER NOT NULL, " +
                      "name TEXT NOT NULL, PRIMARY KEY (coalition, channel));") + @"
PRAGMA user_version = " + userVersion + ";";
                command.ExecuteNonQuery();
            }

            var database = new SrsDatabase(path);

            using var check = database.Open();
            foreach (var (table, column) in new[]
                     {
                         ("events", "payload"), ("events", "client_guid"), ("coalition_channel_names", "name")
                     })
            {
                using var columns = check.CreateCommand();
                columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
                columns.Parameters.AddWithValue("$table", table);
                columns.Parameters.AddWithValue("$column", column);
                Assert.True((long)columns.ExecuteScalar() == 1, $"{table}.{column} is missing");
            }

            using var version = check.CreateCommand();
            version.CommandText = "PRAGMA user_version;";
            Assert.Equal((long)SrsDatabase.SchemaVersion, (long)version.ExecuteScalar());
        }

        [Fact]
        public void RunningEveryMigrationAgainChangesNothing()
        {
            using var temp = new TempDirectory();
            var path = temp.File(SrsDatabase.FileName);
            var database = new SrsDatabase(path);
            new ServerSettingsStore(database, new Dictionary<string, string>()).SetChannelNames(new Dictionary<int, string> { [1] = "Ops" });
            using (var connection = database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO events (occurred_at, category, actor, message, type) " +
                                      "VALUES ('2026-01-01T00:00:00.0000000Z', 'admin', 'admin', 'Kept', 'admin.test');";
                command.ExecuteNonQuery();
            }

            var before = Snapshot(database);

            // As when a database numbered by another build is opened: every migration runs again.
            SetVersion(database, 0);
            var reopened = new SrsDatabase(path);

            Assert.Equal(before, Snapshot(reopened));
        }

        [Fact]
        public void RunsStatementsAsWrittenSemicolonsIncluded()
        {
            using var temp = new TempDirectory();
            var database = new SqliteDatabase(temp.File("notes.db"), new[]
            {
                new[]
                {
                    "CREATE TABLE IF NOT EXISTS notes (id INTEGER PRIMARY KEY, text TEXT NOT NULL)",
                    "INSERT OR IGNORE INTO notes (id, text) VALUES (1, 'Welcome; be nice')"
                }
            });

            using var connection = database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT text FROM notes;";
            Assert.Equal("Welcome; be nice", command.ExecuteScalar());
        }

        /// <summary>Every table's definition and rows, as text.</summary>
        internal static string Snapshot(SqliteDatabase database)
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

        private static void SetVersion(SqliteDatabase database, int version)
        {
            using (var connection = database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version = " + version + ";";
                command.ExecuteNonQuery();
            }

            SqliteDatabase.ClearPool(database.DatabasePath);
        }
    }

    public class ClientEventRecorderTests
    {
        private static readonly IReadOnlyDictionary<string, string> NoOverrides = new Dictionary<string, string>();

        [Fact]
        public void RecordsConnectionsWithTheClientAddress()
        {
            using var temp = new TempDirectory();
            var (events, published, recorder) = Create(temp);
            using var _ = recorder;
            var client = Pilot();

            events.PublishClientConnected(client, "203.0.113.7");
            events.PublishClientDisconnected(client);
            events.PublishConnectionRejected("203.0.113.8", "banned");

            Assert.Equal(new[] { "client.connected", "client.disconnected", "client.rejected" },
                published.Select(e => e.Type));
            Assert.Equal("203.0.113.7",
                published[1].Payload.GetProperty("client").GetProperty("ip").GetString());
            Assert.Equal("banned", published[2].Payload.GetProperty("reason").GetString());
        }

        [Fact]
        public void RecordsNameAndCoalitionChanges()
        {
            using var temp = new TempDirectory();
            var (events, published, recorder) = Create(temp);
            using var _ = recorder;
            var client = Pilot();
            client.Name = "New Name";
            client.Coalition = 2;

            events.PublishClientUpdated(client, "Pilot", 1, null);

            Assert.Equal(new[] { "client.name_changed", "client.coalition_changed" }, published.Select(e => e.Type));
            Assert.Equal("Pilot", published[0].Payload.GetProperty("previousName").GetString());
            Assert.Equal("New Name moved from Red to Blue", published[1].Message);
            Assert.Equal("Red", published[1].Payload.GetProperty("previousCoalitionName").GetString());
        }

        [Fact]
        public void ReportsARadioChangeOnceItHasSettled()
        {
            using var temp = new TempDirectory();
            var (events, published, recorder) = Create(temp, new Dictionary<int, string> { [4] = "Tower" });
            using var _ = recorder;
            var client = Pilot();
            var start = DateTime.UtcNow;

            // Turning the dial from channel 1 through 2 and 3 to 4.
            var previous = client.GameState;
            foreach (var channel in new[] { 2, 3, 4 })
            {
                client.GameState = OnChannel(channel);
                events.PublishClientUpdated(client, client.Name, client.Coalition, previous);
                previous = client.GameState;
            }

            recorder.FlushRadioChanges(start);
            Assert.Empty(published);

            recorder.FlushRadioChanges(start + ClientEventRecorder.RadioSettleTime + TimeSpan.FromSeconds(1));
            var tuned = Assert.Single(published);
            Assert.Equal("radio.frequency_changed", tuned.Type);
            Assert.Equal(EventCategory.Radio, tuned.Category);
            Assert.Equal("Pilot (Red) tuned radio 1 to channel 4 \"Tower\" (254.000 MHz)", tuned.Message);
            var radio = tuned.Payload.GetProperty("radio");
            Assert.Equal(1, radio.GetProperty("index").GetInt32());
            Assert.Equal(4, radio.GetProperty("channel").GetInt32());
            Assert.Equal(1, radio.GetProperty("previousChannel").GetInt32());
            Assert.Equal(254.0, radio.GetProperty("frequencyMhz").GetDouble());
            Assert.Equal(251.0, radio.GetProperty("previousFrequencyMhz").GetDouble());
        }

        [Fact]
        public void RadioEventUsesThePilotsCoalitionChannelName()
        {
            using var temp = new TempDirectory();
            var (events, published, recorder) = Create(temp,
                alliesChannelNames: new Dictionary<int, string> { [4] = "Allied Tower" });
            using var _ = recorder;
            var client = Pilot();
            var previous = client.GameState;
            client.GameState = OnChannel(4);

            events.PublishClientUpdated(client, client.Name, client.Coalition, previous);
            recorder.FlushRadioChanges(DateTime.UtcNow + TimeSpan.FromMinutes(1));

            var tuned = Assert.Single(published);
            Assert.Contains("\"Allied Tower\"", tuned.Message);
            Assert.Equal("Allied Tower", tuned.Payload.GetProperty("radio").GetProperty("channelName").GetString());
        }

        [Fact]
        public void IgnoresARadioTunedAwayAndBack()
        {
            using var temp = new TempDirectory();
            var (events, published, recorder) = Create(temp);
            using var _ = recorder;
            var client = Pilot();
            var original = client.GameState;

            client.GameState = OnChannel(2);
            events.PublishClientUpdated(client, client.Name, client.Coalition, original);
            var changed = client.GameState;
            client.GameState = OnChannel(1);
            events.PublishClientUpdated(client, client.Name, client.Coalition, changed);

            recorder.FlushRadioChanges(DateTime.UtcNow + TimeSpan.FromMinutes(1));
            Assert.Empty(published);
        }

        [Fact]
        public void ReportsARadioTurnedOff()
        {
            using var temp = new TempDirectory();
            var (events, published, recorder) = Create(temp);
            using var _ = recorder;
            var client = Pilot();
            var previous = client.GameState;
            client.GameState = OnChannel(1);
            client.GameState.radios[1].modulation = RadioInformation.Modulation.DISABLED;

            events.PublishClientUpdated(client, client.Name, client.Coalition, previous);
            recorder.FlushRadioChanges(DateTime.UtcNow + TimeSpan.FromMinutes(1));

            var off = Assert.Single(published);
            Assert.Equal("Pilot (Red) turned off radio 1", off.Message);
            Assert.False(off.Payload.GetProperty("radio").GetProperty("enabled").GetBoolean());
        }

        [Fact]
        public void DropsPendingRadioChangesWhenTheClientLeaves()
        {
            using var temp = new TempDirectory();
            var (events, published, recorder) = Create(temp);
            using var _ = recorder;
            var client = Pilot();
            var previous = client.GameState;
            client.GameState = OnChannel(3);

            events.PublishClientUpdated(client, client.Name, client.Coalition, previous);
            events.PublishClientDisconnected(client);
            recorder.FlushRadioChanges(DateTime.UtcNow + TimeSpan.FromMinutes(1));

            Assert.Equal(new[] { "client.disconnected" }, published.Select(e => e.Type));
        }

        private static (ServerEvents, List<SrsEvent>, ClientEventRecorder) Create(TempDirectory temp,
            IDictionary<int, string> channelNames = null, IDictionary<int, string> alliesChannelNames = null)
        {
            var settings = new ServerSettingsStore(temp.CreateDatabase(), NoOverrides);
            if (channelNames != null)
            {
                settings.SetChannelNames(channelNames);
            }

            if (alliesChannelNames != null)
            {
                settings.SetChannelNames(ChannelNameSettings.AlliesCoalition, alliesChannelNames);
            }

            var events = new ServerEvents();
            var published = new List<SrsEvent>();
            events.EventPublished += published.Add;
            return (events, published, new ClientEventRecorder(events, settings));
        }

        private static SRClient Pilot()
        {
            return new SRClient { ClientGuid = "guid-1", Name = "Pilot", Coalition = 1, GameState = OnChannel(1) };
        }

        private static PlayerGameState OnChannel(int channel)
        {
            var state = new PlayerGameState();
            state.radios[1].freq = PlayerGameState.START_FREQ + PlayerGameState.CHANNEL_OFFSET * channel;
            return state;
        }
    }
}
