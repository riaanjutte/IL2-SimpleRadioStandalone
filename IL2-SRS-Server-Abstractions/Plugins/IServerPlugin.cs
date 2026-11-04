using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins
{
    /// <summary>
    /// A server plugin. The server looks for one public class implementing this (with a parameterless
    /// constructor) in <c>plugins/&lt;name&gt;/&lt;name&gt;.dll</c> under its data directory or install folder,
    /// and loads it before the server starts. Plugins run in the server process with its permissions.
    /// </summary>
    public interface IServerPlugin
    {
        /// <summary>
        /// Short lowercase id, e.g. "integrations": the plugin's data folder (<see cref="PluginContext.DataDirectory"/>)
        /// and the address of its static files (plugins/&lt;id&gt;/... from the plugin's wwwroot folder).
        /// </summary>
        string Id { get; }

        /// <summary>Name shown on the Plugins page.</summary>
        string Name { get; }

        /// <summary>
        /// Registers the plugin's services: hosted services, event types (<see cref="EventTypeExtensions.AddEventTypes"/>),
        /// navigation links and stylesheets (<see cref="AdminExtensions"/>). Routable components in the plugin's
        /// assembly become admin pages. Server services (<see cref="Events.IEventBus"/>, <see cref="IServerInfo"/>,
        /// <see cref="IAdminActor"/>, ...) can be injected.
        /// </summary>
        void ConfigureServices(PluginContext context, IServiceCollection services);

        /// <summary>
        /// Adds REST endpoints under /api/v1. They require an API key with the read scope; require
        /// <see cref="ServerApi.WritePolicy"/> on endpoints that change something.
        /// </summary>
        void MapApi(IEndpointRouteBuilder api)
        {
        }
    }

    /// <summary>Where a plugin runs.</summary>
    public sealed class PluginContext
    {
        public PluginContext(IConfiguration configuration, string dataDirectory, string serverDataDirectory)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            DataDirectory = dataDirectory;
            ServerDataDirectory = serverDataDirectory;
        }

        /// <summary>The server's configuration: environment, srs-server.json and command line.</summary>
        public IConfiguration Configuration { get; }

        /// <summary>The plugin's own folder for its data (plugin-data/&lt;id&gt; in the data directory); it exists.</summary>
        public string DataDirectory { get; }

        /// <summary>The server's data directory.</summary>
        public string ServerDataDirectory { get; }
    }

    /// <summary>Which server this is.</summary>
    public interface IServerInfo
    {
        /// <summary>The Server Name setting, or the host name when it isn't set.</summary>
        string Name { get; }

        /// <summary>The machine name; in a container its hostname, the container ID unless the stack sets one.</summary>
        string Hostname { get; }

        /// <summary>The image version, e.g. 1.0.5.0-preview, or the server version outside Docker.</summary>
        string Version { get; }

        /// <summary>The Public Admin URL setting, or null.</summary>
        string AdminUrl { get; }
    }

    /// <summary>Who is using the admin UI, for the actor of events it causes.</summary>
    public interface IAdminActor
    {
        /// <summary>Name recorded in the event log for changes made from the admin UI.</summary>
        string Name { get; }
    }

    /// <summary>Changes admin pages showing live server state refresh on.</summary>
    public interface IServerStatusEvents
    {
        /// <summary>A client connected, disconnected, or changed name, coalition or callsign.</summary>
        event Action ClientsChanged;

        /// <summary>The server was started or stopped.</summary>
        event Action RunningStateChanged;
    }

    /// <summary>The REST API's authorization policies, for plugin endpoints.</summary>
    public static class ServerApi
    {
        /// <summary>An API key with the read or write scope; every endpoint under /api/v1 requires it.</summary>
        public const string ReadPolicy = "api-read";

        /// <summary>An API key with the write scope; require it on endpoints that change something.</summary>
        public const string WritePolicy = "api-write";
    }
}
