using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Sample
{
    /// <summary>Adds a page, a stylesheet, an event type and an endpoint, and says where its data goes.</summary>
    public sealed class SamplePlugin : IServerPlugin
    {
        public const string PluginId = "sample";

        public static readonly SrsEventType Pinged = new("sample.pinged", "admin", "Sample pinged",
            "The sample plugin's endpoint was called.");

        private PluginContext _context;

        public string Id => PluginId;
        public string Name => "Sample";

        public void ConfigureServices(PluginContext context, IServiceCollection services)
        {
            _context = context;
            services.AddEventTypes(Pinged);
            services.AddAdminNavItem("sample", "Sample");
            services.AddAdminStylesheet(AdminExtensions.PluginFile(PluginId, "sample.css"));
        }

        public void MapApi(IEndpointRouteBuilder api)
        {
            api.MapPost("/sample/ping", (IEventBus events) =>
            {
                events.Publish(SrsEvent.Create(Pinged, "plugin:sample", "Pinged"));
                return Results.Ok(new { dataDirectory = _context.DataDirectory });
            }).RequireAuthorization(ServerApi.WritePolicy);
        }
    }
}
