using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// The data an integration sees for an event: <c>event.*</c> (type, actor, message, time) plus the
    /// event's payload, e.g. <c>{ "event": { "type": "client.connected", … }, "client": { "name": … } }</c>.
    /// Conditions and templates address it by dotted paths.
    /// </summary>
    /// <summary>A field available to an integration; <see cref="OnlyIn"/> is empty when every trigger event has it.</summary>
    public sealed record EventFieldUse(SrsEventField Field, IReadOnlyList<SrsEventType> OnlyIn);

    public static class EventData
    {
        /// <summary>Fields every event has in addition to its payload, for conditions and templates.</summary>
        public static readonly IReadOnlyList<SrsEventField> CommonFields = new SrsEventField[]
        {
            new("event.type", "string", "Event type, e.g. client.connected"),
            new("event.label", "string", "Event name, e.g. Client connected"),
            new("event.category", "string", "server, client, radio, admin or auth"),
            new("event.actor", "string", "Who caused it: system, admin, api:<key name> or anonymous", "system"),
            new("event.message", "string", "The event log message"),
            new("event.occurredAt", "string", "When it happened (UTC, ISO 8601)", "2026-10-12T18:00:00.0000000Z"),
            new("server.name", "string", "Server Name setting, or the host name when it isn't set", "My SRS Server"),
            new("server.hostname", "string",
                "Host name; in Docker the container ID unless the stack sets hostname", "srs-1"),
            new("server.version", "string", "Server version, e.g. the image version", "1.0.5.0-preview"),
            new("server.adminUrl", "string", "Public Admin URL setting, when set", "https://srs.example.com")
        };

        /// <summary>The server's identity fields of the <c>server</c> section, or null without one.</summary>
        public static JsonObject ServerSection(IServerInfo server)
        {
            return server == null
                ? null
                : new JsonObject
                {
                    ["name"] = server.Name,
                    ["hostname"] = server.Hostname,
                    ["version"] = server.Version,
                    ["adminUrl"] = server.AdminUrl
                };
        }

        /// <param name="server">The server's identity (<see cref="ServerSection"/>), added to <c>server.*</c>.</param>
        public static JsonElement From(SrsEvent srsEvent, JsonObject server = null)
        {
            var data = JsonNode.Parse(srsEvent.Payload.GetRawText()) as JsonObject ?? new JsonObject();
            AddServer(data, server);
            data["event"] = EventSection(srsEvent.EventType, srsEvent.Actor, srsEvent.Message, srsEvent.OccurredAtUtc);
            return JsonSerializer.SerializeToElement(data);
        }

        /// <summary>
        /// Example data for an event type, built from its fields' example values; <paramref name="server"/>
        /// replaces the example server identity with the real one.
        /// </summary>
        public static JsonElement Sample(SrsEventType type, JsonObject server = null)
        {
            var data = new JsonObject();
            foreach (var field in Fields(type))
            {
                Set(data, field.Path, JsonSerializer.SerializeToNode(field.Example ?? DefaultExample(field)));
            }

            if (server != null)
            {
                foreach (var entry in server)
                {
                    ((JsonObject)data["server"])[entry.Key] = entry.Value?.DeepClone();
                }
            }

            data["event"] = EventSection(type, "system", $"Example {type.Label.ToLowerInvariant()} event",
                new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc));
            return JsonSerializer.SerializeToElement(data);
        }

        /// <summary>Adds the server identity to the payload's <c>server</c> section, keeping what the event set.</summary>
        private static void AddServer(JsonObject data, JsonObject server)
        {
            if (server == null)
            {
                return;
            }

            if (data["server"] is not JsonObject section)
            {
                section = new JsonObject();
                data["server"] = section;
            }

            foreach (var entry in server)
            {
                if (!section.ContainsKey(entry.Key))
                {
                    section[entry.Key] = entry.Value?.DeepClone();
                }
            }
        }

        /// <summary>Every field path an integration on this event can use: <c>event.*</c> and the payload fields.</summary>
        public static IEnumerable<SrsEventField> Fields(SrsEventType type)
        {
            return CommonFields.Concat(type?.Fields ?? Array.Empty<SrsEventField>());
        }

        /// <summary>
        /// The fields an integration on several events can use: <c>event.*</c> and each event's payload fields,
        /// each once. <see cref="EventFieldUse.OnlyIn"/> lists the events a field is limited to.
        /// </summary>
        public static IReadOnlyList<EventFieldUse> Fields(IReadOnlyList<SrsEventType> types)
        {
            var fields = CommonFields
                .Select(f => (Field: f, In: types.ToList()))
                .ToList();
            foreach (var type in types)
            {
                foreach (var field in type.Fields)
                {
                    var index = fields.FindIndex(f => f.Field.Path == field.Path);
                    if (index < 0)
                    {
                        fields.Add((field, new List<SrsEventType> { type }));
                    }
                    else if (!fields[index].In.Contains(type))
                    {
                        fields[index].In.Add(type);
                    }
                }
            }

            return fields
                .Select(f => new EventFieldUse(f.Field,
                    f.In.Count == types.Count ? Array.Empty<SrsEventType>() : f.In))
                .ToList();
        }

        /// <summary>The value at a dotted path, or null when it is missing.</summary>
        public static JsonElement? Resolve(JsonElement data, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var current = data;
            foreach (var part in path.Trim().Split('.'))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                {
                    return null;
                }
            }

            return current;
        }

        /// <summary>A value as text: strings as-is, numbers and booleans invariant, null/missing as null.</summary>
        public static string AsText(JsonElement? value)
        {
            if (value == null)
            {
                return null;
            }

            var element = value.Value;
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble().ToString(CultureInfo.InvariantCulture),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.GetRawText()
            };
        }

        /// <summary>True when every condition holds; rows without a field are ignored.</summary>
        public static bool Matches(JsonElement data, IEnumerable<IntegrationCondition> conditions)
        {
            return conditions.Where(c => !string.IsNullOrWhiteSpace(c.Field)).All(c => Matches(data, c));
        }

        public static bool Matches(JsonElement data, IntegrationCondition condition)
        {
            var actual = AsText(Resolve(data, condition.Field));
            var expected = (condition.Value ?? string.Empty).Trim();

            switch (condition.Operator)
            {
                case ConditionOperator.Exists:
                    return !string.IsNullOrEmpty(actual);
                case ConditionOperator.NotExists:
                    return string.IsNullOrEmpty(actual);
                case ConditionOperator.IsNot:
                    return !Equal(actual, expected);
                case ConditionOperator.Contains:
                    return actual != null && actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
                case ConditionOperator.NotContains:
                    return actual == null || !actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
                case ConditionOperator.GreaterThan:
                    return TryNumber(actual, out var greater) && TryNumber(expected, out var lowerBound) &&
                           greater > lowerBound;
                case ConditionOperator.LessThan:
                    return TryNumber(actual, out var less) && TryNumber(expected, out var upperBound) &&
                           less < upperBound;
                case ConditionOperator.In:
                    return expected.Split(',').Any(option => Equal(actual, option.Trim()));
                default:
                    return Equal(actual, expected);
            }
        }

        private static bool Equal(string actual, string expected)
        {
            if (string.IsNullOrEmpty(actual))
            {
                return string.IsNullOrEmpty(expected);
            }

            if (TryNumber(actual, out var left) && TryNumber(expected, out var right))
            {
                return Math.Abs(left - right) < 1e-9;
            }

            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryNumber(string text, out double number)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        private static JsonObject EventSection(SrsEventType type, string actor, string message, DateTime occurredAtUtc)
        {
            return new JsonObject
            {
                ["type"] = type.Type,
                ["label"] = type.Label,
                ["category"] = type.Category,
                ["actor"] = actor,
                ["message"] = message,
                ["occurredAt"] = occurredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private static object DefaultExample(SrsEventField field)
        {
            return field.Type switch
            {
                "number" => 1,
                "boolean" => true,
                _ => "example"
            };
        }

        private static void Set(JsonObject root, string path, JsonNode value)
        {
            var parts = path.Split('.');
            var current = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (current[parts[i]] is not JsonObject next)
                {
                    next = new JsonObject();
                    current[parts[i]] = next;
                }

                current = next;
            }

            current[parts[^1]] = value;
        }
    }
}
