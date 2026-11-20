using System;
using System.Linq;
using System.Text.Json.Nodes;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations.Tests
{
    /// <summary>The server.* fields integration messages get from the server identity.</summary>
    public class ServerFieldsTests
    {
        [Fact]
        public void EventDataHasTheServerIdentityWithoutReplacingTheEventsOwnServerFields()
        {
            var identity = new JsonObject
            {
                ["name"] = "My SRS Server", ["hostname"] = "srs-1", ["version"] = "1.0.5.0", ["adminUrl"] = null
            };
            var started = SrsEvent.Create(SrsEventTypes.ServerStopped, "admin", "stopped",
                EventPayload.Server(6002, 3, DateTime.UtcNow.AddMinutes(-90)));
            var data = EventData.From(started, identity);

            Assert.Equal("My SRS Server", EventData.AsText(EventData.Resolve(data, "server.name")));
            Assert.Equal("srs-1", EventData.AsText(EventData.Resolve(data, "server.hostname")));
            Assert.Equal("6002", EventData.AsText(EventData.Resolve(data, "server.port")));
            Assert.InRange(EventData.Resolve(data, "server.uptime")!.Value.GetInt64(), 5390, 5410);

            var connected = EventData.From(SrsEvent.Create(SrsEventTypes.ClientConnected, "system", "joined"), identity);
            Assert.Equal("My SRS Server", EventData.AsText(EventData.Resolve(connected, "server.name")));
        }

        [Fact]
        public void EveryEventListsTheServerIdentityFields()
        {
            var paths = EventData.Fields(SrsEventTypes.ClientConnected).Select(f => f.Path).ToList();
            Assert.Contains("server.name", paths);
            Assert.Contains("server.adminUrl", paths);
            Assert.DoesNotContain("server.uptime", paths);
            Assert.Contains("server.uptime", EventData.Fields(SrsEventTypes.ServerStopped).Select(f => f.Path));
        }

        [Fact]
        public void SamplesUseTheRealServerIdentity()
        {
            var sample = EventData.Sample(SrsEventTypes.ServerStarted, new JsonObject { ["name"] = "My SRS Server" });

            Assert.Equal("My SRS Server", EventData.AsText(EventData.Resolve(sample, "server.name")));
            Assert.Equal("srs-1", EventData.AsText(EventData.Resolve(EventData.Sample(SrsEventTypes.ServerStarted),
                "server.hostname")));
            Assert.Equal("6002", EventData.AsText(EventData.Resolve(sample, "server.port")));
        }

        [Fact]
        public void TemplatesCanShowUptimeAsATime()
        {
            var data = EventData.Sample(SrsEventTypes.ServerStopped);

            Assert.Equal("03:12:40", MessageTemplate.Render("{{ timespan.from_seconds server.uptime }}", data,
                TemplateEscape.None));
        }
    }
}
