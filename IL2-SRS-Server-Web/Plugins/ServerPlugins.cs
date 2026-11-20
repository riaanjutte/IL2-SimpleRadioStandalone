using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins
{
    /// <summary>A plugin the server runs.</summary>
    public sealed record LoadedPlugin(IServerPlugin Plugin, string Version, string Directory)
    {
        public string Id => Plugin.Id;
        public string Name => Plugin.Name;
        public Assembly Assembly => Plugin.GetType().Assembly;
    }

    /// <summary>A plugin folder or plugin the server could not load, and why.</summary>
    public sealed record PluginFailure(string Source, string Error);

    /// <summary>The plugins found at startup.</summary>
    public sealed class PluginCatalog
    {
        public List<LoadedPlugin> Loaded { get; } = new();
        public List<PluginFailure> Failed { get; } = new();
    }

    /// <summary>
    /// Finds, loads and wires up plugins. A plugin is a folder <c>plugins/&lt;name&gt;/</c> holding
    /// <c>&lt;name&gt;.dll</c> (and its dependencies) in the install folder, the data directory, or a folder in
    /// <see cref="PathsKey"/>. Each plugin gets its own load context; assemblies the server has (the SDK,
    /// ASP.NET Core, SQLite, ...) are shared with it.
    /// </summary>
    public static class ServerPlugins
    {
        public const string FolderName = "plugins";

        /// <summary>Plugin data lives in plugin-data/&lt;id&gt; in the data directory.</summary>
        public const string DataFolderName = "plugin-data";

        /// <summary>More folders containing plugin folders, separated by ';' (e.g. a development build).</summary>
        public const string PathsKey = "SRS_PLUGIN_PATHS";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Regex ValidId = new("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant);

        /// <summary>Loads the plugins in the plugin folders.</summary>
        internal static void LoadFromFolders(WebApplicationBuilder builder)
        {
            // Created up front so admins see where to put plugins.
            var dataPlugins = Directory.CreateDirectory(Path.Combine(ServerPaths.DataDirectory, FolderName)).FullName;
            var roots = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, FolderName),
                dataPlugins
            };
            roots.AddRange((builder.Configuration[PathsKey] ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            foreach (var root in roots.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var folder in Directory.GetDirectories(root).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    LoadFolder(builder, folder);
                }
            }
        }

        /// <summary>
        /// Runs a plugin in this server: registers its services, pages and API. Used for plugin folders, and by
        /// hosts and tests that compile a plugin in; <paramref name="directory"/> holds its wwwroot (by default
        /// the folder of its assembly).
        /// </summary>
        public static WebApplicationBuilder AddServerPlugin(this WebApplicationBuilder builder, IServerPlugin plugin,
            string directory = null)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            var catalog = Catalog(builder.Services);
            var source = plugin.GetType().Assembly.GetName().Name;

            if (plugin.Id == null || !ValidId.IsMatch(plugin.Id))
            {
                Fail(catalog, source, $"Plugin id \"{plugin.Id}\" must be lowercase letters, digits and hyphens.");
                return builder;
            }

            if (catalog.Loaded.Any(p => p.Id == plugin.Id))
            {
                Fail(catalog, source, $"Another plugin already uses the id \"{plugin.Id}\".");
                return builder;
            }

            // A plugin that fails partway leaves nothing behind (e.g. a hosted service that would start anyway).
            var servicesBefore = builder.Services.ToList();
            try
            {
                var dataDirectory = Path.Combine(ServerPaths.DataDirectory, DataFolderName, plugin.Id);
                Directory.CreateDirectory(dataDirectory);
                plugin.ConfigureServices(
                    new PluginContext(builder.Configuration, dataDirectory, ServerPaths.DataDirectory),
                    builder.Services);
            }
            catch (Exception ex)
            {
                builder.Services.Clear();
                foreach (var service in servicesBefore)
                {
                    builder.Services.Add(service);
                }

                Fail(catalog, source, "Setting up the plugin failed: " + ex.Message);
                return builder;
            }

            builder.Services.AddAdminPages(plugin.GetType().Assembly);
            var loaded = new LoadedPlugin(plugin,
                plugin.GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?.Split('+')[0] ?? plugin.GetType().Assembly.GetName().Version?.ToString(),
                directory ?? Path.GetDirectoryName(plugin.GetType().Assembly.Location));
            catalog.Loaded.Add(loaded);
            Logger.Info($"Plugin {loaded.Name} ({loaded.Id} {loaded.Version}) loaded from {loaded.Directory}");
            return builder;
        }

        /// <summary>Serves each plugin's wwwroot folder at plugins/&lt;id&gt;/.</summary>
        internal static void UseStaticFiles(WebApplication app)
        {
            foreach (var plugin in app.Services.GetRequiredService<PluginCatalog>().Loaded)
            {
                var wwwroot = plugin.Directory == null ? null : Path.Combine(plugin.Directory, "wwwroot");
                if (wwwroot != null && Directory.Exists(wwwroot))
                {
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        RequestPath = "/" + FolderName + "/" + plugin.Id,
                        FileProvider = new PhysicalFileProvider(wwwroot)
                    });
                }
            }
        }

        /// <summary>Adds each plugin's endpoints to the REST API group.</summary>
        internal static void MapApis(IEndpointRouteBuilder api)
        {
            foreach (var plugin in api.ServiceProvider.GetRequiredService<PluginCatalog>().Loaded)
            {
                plugin.Plugin.MapApi(api);
            }
        }

        internal static PluginCatalog Catalog(IServiceCollection services)
        {
            if (services.FirstOrDefault(d => d.ServiceType == typeof(PluginCatalog))?.ImplementationInstance is
                PluginCatalog existing)
            {
                return existing;
            }

            var catalog = new PluginCatalog();
            services.AddSingleton(catalog);
            return catalog;
        }

        private static void LoadFolder(WebApplicationBuilder builder, string folder)
        {
            var name = Path.GetFileName(folder);
            var assemblyPath = Path.Combine(folder, name + ".dll");
            if (!File.Exists(assemblyPath))
            {
                Fail(Catalog(builder.Services), folder, $"No {name}.dll in the plugin folder.");
                return;
            }

            IServerPlugin plugin;
            try
            {
                var assembly = new PluginLoadContext(assemblyPath).LoadFromAssemblyPath(assemblyPath);
                var sdk = assembly.GetReferencedAssemblies()
                    .FirstOrDefault(a => a.Name == typeof(IServerPlugin).Assembly.GetName().Name);
                var serverSdk = typeof(IServerPlugin).Assembly.GetName().Version;
                if (sdk?.Version != null && sdk.Version.Major != serverSdk.Major)
                {
                    Fail(Catalog(builder.Services), folder,
                        $"Built for plugin SDK {sdk.Version.Major}.x; this server has {serverSdk.Major}.x.");
                    return;
                }

                var types = assembly.GetExportedTypes()
                    .Where(t => typeof(IServerPlugin).IsAssignableFrom(t) && !t.IsAbstract)
                    .ToList();
                if (types.Count != 1)
                {
                    Fail(Catalog(builder.Services), folder,
                        $"{name}.dll must have exactly one public IServerPlugin class; it has {types.Count}.");
                    return;
                }

                plugin = (IServerPlugin)Activator.CreateInstance(types[0]);
            }
            catch (Exception ex)
            {
                Fail(Catalog(builder.Services), folder, "Loading failed: " + ex.Message);
                return;
            }

            builder.AddServerPlugin(plugin, folder);
        }

        private static void Fail(PluginCatalog catalog, string source, string error)
        {
            catalog.Failed.Add(new PluginFailure(source, error));
            Logger.Error($"Plugin {source} not loaded: {error}");
        }

        /// <summary>
        /// Loads a plugin's own dependencies from its folder; anything the server itself ships (or the shared
        /// framework provides) comes from the server, so plugin and server share types such as the SDK's.
        /// </summary>
        private sealed class PluginLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;

            public PluginLoadContext(string assemblyPath)
                : base(Path.GetFileNameWithoutExtension(assemblyPath))
            {
                _resolver = new AssemblyDependencyResolver(assemblyPath);
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll")) ||
                    Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name))
                {
                    return null;
                }

                var path = _resolver.ResolveAssemblyToPath(assemblyName);
                return path == null ? null : LoadFromAssemblyPath(path);
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
            }
        }
    }
}
