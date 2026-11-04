using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Api;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    public sealed record ConditionDto(string Field, string Operator, string Value);

    public sealed record IntegrationResponse(long Id, string Name, string Description, bool Enabled,
        IReadOnlyList<string> Triggers,
        IReadOnlyList<ConditionDto> Conditions, string Action, JsonObject Config,
        IReadOnlyDictionary<string, string> StoredSecrets, string RetryMode, int MaxRetries, DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    /// <summary>Secret settings left empty keep their stored value when the action is unchanged.</summary>
    public sealed record IntegrationRequest(string Name, string Description, bool? Enabled,
        IReadOnlyList<string> Triggers,
        IReadOnlyList<ConditionDto> Conditions, string Action, JsonObject Config, string RetryMode, int? MaxRetries);

    public sealed record IntegrationRunResponse(long Id, long IntegrationId, string IntegrationName, string Action,
        string EventType, string EventMessage, string Status, int Attempts, DateTime CreatedAtUtc,
        DateTime? NextAttemptAtUtc, DateTime? FinishedAtUtc, string Error, string Result, JsonElement Data);

    public sealed record ActionTypeResponse(string Type, string Label, string Description,
        IReadOnlyCollection<string> SecretFields);

    public sealed record IntegrationTestRunResponse(string Event, bool Succeeded, string Status, string Message,
        bool ConditionsMatchSample);

    public sealed record IntegrationTestResponse(bool Succeeded, IReadOnlyList<IntegrationTestRunResponse> Runs);

    /// <summary>Integrations under /api/v1/integrations; changes need the write scope.</summary>
    internal static class IntegrationEndpoints
    {
        public static void Map(IEndpointRouteBuilder api)
        {
            api.MapGet("/integrations", (IntegrationService service) => service.Store.List().Select(ToResponse))
                .WithSummary("Integrations (secret settings are never returned)");
            api.MapGet("/integrations/{id:long}", (long id, IntegrationService service) =>
                    service.Store.Get(id) is { } integration
                        ? Results.Ok(ToResponse(integration))
                        : NotFound(id))
                .WithSummary("One integration");
            api.MapGet("/integrations/action-types", (IntegrationService service) =>
                    service.Actions.Select(a => new ActionTypeResponse(a.Type, a.Label, a.Description, a.SecretFields)))
                .WithSummary("Actions an integration can run on this server");

            Write(api.MapPost("/integrations", (IntegrationRequest request, IntegrationService service, ClaimsPrincipal user) =>
                    Save(0, request, null, service, user)))
                .WithSummary("Create an integration");
            Write(api.MapPut("/integrations/{id:long}",
                    (long id, IntegrationRequest request, IntegrationService service, ClaimsPrincipal user) =>
                        service.Store.Get(id) is { } existing
                            ? Save(id, request, existing, service, user)
                            : NotFound(id)))
                .WithSummary("Replace an integration");
            Write(api.MapDelete("/integrations/{id:long}", (long id, IntegrationService service, ClaimsPrincipal user) =>
                    service.Delete(id, Actor(user)) ? Results.NoContent() : NotFound(id)))
                .WithSummary("Delete an integration; its run history is kept");
            Write(api.MapPost("/integrations/test",
                    async (IntegrationRequest request, IntegrationService service, CancellationToken cancellationToken) =>
                    {
                        var test = await service.TestAsync(ToDefinition(0, request, null), cancellationToken);
                        return Results.Ok(new IntegrationTestResponse(test.Succeeded, test.Runs
                            .Select(r => new IntegrationTestRunResponse(r.Event?.Type, r.Result.IsSuccess,
                                r.Result.Status, r.Result.Message, r.ConditionsMatchSample))
                            .ToList()));
                    }))
                .WithSummary("Run an integration's action once per trigger event with example data, without saving it");

            api.MapGet("/integrations/runs", (string status, long? integration, int? limit, IntegrationService service) =>
                    service.Store.QueryExecutions(status, integration, limit ?? 200).Select(ToRun))
                .WithSummary("Integration runs, newest first (status: queued, running, succeeded, failed, skipped)");
            Write(api.MapPost("/integrations/runs/{id:long}/retry", (long id, IntegrationService service) =>
                    service.Store.Requeue(id)
                        ? Results.Ok(ToRun(service.Store.GetExecution(id)))
                        : Results.Json(new ErrorResponse($"Run {id} does not exist or is still queued or running."),
                            statusCode: StatusCodes.Status409Conflict)))
                .WithSummary("Run a finished run again now");
        }

        private static IResult Save(long id, IntegrationRequest request, IntegrationDefinition existing,
            IntegrationService service, ClaimsPrincipal user)
        {
            var definition = ToDefinition(id, request, existing);
            var validation = service.Validate(definition);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new ValidationErrorResponse(validation.FirstError, validation.Errors));
            }

            var error = service.Save(definition, Actor(user), out var saved);
            return error == null
                ? id == 0 ? Results.Created($"/api/v1/integrations/{saved.Id}", ToResponse(saved)) : Results.Ok(ToResponse(saved))
                : Results.BadRequest(new ErrorResponse(error));
        }

        private static IntegrationDefinition ToDefinition(long id, IntegrationRequest request,
            IntegrationDefinition existing)
        {
            return new IntegrationDefinition
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                Enabled = request.Enabled ?? existing?.Enabled ?? true,
                TriggerTypes = request.Triggers ?? Array.Empty<string>(),
                Conditions = (request.Conditions ?? Array.Empty<ConditionDto>())
                    .Select(c => new IntegrationCondition(c.Field, c.Operator ?? ConditionOperator.Is, c.Value))
                    .ToList(),
                ActionType = request.Action,
                ActionConfig = request.Config ?? new JsonObject(),
                RetryMode = Enum.TryParse<RetryMode>(request.RetryMode, true, out var mode) ? mode : RetryMode.None,
                MaxRetries = request.MaxRetries ?? 0
            };
        }

        private static IntegrationResponse ToResponse(IntegrationDefinition integration)
        {
            var config = (JsonObject)integration.ActionConfig.DeepClone();
            foreach (var secret in integration.SecretHints.Keys)
            {
                config.Remove(secret);
            }

            return new IntegrationResponse(integration.Id, integration.Name, integration.Description,
                integration.Enabled, integration.TriggerTypes,
                integration.Conditions.Select(c => new ConditionDto(c.Field, c.Operator, c.Value)).ToList(),
                integration.ActionType, config, integration.SecretHints,
                integration.RetryMode.ToString().ToLowerInvariant(), integration.MaxRetries, integration.CreatedAtUtc,
                integration.UpdatedAtUtc);
        }

        private static IntegrationRunResponse ToRun(IntegrationExecution run)
        {
            using var data = JsonDocument.Parse(run.Data);
            return new IntegrationRunResponse(run.Id, run.IntegrationId, run.IntegrationName, run.ActionType,
                run.EventType, run.EventMessage, run.Status, run.Attempts, run.CreatedAtUtc, run.NextAttemptAtUtc,
                run.FinishedAtUtc, run.LastError, run.Result, data.RootElement.Clone());
        }

        private static IResult NotFound(long id) => Results.NotFound(new ErrorResponse($"No integration with id {id}."));

        private static RouteHandlerBuilder Write(RouteHandlerBuilder builder) =>
            builder.RequireAuthorization(ServerApi.WritePolicy);

        private static string Actor(ClaimsPrincipal user) => user.Identity?.Name ?? "api";
    }
}
