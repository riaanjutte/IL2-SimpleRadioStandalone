using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Admin;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Api;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Audit;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Components;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Components.Shared;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Hosting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using Scalar.AspNetCore;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server
{
    public static class Program
    {
        public const string ServiceName = "IL2-SRS-Server";
        public const string ConfigFileName = "srs-server.json";
        public const string DefaultLocalUrl = "http://localhost:8080";

        /// <summary>Reverse proxies whose X-Forwarded-For and X-Forwarded-Proto headers are trusted.</summary>
        public const string TrustedProxiesKey = "SRS_TRUSTED_PROXIES";

        public static int Main(string[] args) => Run(args);

        /// <summary>
        /// Runs the server. Hosts that build on it (for example to compile plugins in with
        /// <see cref="ServerPlugins.AddServerPlugin"/>) call this from their own Main with <paramref name="configure"/>,
        /// which runs after the server's own services are registered and the plugin folders are loaded.
        /// </summary>
        public static int Run(string[] args, Action<WebApplicationBuilder> configure = null)
        {
            if (args.Length == 1 && args[0] == HealthCheckCommand.Argument)
            {
                return HealthCheckCommand.Run();
            }

            StartupArguments startup;
            try
            {
                startup = StartupArguments.Parse(args);
                ServerPaths.UseDataDirectory(string.IsNullOrWhiteSpace(startup.DataDirectory)
                    ? ServerPaths.ResolveDefaultDataDirectory()
                    : startup.DataDirectory);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            LogManager.Configuration = ServerLogging.Create(ServerPaths.DataDirectory);
            var logger = LogManager.GetCurrentClassLogger();

            try
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                logger.Info($"IL2-SRS Server starting. Data directory: {ServerPaths.DataDirectory}");

                var app = BuildApp(startup, configure);
                app.Run();
                return Environment.ExitCode;
            }
            catch (Exception ex) when (FindStartupException(ex) is ServerStartupException startupError)
            {
                logger.Fatal("IL2-SRS Server cannot start: " + startupError.Message +
                             (startupError.InnerException != null ? " (" + startupError.InnerException.Message + ")" : string.Empty));
                return 1;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "IL2-SRS Server stopped because of an error: " + ex.Message);
                return 1;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        public static WebApplication BuildApp(StartupArguments startup, Action<WebApplicationBuilder> configure = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = startup.HostArguments,
                // A Windows service starts in System32; always serve from the install folder.
                ContentRootPath = AppContext.BaseDirectory
            });

            // Same SRS_* keys as environment variables, for hosts where those are awkward (Windows services).
            builder.Configuration.AddJsonFile(Path.Combine(ServerPaths.DataDirectory, ConfigFileName), optional: true,
                reloadOnChange: false);
            builder.Configuration.AddEnvironmentVariables();
            builder.Configuration.AddCommandLine(startup.HostArguments);

            builder.Logging.ClearProviders();
            builder.Host.UseNLog();
            builder.Services.AddWindowsService(options => options.ServiceName = ServiceName);

            if (string.IsNullOrEmpty(builder.Configuration["urls"]) &&
                string.IsNullOrEmpty(builder.Configuration["http_ports"]) &&
                string.IsNullOrEmpty(builder.Configuration["https_ports"]))
            {
                // Outside a container, only listen locally unless told otherwise.
                builder.WebHost.UseUrls(DefaultLocalUrl);
            }

            var adminAuth = AdminAuthOptions.FromConfiguration(builder.Configuration);
            if (adminAuth.Disabled)
            {
                LogManager.GetCurrentClassLogger().Warn(
                    $"{AdminAuthOptions.DisabledKey}=true: the admin UI is not password protected. Restrict access to it another way.");
            }

            AddServerData(builder.Services, builder.Configuration, startup);

            builder.Services.AddSingleton(adminAuth);
            builder.Services.AddSingleton<ServerEvents>();
            builder.Services.AddSingleton<IEventBus>(services => services.GetRequiredService<ServerEvents>());
            builder.Services.AddSingleton<IServerStatusEvents>(services => services.GetRequiredService<ServerEvents>());
            builder.Services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();
            builder.Services.AddSingleton<ServerState>();
            builder.Services.AddSingleton<ClientEventRecorder>();
            builder.Services.AddSingleton<ServerAdminService>();
            builder.Services.AddSingleton<ServerIdentity>();
            builder.Services.AddSingleton<IServerInfo>(services => services.GetRequiredService<ServerIdentity>());
            builder.Services.AddSingleton<IAdminActor, AdminActor>();
            // Hosted services start in order and stop in reverse: the audit log first, so it stops last and stores
            // the shutdown events; the SRS server last (below, after the plugins), so plugins see it start and stop.
            builder.Services.AddHostedService(services => services.GetRequiredService<AuditLog>());

            // Keep login cookies valid across restarts.
            builder.Services.AddDataProtection()
                .SetApplicationName(ServiceName)
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(ServerPaths.DataDirectory, "keys")));

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.Cookie.Name = "il2srs.admin";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.ExpireTimeSpan = TimeSpan.FromHours(12);
                    options.SlidingExpiration = true;
                    // A cookie from before the password changed (or without a stamp) no longer signs in.
                    options.Events.OnValidatePrincipal = async context =>
                    {
                        if (!adminAuth.Disabled &&
                            context.Principal?.FindFirst(AdminAuthOptions.SessionStampClaim)?.Value != adminAuth.SessionStamp)
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                    ApiKeyAuthenticationHandler.SchemeName, null);

            builder.Services.AddAuthorization(options =>
            {
                if (adminAuth.Disabled)
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
                }

                options.AddPolicy(ApiKeyAuthenticationHandler.ReadPolicy, policy => policy
                    .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                    .RequireClaim(ApiKeyAuthenticationHandler.ScopeClaim, ApiScope.Read, ApiScope.Write));
                options.AddPolicy(ApiKeyAuthenticationHandler.WritePolicy, policy => policy
                    .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                    .RequireClaim(ApiKeyAuthenticationHandler.ScopeClaim, ApiScope.Write));
            });
            builder.Services.AddCascadingAuthenticationState();
            var trustedProxies = builder.Configuration[TrustedProxiesKey];
            ConfigureForwardedHeaders(new ForwardedHeadersOptions(), trustedProxies); // Fails startup on a bad entry.
            builder.Services.Configure<ForwardedHeadersOptions>(options => ConfigureForwardedHeaders(options, trustedProxies));
            builder.Services.AddAdminLoginRateLimiting();
            builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiKeyOpenApiTransformer>());

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddScoped<ToastService>();

            ServerPlugins.Catalog(builder.Services);
            ServerPlugins.LoadFromFolders(builder);
            configure?.Invoke(builder);
            builder.Services.AddHostedService<SrsServerHostedService>();

            var app = builder.Build();

            // First, so the login rate limit, logged addresses and the cookie's Secure flag see the browser's
            // address and scheme rather than the reverse proxy's.
            app.UseForwardedHeaders();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error", createScopeForErrors: true);
            }

            // Friendly "not found" page for the admin UI only; API, login and health responses keep their own bodies.
            app.UseWhen(context => !IsMachineEndpoint(context.Request.Path),
                branch => branch.UseStatusCodePagesWithReExecute("/not-found"));
            // After the status code pages, so the re-executed request is matched to /not-found again; when left to
            // WebApplication, routing runs first and the re-executed request finds no page (an empty 404).
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseAntiforgery();
            ServerPlugins.UseStaticFiles(app);

            // Named explicitly so the manifest is found when another assembly (e.g. tests) is the entry point.
            var staticAssetsManifest = Path.Combine(AppContext.BaseDirectory,
                typeof(Program).Assembly.GetName().Name + ".staticwebassets.endpoints.json");
            if (File.Exists(staticAssetsManifest))
            {
                app.MapStaticAssets(staticAssetsManifest);
            }

            app.MapOpenApi().AllowAnonymous();
            // Interactive API reference; admins paste an API key to try requests.
            app.MapScalarApiReference("/scalar", options => options
                    .WithTitle("IL2-SRS Server API")
                    .AddPreferredSecuritySchemes(ApiKeyOpenApiTransformer.SchemeName))
                .RequireAuthorization();
            app.MapAdminEndpoints();
            app.MapServerApi();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddAdditionalAssemblies(app.Services.GetServices<AdminRazorAssembly>()
                    .Select(page => page.Assembly).Distinct().ToArray());

            return app;
        }

        private static void AddServerData(IServiceCollection services, IConfiguration configuration,
            StartupArguments startup)
        {
            var database = new SrsDatabase(ServerPaths.Resolve(SrsDatabase.FileName));
            var settings = new ServerSettingsStore(database, ServerSettingsStore.ReadOverrides(configuration));
            var bans = new BanStore(database);

            if (database.WasCreated)
            {
                LegacyFileImporter.ImportServerConfig(
                    ServerPaths.Resolve(string.IsNullOrWhiteSpace(startup.ImportConfigFile)
                        ? "server.cfg"
                        : startup.ImportConfigFile),
                    settings);
                LegacyFileImporter.ImportBannedIps(ServerPaths.Resolve("banned.txt"), bans);
            }
            else if (!string.IsNullOrWhiteSpace(startup.ImportConfigFile))
            {
                LogManager.GetCurrentClassLogger().Warn(
                    $"-cfg={startup.ImportConfigFile} ignored: settings are only imported when {SrsDatabase.FileName} is first created.");
            }

            ServerSettingsStore.Instance = settings;

            services.AddSingleton(database);
            services.AddSingleton(settings);
            services.AddSingleton(bans);
            services.AddSingleton(new ApiKeyStore(database));
            services.AddSingleton(new AuditRetention
            {
                Days = ReadPositiveInt(configuration, AuditRetention.DaysKey, 30),
                MaxRows = ReadPositiveInt(configuration, AuditRetention.MaxRowsKey, 100_000)
            });
            services.AddSingleton<AuditLog>();
        }

        /// <summary>
        /// Trusts X-Forwarded-For and X-Forwarded-Proto from the reverse proxies in <paramref name="trustedProxies"/>
        /// (addresses or CIDR ranges, separated by ',' or ';'), and from this machine.
        /// </summary>
        internal static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, string trustedProxies)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var entry in (trustedProxies ?? string.Empty).Split(new[] { ',', ';' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (entry.Contains('/') && System.Net.IPNetwork.TryParse(entry, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                }
                else if (IPAddress.TryParse(entry, out var address))
                {
                    options.KnownProxies.Add(address);
                }
                else
                {
                    throw new ServerStartupException(
                        $"{TrustedProxiesKey} entry \"{entry}\" is not an IP address or CIDR range (e.g. 172.18.0.0/16).");
                }
            }
        }

        private static bool IsMachineEndpoint(PathString path)
        {
            return path.StartsWithSegments("/api") || path.StartsWithSegments("/account") ||
                   path.StartsWithSegments("/healthz") || path.StartsWithSegments("/openapi");
        }

        private static ServerStartupException FindStartupException(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is ServerStartupException startupException)
                {
                    return startupException;
                }

                if (current is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
                {
                    return FindStartupException(aggregate.InnerExceptions[0]);
                }
            }

            return null;
        }

        private static int ReadPositiveInt(IConfiguration configuration, string key, int defaultValue)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 1)
            {
                throw new ServerStartupException($"{key} must be a positive whole number.");
            }

            return number;
        }
    }
}
