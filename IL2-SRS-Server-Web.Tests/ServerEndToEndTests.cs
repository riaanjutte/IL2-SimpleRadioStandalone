using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Api;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Hosting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Tests
{
    /// <summary>
    /// Starts the real server (admin web host, SRS TCP and UDP listeners, SQLite) on free ports
    /// and talks to it the way an SRS client and an API consumer would.
    /// </summary>
    [Collection(ServerCollection.Name)]
    public class ServerEndToEndTests : IAsyncLifetime
    {
        private const string ClientGuid = "EndToEndClientGuid0001";

        private readonly TempDirectory _data = new TempDirectory();
        private WebApplication _app;
        private HttpClient _http;
        private int _srsPort;

        public async ValueTask InitializeAsync()
        {
            _srsPort = FindFreePort();
            ServerPaths.UseDataDirectory(_data.Path);
            File.WriteAllText(_data.File("server.cfg"), "[General Settings]\nCHANNEL_LIMIT=9\n[Channel Names]\n1=Ops\n");

            _app = Program.BuildApp(StartupArguments.Parse(new[]
            {
                "--urls=http://127.0.0.1:0",
                "--SRS_ADMIN_PASSWORD=correct-horse",
                "--SRS_SERVER_PORT=" + _srsPort
            }), builder =>
            {
                // What a host building on the server does: add an admin link.
                builder.Services.AddAdminNavItem("workflows", "Workflows");
            });
            await _app.StartAsync(TestContext.Current.CancellationToken);

            var address = _app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>().Addresses.First();
            _http = new HttpClient { BaseAddress = new Uri(address) };
        }

        public async ValueTask DisposeAsync()
        {
            _http?.Dispose();
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }

            _data.Dispose();
        }

        [Fact]
        public void CreatesThePluginsFolderInTheDataDirectory()
        {
            Assert.True(Directory.Exists(Path.Combine(_data.Path, ServerPlugins.FolderName)));
        }

        [Fact]
        public async Task LoginShowsPasswordErrorsOnTheField()
        {
            var token = TestContext.Current.CancellationToken;
            var login = await _http.GetStringAsync("/login", token);
            Assert.DoesNotContain("aria-invalid", login);
            var antiforgery = System.Text.RegularExpressions.Regex
                .Match(login, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(antiforgery);

            async Task<string> LogIn(string password)
            {
                using var form = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                    new System.Collections.Generic.KeyValuePair<string, string>("returnUrl", "/"),
                    new System.Collections.Generic.KeyValuePair<string, string>("password", password)
                });
                using var response = await _http.PostAsync("/account/login", form, token);
                Assert.Contains("/login", response.RequestMessage!.RequestUri!.AbsolutePath);
                return await response.Content.ReadAsStringAsync(token);
            }

            var empty = await LogIn("");
            Assert.Contains("Enter the admin password.", empty);
            Assert.Contains("aria-invalid=\"true\"", empty);

            var wrong = await LogIn("wrong-password");
            Assert.Contains("Incorrect password.", wrong);
            Assert.Contains("aria-describedby=\"password-error\"", wrong);
        }

        [Fact]
        public async Task ChangingThePasswordSignsOutExistingSessions()
        {
            var token = TestContext.Current.CancellationToken;
            var cookies = new CookieContainer();

            async Task<string> Open(string path)
            {
                using var http = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = _http.BaseAddress };
                using var response = await http.GetAsync(path, token);
                return response.RequestMessage!.RequestUri!.AbsolutePath;
            }

            using (var http = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = _http.BaseAddress })
            {
                var login = await http.GetStringAsync("/login", token);
                var antiforgery = System.Text.RegularExpressions.Regex
                    .Match(login, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
                using var form = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                    new System.Collections.Generic.KeyValuePair<string, string>("returnUrl", "/clients"),
                    new System.Collections.Generic.KeyValuePair<string, string>("password", "correct-horse")
                });
                using var response = await http.PostAsync("/account/login", form, token);
                Assert.Equal("/clients", response.RequestMessage!.RequestUri!.AbsolutePath);
            }

            // The same server, restarted with a new password: the old login cookie no longer works.
            await _app.StopAsync(token);
            await _app.DisposeAsync();
            _http.Dispose();
            _app = Program.BuildApp(StartupArguments.Parse(new[]
            {
                "--urls=http://127.0.0.1:0",
                "--SRS_ADMIN_PASSWORD=battery-staple",
                "--SRS_SERVER_PORT=" + FindFreePort()
            }));
            await _app.StartAsync(token);
            _http = new HttpClient
            {
                BaseAddress = new Uri(_app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>().Addresses.First())
            };

            Assert.Equal("/login", await Open("/clients"));
        }

        [Fact]
        public async Task LogsTheBrowsersAddressFromATrustedProxy()
        {
            var token = TestContext.Current.CancellationToken;
            var (_, readKey) = _app.Services.GetRequiredService<ApiKeyStore>().Create("reader", ApiScope.Read);
            var login = await _http.GetStringAsync("/login", token);
            var antiforgery = System.Text.RegularExpressions.Regex
                .Match(login, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;

            // The test connects from 127.0.0.1, which is always trusted, as a reverse proxy on the same machine.
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/account/login"))
            {
                request.Headers.Add("X-Forwarded-For", "203.0.113.9");
                request.Content = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                    new System.Collections.Generic.KeyValuePair<string, string>("password", "wrong-password")
                });
                using var response = await _http.SendAsync(request, token);
            }

            for (var i = 0; i < 50; i++)
            {
                using var events = Authorized(HttpMethod.Get, "/api/v1/events?type=auth.login_failed", readKey);
                using var response = await _http.SendAsync(events, token);
                var list = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                if (list.GetArrayLength() > 0)
                {
                    Assert.Equal("Failed admin login from 203.0.113.9", list[0].GetProperty("message").GetString());
                    return;
                }

                await Task.Delay(100, token);
            }

            Assert.Fail("The failed login was not logged.");
        }

        [Fact]
        public async Task UnknownAddressesShowTheNotFoundPageExceptInTheApi()
        {
            var token = TestContext.Current.CancellationToken;

            foreach (var path in new[] { "/does-not-exist", "/missing/page.html", "/textures/missing.svg" })
            {
                using var page = await _http.GetAsync(path, token);
                Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
                Assert.Contains("Page not found", await page.Content.ReadAsStringAsync(token));
            }

            using var api = await _http.GetAsync("/api/v1/does-not-exist", token);
            Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
            Assert.DoesNotContain("Page not found", await api.Content.ReadAsStringAsync(token));
        }

        [Fact]
        public async Task ApiRequiresAKeyWithTheRightScope()
        {
            var keys = _app.Services.GetRequiredService<ApiKeyStore>();
            var (_, readKey) = keys.Create("reader", ApiScope.Read);
            var token = TestContext.Current.CancellationToken;

            using (var anonymous = await _http.GetAsync("/api/v1/status", token))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            }

            using var request = Authorized(HttpMethod.Get, "/api/v1/status", readKey);
            using var status = await _http.SendAsync(request, token);
            status.EnsureSuccessStatusCode();
            var body = await status.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal("RUNNING", body.GetProperty("status").GetString());
            Assert.Equal(_srsPort, body.GetProperty("port").GetInt32());

            using var write = Authorized(HttpMethod.Put, "/api/v1/settings/CHANNEL_LIMIT", readKey);
            write.Content = JsonContent.Create(new { value = "10" });
            using var forbidden = await _http.SendAsync(write, token);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        [Fact]
        public async Task SrsClientSeesImportedSettingsAndApiChanges()
        {
            var token = TestContext.Current.CancellationToken;
            var (_, writeKey) = _app.Services.GetRequiredService<ApiKeyStore>().Create("writer", ApiScope.Write);

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, _srsPort, token);
            var stream = tcp.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);

            await SendAsync(stream, "{\"ServerType\":\"IL2-SRS\",\"Version\":\"1.0.4.12\",\"MsgType\":2," +
                                    "\"Client\":{\"ClientGuid\":\"" + ClientGuid + "\",\"Name\":\"Test Pilot\",\"Coalition\":1}}", token);
            var sync = await ReadMessageAsync(reader, token);
            Assert.Equal(2, sync.GetProperty("MsgType").GetInt32());
            Assert.Equal("9", sync.GetProperty("ServerSettings").GetProperty("CHANNEL_LIMIT").GetString());
            Assert.Equal("Ops", sync.GetProperty("ServerSettings").GetProperty("CHANNEL_NAME_1").GetString());

            using (var udp = new UdpClient(AddressFamily.InterNetwork))
            {
                var ping = Encoding.ASCII.GetBytes(ClientGuid);
                await udp.SendAsync(ping, new IPEndPoint(IPAddress.Loopback, _srsPort), token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var echo = await udp.ReceiveAsync(timeout.Token);
                Assert.Equal(ClientGuid, Encoding.ASCII.GetString(echo.Buffer));
            }

            using (var clients = Authorized(HttpMethod.Get, "/api/v1/clients", writeKey))
            using (var response = await _http.SendAsync(clients, token))
            {
                var list = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                var client = Assert.Single(list.EnumerateArray());
                Assert.Equal("Test Pilot", client.GetProperty("name").GetString());
                Assert.Equal("Red", client.GetProperty("coalition").GetString());
                Assert.True(client.GetProperty("voiceLink").GetBoolean());
            }

            // A second client from the same address, as two players behind one router.
            using var neighbour = new TcpClient();
            await neighbour.ConnectAsync(IPAddress.Loopback, _srsPort, token);
            var neighbourReader = new StreamReader(neighbour.GetStream(), Encoding.UTF8);
            await SendAsync(neighbour.GetStream(), "{\"ServerType\":\"IL2-SRS\",\"Version\":\"1.0.4.12\",\"MsgType\":2," +
                                                   "\"Client\":{\"ClientGuid\":\"EndToEndClientGuid0002\",\"Name\":\"Wingman\",\"Coalition\":1}}", token);
            Assert.Equal(2, (await ReadMessageAsync(neighbourReader, token)).GetProperty("MsgType").GetInt32());

            // A settings change through the API is pushed to connected clients.
            using (var change = Authorized(HttpMethod.Put, "/api/v1/settings/CHANNEL_LIMIT", writeKey))
            {
                change.Content = JsonContent.Create(new { value = "15" });
                using var response = await _http.SendAsync(change, token);
                response.EnsureSuccessStatusCode();
            }

            // Skip the UPDATE broadcast that follows a SYNC; the next SERVER_SETTINGS (4) carries the change.
            JsonElement pushed;
            do
            {
                pushed = await ReadMessageAsync(reader, token);
            } while (pushed.GetProperty("MsgType").GetInt32() != 4);

            Assert.Equal("15", pushed.GetProperty("ServerSettings").GetProperty("CHANNEL_LIMIT").GetString());

            // Banning through the API disconnects the client and blocks the address.
            using (var ban = Authorized(HttpMethod.Post, $"/api/v1/clients/{ClientGuid}/ban", writeKey))
            {
                ban.Content = JsonContent.Create(new { reason = "test", durationMinutes = 5 });
                using var response = await _http.SendAsync(ban, token);
                var record = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                Assert.Equal("127.0.0.1", record.GetProperty("ipAddress").GetString());
                Assert.True(record.GetProperty("active").GetBoolean());
            }

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                Assert.Null(await reader.ReadLineAsync(timeout.Token));
                // The other client from the banned address goes too (after any messages already sent to it).
                while (await neighbourReader.ReadLineAsync(timeout.Token) != null)
                {
                }
            }

            await Task.Delay(1500, token);
            using var events = Authorized(HttpMethod.Get, "/api/v1/events?category=admin", writeKey);
            using var eventResponse = await _http.SendAsync(events, token);
            var eventList = await eventResponse.Content.ReadFromJsonAsync<JsonElement>(token);
            var banned = Assert.Single(eventList.EnumerateArray(), e =>
                e.GetProperty("actor").GetString() == "api:writer" &&
                e.GetProperty("message").GetString().StartsWith("Banned Test Pilot (Red)", StringComparison.Ordinal));
            Assert.Equal("admin.client_banned", banned.GetProperty("type").GetString());
            var payload = banned.GetProperty("payload");
            Assert.Equal("Test Pilot", payload.GetProperty("client").GetProperty("name").GetString());
            Assert.Equal("Red", payload.GetProperty("client").GetProperty("coalitionName").GetString());
            Assert.Equal("127.0.0.1", payload.GetProperty("ban").GetProperty("ip").GetString());

            using var connected = Authorized(HttpMethod.Get, "/api/v1/events?type=client.connected", writeKey);
            using var connectedResponse = await _http.SendAsync(connected, token);
            var connectedList = await connectedResponse.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Contains(connectedList.EnumerateArray(), e =>
                e.GetProperty("payload").GetProperty("client").GetProperty("ip").GetString() == "127.0.0.1");

            using var types = Authorized(HttpMethod.Get, "/api/v1/event-types", writeKey);
            using var typesResponse = await _http.SendAsync(types, token);
            var typeList = await typesResponse.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Contains(typeList.EnumerateArray(), t => t.GetProperty("type").GetString() == "radio.frequency_changed");
        }

        internal static HttpRequestMessage Authorized(HttpMethod method, string path, string key)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            return request;
        }

        private static async Task SendAsync(NetworkStream stream, string json, CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, token);
        }

        private static async Task<JsonElement> ReadMessageAsync(StreamReader reader, CancellationToken token)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var line = await reader.ReadLineAsync(timeout.Token);
            Assert.NotNull(line);
            return JsonDocument.Parse(line).RootElement.Clone();
        }

        /// <summary>
        /// A port the server can open for both TCP and UDP, bound as the server binds them (any address).
        /// Not a port picked by the OS (port 0): Windows reserves blocks of hundreds of UDP ports inside its
        /// dynamic range for Hyper-V and Docker Desktop, and hands out dynamic ports nearly in order, so a run of
        /// them can all be refused for UDP. Random ports below the dynamic range (49152 and up) avoid those blocks,
        /// and each one is checked for both protocols.
        /// </summary>
        internal static int FindFreePort()
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
}
