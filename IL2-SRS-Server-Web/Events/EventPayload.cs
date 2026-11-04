using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Admin;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Events
{
    /// <summary>Builds the payload sections shared by several event types.</summary>
    public static class EventPayload
    {
        public static JsonObject Client(SRClient client, string ipAddress = null)
        {
            return new JsonObject
            {
                ["client"] = new JsonObject
                {
                    ["guid"] = client.ClientGuid,
                    ["name"] = client.Name,
                    ["coalition"] = client.Coalition,
                    ["coalitionName"] = ClientAdminPresentation.GetCoalitionName(client.Coalition),
                    ["callsign"] = Blank(client.AssignedCallsign),
                    ["vehicle"] = Blank(client.AssignedVehicle),
                    ["airfield"] = Blank(client.AssignedAirfield),
                    ["ip"] = ipAddress
                }
            };
        }

        /// <summary>The <c>server</c> section of server events; <paramref name="startedAtUtc"/> is null when stopped.</summary>
        public static JsonObject Server(int port, int clientCount, DateTime? startedAtUtc)
        {
            var uptime = startedAtUtc == null
                ? 0
                : (long)Math.Max(0, (DateTime.UtcNow - startedAtUtc.Value).TotalSeconds);
            return new JsonObject
            {
                ["server"] = new JsonObject { ["port"] = port, ["clientCount"] = clientCount, ["uptime"] = uptime }
            };
        }

        public static JsonObject Ban(BanRecord ban)
        {
            return new JsonObject
            {
                ["ban"] = new JsonObject
                {
                    ["id"] = ban.Id,
                    ["ip"] = ban.IpAddress,
                    ["playerName"] = ban.PlayerName,
                    ["reason"] = ban.Reason,
                    ["expiresAt"] = ban.ExpiresAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                    ["permanent"] = ban.ExpiresAtUtc == null
                }
            };
        }

        public static JsonObject Remote(string ipAddress)
        {
            return new JsonObject { ["ip"] = ipAddress };
        }

        /// <summary>Adds the properties of <paramref name="extra"/> to <paramref name="payload"/>.</summary>
        public static JsonObject With(this JsonObject payload, JsonObject extra)
        {
            foreach (var property in new List<KeyValuePair<string, JsonNode>>(extra))
            {
                extra.Remove(property.Key);
                payload[property.Key] = property.Value;
            }

            return payload;
        }

        /// <summary>Adds one property to <paramref name="payload"/>.</summary>
        public static JsonObject With(this JsonObject payload, string name, JsonNode value)
        {
            payload[name] = value;
            return payload;
        }

        private static string Blank(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value == "---" ? null : value;
        }
    }
}
