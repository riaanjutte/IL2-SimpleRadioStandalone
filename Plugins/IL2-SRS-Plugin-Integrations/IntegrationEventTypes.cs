using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>The events the integrations plugin publishes (the names predate the plugin, so they stay admin.*).</summary>
    public static class IntegrationEventTypes
    {
        private static readonly SrsEventField[] IntegrationFields =
        {
            new("integration.id", "number", "Integration id", 3),
            new("integration.name", "string", "Integration name", "Post joins to Discord"),
            new("integration.triggers", "string", "Trigger event types, comma-separated",
                "server.started,server.stopped"),
            new("integration.action", "string", "Action type", "discord-webhook")
        };

        public static readonly SrsEventType IntegrationCreated = new("admin.integration_created", EventCategory.Admin,
            "Integration created", "An admin created an integration.", IntegrationFields);

        public static readonly SrsEventType IntegrationUpdated = new("admin.integration_updated", EventCategory.Admin,
            "Integration changed", "An admin changed, enabled or disabled an integration.", IntegrationFields,
            new SrsEventField[] { new("integration.enabled", "boolean", "Whether the integration is enabled", true) });

        public static readonly SrsEventType IntegrationDeleted = new("admin.integration_deleted", EventCategory.Admin,
            "Integration deleted", "An admin deleted an integration.", IntegrationFields);

        public static readonly SrsEventType[] All = { IntegrationCreated, IntegrationUpdated, IntegrationDeleted };
    }
}
