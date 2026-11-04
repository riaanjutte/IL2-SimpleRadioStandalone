using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Admin;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Audit;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Api
{
    public sealed record StatusResponse(string Version, string Status, bool Running, int Port, string Uptime,
        DateTime? StartedAtUtc, int Clients, string VoiceLinks, int RecentTransmitters, string Memory, int ActiveBans);

    public sealed record ClientResponse(string Guid, string Name, string Coalition, string Callsign, string Vehicle,
        string Airfield, int Radio1Channel, int Radio2Channel, bool VoiceLink, bool Muted, string TransmittingFrequency,
        DateTime? LastTransmissionUtc);

    public sealed record SettingResponse(string Key, string Group, string Label, string Description, string Kind,
        string Value, string Source, bool Locked, bool RequiresRestart);

    public sealed record SettingUpdateRequest(string Value);

    public sealed record ChannelNamesRequest(Dictionary<int, string> ChannelNames);

    public sealed record BanResponse(long Id, string IpAddress, string PlayerName, string Reason, string CreatedBy,
        DateTime CreatedAtUtc, DateTime? ExpiresAtUtc, bool Active);

    public sealed record BanClientRequest(string Reason, int? DurationMinutes);

    public sealed record BanAddressRequest(string IpAddress, string PlayerName, string Reason, int? DurationMinutes);

    public sealed record EventResponse(long Id, DateTime OccurredAtUtc, string Category, string Type, string Actor,
        string Message, string ClientGuid, JsonElement? Payload);

    public sealed record EventFieldResponse(string Path, string Type, string Description);

    public sealed record EventTypeResponse(string Type, string Category, string Label, string Description,
        IReadOnlyList<EventFieldResponse> Fields);

    public sealed record PluginResponse(string Id, string Name, string Version);

    public sealed record PluginFailureResponse(string Source, string Error);

    public sealed record PluginsResponse(IReadOnlyList<PluginResponse> Loaded, IReadOnlyList<PluginFailureResponse> Failed);

    /// <summary>
    /// REST API under /api/v1. Requires an API key; GET needs the read scope, everything else the write scope.
    /// </summary>
    public static class ApiEndpoints
    {
        public static IEndpointRouteBuilder MapServerApi(this IEndpointRouteBuilder endpoints)
        {
            var api = endpoints.MapGroup("/api/v1")
                .RequireAuthorization(ApiKeyAuthenticationHandler.ReadPolicy)
                .DisableAntiforgery()
                .WithTags("IL2-SRS Server");

            api.MapGet("/status", (ServerAdminService admin) => ToStatus(admin))
                .WithSummary("Server status and health");

            // Clients
            api.MapGet("/clients", (ServerAdminService admin) =>
                    admin.GetClients().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(ToClient))
                .WithSummary("Connected clients");
            Write(api.MapPost("/clients/{guid}/kick", (string guid, ServerAdminService admin, ClaimsPrincipal user) =>
                    admin.KickClient(guid, Actor(user)) ? Results.NoContent() : ClientNotFound(guid)))
                .WithSummary("Disconnect a client");
            Write(api.MapPost("/clients/{guid}/ban",
                    (string guid, BanClientRequest request, ServerAdminService admin, ClaimsPrincipal user) =>
                    {
                        var ban = admin.BanClient(guid, request?.Reason, Duration(request?.DurationMinutes), Actor(user));
                        return ban != null ? Results.Ok(ToBan(ban)) : ClientNotFound(guid);
                    }))
                .WithSummary("Ban a client's IP address and disconnect them; omit durationMinutes for a permanent ban");
            Write(api.MapPost("/clients/{guid}/mute", (string guid, ServerAdminService admin, ClaimsPrincipal user) =>
                    admin.SetClientMuted(guid, true, Actor(user)) ? Results.NoContent() : ClientNotFound(guid)))
                .WithSummary("Mute a client on the server");
            Write(api.MapPost("/clients/{guid}/unmute", (string guid, ServerAdminService admin, ClaimsPrincipal user) =>
                    admin.SetClientMuted(guid, false, Actor(user)) ? Results.NoContent() : ClientNotFound(guid)))
                .WithSummary("Unmute a client on the server");

            // Settings
            api.MapGet("/settings", (ServerAdminService admin) =>
                    admin.GetSettings().Where(s => !s.Key.Hidden).Select(s => ToSetting(s.Key, s.Value)))
                .WithSummary("All server settings with their source (default, database or override)");
            Write(api.MapPut("/settings/{key}",
                    (string key, SettingUpdateRequest request, ServerAdminService admin, ClaimsPrincipal user) =>
                    {
                        if (!SettingDefinition.TryFind(key, out var definition) || definition.Hidden)
                        {
                            return Results.NotFound(new ErrorResponse($"Unknown setting {key}."));
                        }

                        var error = admin.SetSetting(definition.Key, request?.Value, Actor(user));
                        return error == null
                            ? Results.Ok(ToSetting(definition, admin.GetSetting(definition.Key)))
                            : Results.BadRequest(new ErrorResponse(error));
                    }))
                .WithSummary("Change a setting");

            api.MapGet("/channel-names", (ServerAdminService admin) => admin.GetChannelNames())
                .WithSummary("Channel names by channel number");
            Write(api.MapPut("/channel-names",
                    (ChannelNamesRequest request, ServerAdminService admin, ClaimsPrincipal user) =>
                    {
                        admin.SetChannelNames(request?.ChannelNames ?? new Dictionary<int, string>(), Actor(user));
                        return Results.Ok(admin.GetChannelNames());
                    }))
                .WithSummary("Replace all channel names; channels left out have no name");
            api.MapGet("/channel-names/{coalition}", (string coalition, ServerAdminService admin) =>
                    TryParseCoalition(coalition, out var value)
                        ? Results.Ok(admin.GetChannelNames(value))
                        : CoalitionNotFound(coalition))
                .WithSummary("Allied or Axis channel names (coalition: allies or axis); these replace the shared names for that coalition");
            Write(api.MapPut("/channel-names/{coalition}",
                    (string coalition, ChannelNamesRequest request, ServerAdminService admin, ClaimsPrincipal user) =>
                    {
                        if (!TryParseCoalition(coalition, out var value))
                        {
                            return CoalitionNotFound(coalition);
                        }

                        admin.SetChannelNames(value, request?.ChannelNames ?? new Dictionary<int, string>(), Actor(user));
                        return Results.Ok(admin.GetChannelNames(value));
                    }))
                .WithSummary("Replace one coalition's channel names; the shared names and the other coalition are unchanged");

            // Bans
            api.MapGet("/bans", (ServerAdminService admin) => admin.GetBans().Select(ToBan))
                .WithSummary("All bans, including expired ones");
            Write(api.MapPost("/bans", (BanAddressRequest request, ServerAdminService admin, ClaimsPrincipal user) =>
                    {
                        var error = admin.BanAddress(request?.IpAddress, request?.PlayerName, request?.Reason,
                            Duration(request?.DurationMinutes), Actor(user), out var ban);
                        return error == null
                            ? Results.Created($"/api/v1/bans/{ban.Id}", ToBan(ban))
                            : Results.BadRequest(new ErrorResponse(error));
                    }))
                .WithSummary("Ban an IP address; omit durationMinutes for a permanent ban");
            Write(api.MapDelete("/bans/{id:long}", (long id, ServerAdminService admin, ClaimsPrincipal user) =>
                    admin.Unban(id, Actor(user))
                        ? Results.NoContent()
                        : Results.NotFound(new ErrorResponse($"No ban with id {id}."))))
                .WithSummary("Remove a ban");

            // Events
            api.MapGet("/events", (string category, string type, string client, string search, DateTime? since, int? limit,
                        AuditLog audit) =>
                    audit.Query(category, search, since?.ToUniversalTime(), limit ?? 200, type, client).Select(ToEvent))
                .WithSummary("Event log, newest first (category: server, client, radio, admin, auth; type: see " +
                             "/event-types; client: client guid; limit up to 1000)");
            api.MapGet("/event-types", (IEventTypeRegistry types) => types.All.Select(ToEventType))
                .WithSummary("Event types with their payload fields");
            Write(api.MapDelete("/events", (AuditLog audit, ClaimsPrincipal user) =>
                    Results.Ok(new { deleted = audit.Clear(Actor(user)) })))
                .WithSummary("Clear the event log");

            // Plugins
            api.MapGet("/plugins", (PluginCatalog plugins) => new PluginsResponse(
                    plugins.Loaded.Select(p => new PluginResponse(p.Id, p.Name, p.Version)).ToList(),
                    plugins.Failed.Select(f => new PluginFailureResponse(f.Source, f.Error)).ToList()))
                .WithSummary("Plugins this server runs, and plugins it could not load");
            ServerPlugins.MapApis(api);

            // Server lifecycle
            Write(api.MapPost("/server/start", (ServerAdminService admin, ClaimsPrincipal user) =>
                    Lifecycle(admin, admin.Start(Actor(user)))))
                .WithSummary("Start the SRS server");
            Write(api.MapPost("/server/stop", (ServerAdminService admin, ClaimsPrincipal user) =>
                    {
                        admin.Stop(Actor(user));
                        return Results.Ok(ToStatus(admin));
                    }))
                .WithSummary("Stop the SRS server; connected clients are disconnected");
            Write(api.MapPost("/server/restart", (ServerAdminService admin, ClaimsPrincipal user) =>
                    Lifecycle(admin, admin.Restart(Actor(user)))))
                .WithSummary("Restart the SRS server so port and startup-only settings take effect");

            return endpoints;
        }

        private static RouteHandlerBuilder Write(RouteHandlerBuilder builder)
        {
            return builder.RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy);
        }

        private static IResult Lifecycle(ServerAdminService admin, string error)
        {
            return error == null
                ? Results.Ok(ToStatus(admin))
                : Results.Json(new ErrorResponse(error), statusCode: StatusCodes.Status409Conflict);
        }

        private static IResult ClientNotFound(string guid)
        {
            return Results.NotFound(new ErrorResponse($"No connected client {guid}."));
        }

        private static IResult CoalitionNotFound(string coalition)
        {
            return Results.NotFound(new ErrorResponse($"Unknown coalition {coalition}; use allies or axis."));
        }

        internal static bool TryParseCoalition(string value, out int coalition)
        {
            foreach (var candidate in ChannelNameSettings.Coalitions)
            {
                if (string.Equals(value, ChannelNameSettings.GetCoalitionLabel(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    coalition = candidate;
                    return true;
                }
            }

            coalition = ChannelNameSettings.SharedCoalition;
            return false;
        }

        private static string Actor(ClaimsPrincipal user) => user.Identity?.Name ?? "api";

        private static TimeSpan? Duration(int? minutes)
        {
            return minutes.HasValue && minutes.Value > 0 ? TimeSpan.FromMinutes(minutes.Value) : null;
        }

        private static StatusResponse ToStatus(ServerAdminService admin)
        {
            var health = admin.GetHealth();
            return new StatusResponse(admin.Version, health.Status, admin.IsRunning, admin.Port, health.Uptime,
                admin.StartedAtUtc, health.Clients, health.VoiceLinks, health.RecentTransmitters, health.Memory,
                admin.ActiveBanCount);
        }

        private static ClientResponse ToClient(SRClient client)
        {
            return new ClientResponse(client.ClientGuid, client.Name,
                ClientAdminPresentation.GetCoalitionName(client.Coalition), Blank(client.AssignedCallsign),
                Blank(client.AssignedVehicle), Blank(client.AssignedAirfield),
                ClientAdminPresentation.GetRadioChannel(client.GameState, 1),
                ClientAdminPresentation.GetRadioChannel(client.GameState, 2), client.VoipPort != null, client.Muted,
                client.TransmittingFrequency,
                client.LastTransmissionReceived == default ? null : client.LastTransmissionReceived.ToUniversalTime());
        }

        internal static SettingResponse ToSetting(SettingDefinition definition, SettingValue value)
        {
            return new SettingResponse(definition.Name, definition.Group, definition.Label, definition.Description,
                definition.Kind.ToString(), definition.Secret ? null : value.StringValue, value.Source.ToString(),
                value.IsLocked, definition.RequiresRestart);
        }

        private static BanResponse ToBan(BanRecord ban)
        {
            return new BanResponse(ban.Id, ban.IpAddress, ban.PlayerName, ban.Reason, ban.CreatedBy, ban.CreatedAtUtc,
                ban.ExpiresAtUtc, ban.IsActive(DateTime.UtcNow));
        }

        private static EventResponse ToEvent(AuditEvent auditEvent)
        {
            return new EventResponse(auditEvent.Id, auditEvent.OccurredAtUtc, auditEvent.Category, auditEvent.Type,
                auditEvent.Actor, auditEvent.Message, auditEvent.ClientGuid,
                auditEvent.Payload == null ? null : JsonDocument.Parse(auditEvent.Payload).RootElement.Clone());
        }

        private static EventTypeResponse ToEventType(SrsEventType eventType)
        {
            return new EventTypeResponse(eventType.Type, eventType.Category, eventType.Label, eventType.Description,
                eventType.Fields.Select(f => new EventFieldResponse(f.Path, f.Type, f.Description)).ToList());
        }

        private static string Blank(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value == "---" ? null : value;
        }
    }
}
