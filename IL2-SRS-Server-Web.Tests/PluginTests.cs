using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
    /// <summary>Tests that start a server share ServerPaths, so they run one at a time.</summary>
    [CollectionDefinition(Name)]
    public class ServerCollection
    {
        public const string Name = "Server";
    }

    /// <summary>
    /// Installs the sample plugin the way an admin would (a folder under plugins/ in the data directory),
    /// next to a folder that isn't a plugin, and starts the real server.
    /// </summary>
    [Collection(ServerCollection.Name)]
    public class PluginLoadingTests : IAsyncLifetime
    {
        private const string PluginName = "IL2-SRS-Plugin-Sample";

        private readonly TempDirectory _data = new TempDirectory();
        private WebApplication _app;
        private HttpClient _http;
        private string _key;

        public async ValueTask InitializeAsync()
        {
            ServerPaths.UseDataDirectory(_data.Path);

            // The plugin's build output: its assembly and wwwroot (the SDK comes from the server).
            var folder = Directory.CreateDirectory(Path.Combine(_data.Path, ServerPlugins.FolderName, PluginName)).FullName;
            File.Copy(Path.Combine(AppContext.BaseDirectory, PluginName + ".dll"), Path.Combine(folder, PluginName + ".dll"));
            Directory.CreateDirectory(Path.Combine(folder, "wwwroot"));
            File.Copy(Path.Combine(AppContext.BaseDirectory, "wwwroot", "sample.css"), Path.Combine(folder, "wwwroot", "sample.css"));
            Directory.CreateDirectory(Path.Combine(_data.Path, ServerPlugins.FolderName, "Broken"));

            _app = Program.BuildApp(StartupArguments.Parse(new[]
            {
                "--urls=http://127.0.0.1:0",
                "--SRS_ADMIN_PASSWORD=correct-horse",
                "--SRS_SERVER_PORT=" + ServerEndToEndTests.FindFreePort()
            }));
            await _app.StartAsync(TestContext.Current.CancellationToken);

            _key = _app.Services.GetRequiredService<ApiKeyStore>().Create("writer", ApiScope.Write).Key;
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
        public async Task LoadsPluginFoldersAndReportsTheOnesItCannot()
        {
            var token = TestContext.Current.CancellationToken;
            using var request = ServerEndToEndTests.Authorized(HttpMethod.Get, "/api/v1/plugins", _key);
            using var response = await _http.SendAsync(request, token);
            var plugins = await response.Content.ReadFromJsonAsync<JsonElement>(token);

            var loaded = Assert.Single(plugins.GetProperty("loaded").EnumerateArray());
            Assert.Equal("sample", loaded.GetProperty("id").GetString());
            Assert.Equal("1.0.0", loaded.GetProperty("version").GetString());
            var failed = Assert.Single(plugins.GetProperty("failed").EnumerateArray());
            Assert.EndsWith("Broken", failed.GetProperty("source").GetString());
            Assert.Equal("No Broken.dll in the plugin folder.", failed.GetProperty("error").GetString());

            // Its pages, stylesheet and event types come with it.
            using var css = await _http.GetAsync("/plugins/sample/sample.css", token);
            Assert.Equal(HttpStatusCode.OK, css.StatusCode);
            using var types = ServerEndToEndTests.Authorized(HttpMethod.Get, "/api/v1/event-types", _key);
            using var typesResponse = await _http.SendAsync(types, token);
            var typeList = await typesResponse.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Contains(typeList.EnumerateArray(), t => t.GetProperty("type").GetString() == "sample.pinged");
            using var page = await _http.GetAsync("/sample", token);
            Assert.Equal("/login", page.RequestMessage!.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task PluginEndpointsPublishEventsAndKeepDataInTheirOwnFolder()
        {
            var token = TestContext.Current.CancellationToken;
            using var ping = ServerEndToEndTests.Authorized(HttpMethod.Post, "/api/v1/sample/ping", _key);
            using var response = await _http.SendAsync(ping, token);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            var dataDirectory = Path.Combine(_data.Path, ServerPlugins.DataFolderName, "sample");
            Assert.Equal(dataDirectory, body.GetProperty("dataDirectory").GetString());
            Assert.True(Directory.Exists(dataDirectory));

            for (var i = 0; i < 50; i++)
            {
                using var events = ServerEndToEndTests.Authorized(HttpMethod.Get, "/api/v1/events?type=sample.pinged", _key);
                using var eventResponse = await _http.SendAsync(events, token);
                var list = await eventResponse.Content.ReadFromJsonAsync<JsonElement>(token);
                if (list.GetArrayLength() == 1)
                {
                    Assert.Equal("plugin:sample", list[0].GetProperty("actor").GetString());
                    return;
                }

                await Task.Delay(100, token);
            }

            Assert.Fail("The plugin's event was not logged.");
        }
    }

    /// <summary>A plugin that fails partway through setting up leaves none of its services behind.</summary>
    [Collection(ServerCollection.Name)]
    public class PluginFailureTests
    {
        [Fact]
        public void APluginThatFailsLeavesNoServicesBehind()
        {
            using var data = new TempDirectory();
            ServerPaths.UseDataDirectory(data.Path);

            var builder = WebApplication.CreateBuilder();
            var before = builder.Services.Count;
            builder.AddServerPlugin(new FailingPlugin());

            Assert.Equal(before + 1, builder.Services.Count); // Only the plugin catalog, which reports the failure.
            Assert.DoesNotContain(builder.Services, s => s.ImplementationType == typeof(FailingPlugin.Worker));
            var failed = Assert.Single(ServerPlugins.Catalog(builder.Services).Failed);
            Assert.Equal("Setting up the plugin failed: Missing setting", failed.Error);
        }

        private sealed class FailingPlugin : IServerPlugin
        {
            public string Id => "failing";

            public string Name => "Failing";

            public void ConfigureServices(PluginContext context, IServiceCollection services)
            {
                services.AddHostedService<Worker>();
                throw new InvalidOperationException("Missing setting");
            }

            public void MapApi(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder api)
            {
            }

            internal sealed class Worker : Microsoft.Extensions.Hosting.BackgroundService
            {
                protected override Task ExecuteAsync(System.Threading.CancellationToken stoppingToken) => Task.CompletedTask;
            }
        }
    }
}
