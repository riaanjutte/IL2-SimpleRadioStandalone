using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Api;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Hosting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations.Tests
{
    /// <summary>Tests that start a server share ServerPaths, so they run one at a time.</summary>
    [CollectionDefinition(Name)]
    public class ServerCollection
    {
        public const string Name = "Server";
    }

    /// <summary>Starts the real server with the plugin and talks to its API.</summary>
    public abstract class ServerTest : IAsyncLifetime
    {
        private protected readonly TempDirectory Data = new TempDirectory();
        protected WebApplication App;
        protected HttpClient Http;

        public async ValueTask InitializeAsync()
        {
            ServerPaths.UseDataDirectory(Data.Path);
            Prepare();

            App = Program.BuildApp(StartupArguments.Parse(new[]
            {
                "--urls=http://127.0.0.1:0",
                "--SRS_ADMIN_PASSWORD=correct-horse",
                "--SRS_SERVER_PORT=" + FindFreePort()
            }), Configure);
            await App.StartAsync(TestContext.Current.CancellationToken);

            var address = App.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>().Addresses.First();
            Http = new HttpClient { BaseAddress = new Uri(address) };
        }

        public async ValueTask DisposeAsync()
        {
            Http?.Dispose();
            if (App != null)
            {
                await App.StopAsync();
                await App.DisposeAsync();
            }

            Data.Dispose();
        }

        /// <summary>Sets up the data directory before the server starts.</summary>
        protected virtual void Prepare()
        {
        }

        /// <summary>Adds to the server, as a host building on it would.</summary>
        protected virtual void Configure(WebApplicationBuilder builder)
        {
        }

        protected string CreateKey(string scope) =>
            App.Services.GetRequiredService<ApiKeyStore>().Create(scope == ApiScope.Write ? "writer" : "reader", scope).Key;

        protected static HttpRequestMessage Authorized(HttpMethod method, string path, string key)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            return request;
        }

        /// <summary>
        /// A port the server can open for both TCP and UDP, bound as the server binds them (any address).
        /// Not a port picked by the OS (port 0): Windows reserves blocks of hundreds of UDP ports inside its
        /// dynamic range for Hyper-V and Docker Desktop, and hands out dynamic ports nearly in order, so a run of
        /// them can all be refused for UDP. Random ports below the dynamic range (49152 and up) avoid those blocks,
        /// and each one is checked for both protocols.
        /// </summary>
        private static int FindFreePort()
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var port = Random.Shared.Next(20000, 40000);
                try
                {
                    var tcp = new TcpListener(IPAddress.Any, port);
                    tcp.Start();
                    try
                    {
                        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        udp.Bind(new IPEndPoint(IPAddress.Any, port));
                        return port;
                    }
                    finally
                    {
                        tcp.Stop();
                    }
                }
                catch (SocketException)
                {
                    // In use or reserved for one of the protocols: try another.
                }
            }

            throw new InvalidOperationException("No port is free for both TCP and UDP.");
        }
    }

    /// <summary>A host compiling the plugin in and adding its own action.</summary>
    [Collection(ServerCollection.Name)]
    public class CompiledInPluginTests : ServerTest
    {
        private readonly RecordingAction _action = new RecordingAction();

        protected override void Configure(WebApplicationBuilder builder)
        {
            builder.AddServerPlugin(new IntegrationsPlugin());
            builder.Services.AddSingleton(_action);
            builder.Services.AddIntegrationAction<RecordingAction>();
        }

        [Fact]
        public async Task IntegrationsAddedByAHostRunWhenTheirEventHappens()
        {
            var token = TestContext.Current.CancellationToken;
            var writeKey = CreateKey(ApiScope.Write);

            using (var types = Authorized(HttpMethod.Get, "/api/v1/integrations/action-types", writeKey))
            using (var response = await Http.SendAsync(types, token))
            {
                var list = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                Assert.Contains(list.EnumerateArray(), t => t.GetProperty("type").GetString() == RecordingAction.ActionType);
            }

            long id;
            using (var invalid = Authorized(HttpMethod.Post, "/api/v1/integrations", writeKey))
            {
                invalid.Content = JsonContent.Create(new { name = "", triggers = new[] { "admin.setting_changed" }, action = RecordingAction.ActionType });
                using var response = await Http.SendAsync(invalid, token);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                Assert.Equal("Enter a name.", body.GetProperty("error").GetString());
                Assert.Equal("Enter a token.", body.GetProperty("errors").GetProperty("config.token").GetString());
            }

            using (var create = Authorized(HttpMethod.Post, "/api/v1/integrations", writeKey))
            {
                create.Content = JsonContent.Create(new
                {
                    name = "Channel limit watch",
                    triggers = new[] { "admin.setting_changed" },
                    conditions = new[] { new { field = "setting.key", @operator = "is", value = "CHANNEL_LIMIT" } },
                    action = RecordingAction.ActionType,
                    config = new { token = "host-secret-value" }
                });
                using var response = await Http.SendAsync(create, token);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                var created = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                id = created.GetProperty("id").GetInt64();
                Assert.False(created.GetProperty("config").TryGetProperty("token", out _));
                Assert.Equal("ends in alue", created.GetProperty("storedSecrets").GetProperty("token").GetString());
            }

            foreach (var (key, value) in new[] { ("CHANNEL_LIMIT", "12"), ("COALITION_AUDIO_SECURITY", "true") })
            {
                using var change = Authorized(HttpMethod.Put, "/api/v1/settings/" + key, writeKey);
                change.Content = JsonContent.Create(new { value });
                using var response = await Http.SendAsync(change, token);
                response.EnsureSuccessStatusCode();
            }

            JsonElement run = default;
            for (var i = 0; i < 50; i++)
            {
                using var runs = Authorized(HttpMethod.Get, $"/api/v1/integrations/runs?integration={id}", writeKey);
                using var response = await Http.SendAsync(runs, token);
                var list = (await response.Content.ReadFromJsonAsync<JsonElement>(token)).EnumerateArray().ToList();
                if (list.Count == 1 && list[0].GetProperty("status").GetString() == "succeeded")
                {
                    run = list[0];
                    break;
                }

                await Task.Delay(100, token);
            }

            Assert.Equal(JsonValueKind.Object, run.ValueKind);
            Assert.Equal("12", run.GetProperty("data").GetProperty("setting").GetProperty("value").GetString());
            Assert.Equal("api:writer", run.GetProperty("data").GetProperty("event").GetProperty("actor").GetString());
            var executed = Assert.Single(_action.Runs);
            Assert.Equal("host-secret-value", executed.Config["token"]!.GetValue<string>());
        }
    }

    /// <summary>
    /// Installs the plugin the way an admin would (a folder under plugins/ in the data directory) next to a
    /// server database from before the plugin, and starts the real server.
    /// </summary>
    [Collection(ServerCollection.Name)]
    public class InstalledPluginTests : ServerTest
    {
        private const string PluginName = "IL2-SRS-Plugin-Integrations";

        protected override void Prepare()
        {
            CreateServerDatabaseWithIntegrations(Data.File(SrsDatabase.FileName));

            // The plugin's build output: its assembly and wwwroot (Scriban and the SDK come from the server).
            var built = Path.GetDirectoryName(typeof(IntegrationsPlugin).Assembly.Location)!;
            var folder = Directory.CreateDirectory(Path.Combine(Data.Path, ServerPlugins.FolderName, PluginName)).FullName;
            File.Copy(Path.Combine(built, PluginName + ".dll"), Path.Combine(folder, PluginName + ".dll"));
            Directory.CreateDirectory(Path.Combine(folder, "wwwroot"));
            File.Copy(Path.Combine(built, "wwwroot", "integrations.css"), Path.Combine(folder, "wwwroot", "integrations.css"));
        }

        [Fact]
        public async Task LoadsWithItsPagesStylesheetEventTypesAndApi()
        {
            var token = TestContext.Current.CancellationToken;
            var key = CreateKey(ApiScope.Read);

            using var request = Authorized(HttpMethod.Get, "/api/v1/plugins", key);
            using var response = await Http.SendAsync(request, token);
            var plugins = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            var loaded = Assert.Single(plugins.GetProperty("loaded").EnumerateArray());
            Assert.Equal(IntegrationsPlugin.PluginId, loaded.GetProperty("id").GetString());
            Assert.Equal("1.0.0", loaded.GetProperty("version").GetString());

            using var css = await Http.GetAsync("/plugins/integrations/integrations.css", token);
            Assert.Equal(HttpStatusCode.OK, css.StatusCode);
            using var types = Authorized(HttpMethod.Get, "/api/v1/event-types", key);
            using var typesResponse = await Http.SendAsync(types, token);
            var typeList = await typesResponse.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Contains(typeList.EnumerateArray(), t => t.GetProperty("type").GetString() == "admin.integration_created");
            using var page = await Http.GetAsync("/integrations", token);
            Assert.Equal("/login", page.RequestMessage!.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task MovesIntegrationsFromTheServerDatabase()
        {
            var token = TestContext.Current.CancellationToken;
            using var request = Authorized(HttpMethod.Get, "/api/v1/integrations", CreateKey(ApiScope.Read));
            using var response = await Http.SendAsync(request, token);
            var integration = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>(token)).EnumerateArray());
            Assert.Equal(7, integration.GetProperty("id").GetInt64());
            Assert.Equal("Post joins", integration.GetProperty("name").GetString());

            Assert.True(File.Exists(Path.Combine(Data.Path, ServerPlugins.DataFolderName, IntegrationsPlugin.PluginId,
                IntegrationDatabase.FileName)));
            using var connection = App.Services.GetRequiredService<SrsDatabase>().Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'integration%';";
            Assert.Equal(0L, (long)command.ExecuteScalar());
        }

        /// <summary>A srs.db as this fork's servers left it before the plugin: version 5, integrations included.</summary>
        private static void CreateServerDatabaseWithIntegrations(string path)
        {
            using (var connection = new SqliteConnection("Data Source=" + path + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
CREATE TABLE integrations (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, description TEXT,
    enabled INTEGER NOT NULL, trigger_type TEXT NOT NULL, conditions TEXT NOT NULL, action_type TEXT NOT NULL,
    action_config TEXT NOT NULL, retry_mode TEXT NOT NULL, max_retries INTEGER NOT NULL, created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL);
CREATE TABLE integration_executions (id INTEGER PRIMARY KEY AUTOINCREMENT, integration_id INTEGER NOT NULL,
    integration_name TEXT NOT NULL, action_type TEXT NOT NULL, event_type TEXT NOT NULL, event_message TEXT,
    data TEXT NOT NULL, status TEXT NOT NULL, attempts INTEGER NOT NULL, created_at TEXT NOT NULL,
    next_attempt_at TEXT, finished_at TEXT, last_error TEXT, result TEXT);
CREATE TABLE integration_state (integration_id INTEGER NOT NULL, key TEXT NOT NULL, value TEXT NOT NULL,
    updated_at TEXT NOT NULL, PRIMARY KEY (integration_id, key));
INSERT INTO integrations VALUES (7, 'Post joins', NULL, 1, 'client.connected', '[]', 'http-webhook',
    '{""url"":null}', 'none', 0, '2026-10-12T18:00:00.0000000Z', '2026-10-12T18:00:00.0000000Z');
PRAGMA user_version = 5;";
                command.ExecuteNonQuery();
            }
        }
    }
}
