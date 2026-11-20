using System;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// Integrations: run an action (post to Discord, send an HTTP request) when server events happen. Other
    /// plugins compiled against this one can add actions with <see cref="IntegrationServiceExtensions.AddIntegrationAction{T}"/>.
    /// </summary>
    public sealed class IntegrationsPlugin : IServerPlugin
    {
        public const string PluginId = "integrations";

        public string Id => PluginId;
        public string Name => "Integrations";

        public void ConfigureServices(PluginContext context, IServiceCollection services)
        {
            services.AddSingleton(_ => IntegrationDatabase.Open(context.DataDirectory, context.ServerDataDirectory));
            services.AddSingleton<SecretProtector>();
            services.AddSingleton<IntegrationStore>();
            services.AddSingleton<IntegrationService>();
            services.AddSingleton<IntegrationDispatcher>();
            services.AddHostedService(provider => provider.GetRequiredService<IntegrationDispatcher>());
            services.AddSingleton<WebhookRateLimiter>();
            services.AddHttpClient(WebhookHttp.ClientName, (provider, client) =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "IL2-SRS-Server/" + provider.GetRequiredService<IServerInfo>().Version);
            });
            services.AddIntegrationAction<DiscordWebhookAction>();
            services.AddIntegrationAction<HttpWebhookAction>();

            services.AddEventTypes(IntegrationEventTypes.All);
            services.AddAdminNavItem("integrations", "Integrations");
            services.AddAdminStylesheet(AdminExtensions.PluginFile(PluginId, "integrations.css"));
        }

        public void MapApi(IEndpointRouteBuilder api)
        {
            IntegrationEndpoints.Map(api);
        }
    }
}
