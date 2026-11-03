using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Audit;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations.Tests
{
    /// <summary>An action that records what it was asked to do and answers with a scripted result.</summary>
    internal sealed class RecordingAction : IIntegrationAction
    {
        public const string ActionType = "recording";

        public ConcurrentQueue<IntegrationExecutionContext> Runs { get; } = new();
        public Queue<IntegrationResult> Results { get; } = new();

        public string Type => ActionType;
        public string Label => "Recording";
        public string Description => "Records runs for tests.";
        public Type ConfigEditor => typeof(Microsoft.AspNetCore.Components.ComponentBase);
        public IReadOnlyCollection<string> SecretFields { get; } = new[] { "token" };

        public IReadOnlyDictionary<string, string> Validate(JsonObject config) =>
            config["token"] is null
                ? new Dictionary<string, string> { ["token"] = "Enter a token." }
                : new Dictionary<string, string>();

        public Task<IntegrationResult> ExecuteAsync(IntegrationExecutionContext context, CancellationToken cancellationToken)
        {
            Runs.Enqueue(context);
            lock (Results)
            {
                return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : IntegrationResult.Success("recorded"));
            }
        }
    }

    public class EventDataTests
    {
        private static readonly JsonElement Data = EventData.From(SrsEvent.Create(SrsEventTypes.ClientConnected,
            "system", "Hartmann (Red) connected", EventPayload.Client(
                new SRClient { ClientGuid = "g", Name = "Hartmann", Coalition = 1 }, "203.0.113.7")));

        [Theory]
        [InlineData("client.coalitionName", ConditionOperator.Is, "red", true)]
        [InlineData("client.coalitionName", ConditionOperator.IsNot, "Red", false)]
        [InlineData("client.name", ConditionOperator.Contains, "MANN", true)]
        [InlineData("client.name", ConditionOperator.NotContains, "mann", false)]
        [InlineData("client.coalition", ConditionOperator.GreaterThan, "0", true)]
        [InlineData("client.coalition", ConditionOperator.LessThan, "1", false)]
        [InlineData("client.coalition", ConditionOperator.Is, "1.0", true)]
        [InlineData("client.coalitionName", ConditionOperator.In, "Blue, Red", true)]
        [InlineData("client.callsign", ConditionOperator.Exists, null, false)]
        [InlineData("client.callsign", ConditionOperator.NotExists, null, true)]
        [InlineData("client.callsign", ConditionOperator.Is, "", true)]
        [InlineData("event.type", ConditionOperator.Is, "client.connected", true)]
        [InlineData("event.actor", ConditionOperator.Is, "system", true)]
        [InlineData("client.missing.deeper", ConditionOperator.Is, "x", false)]
        public void EvaluatesConditions(string field, string op, string value, bool expected)
        {
            Assert.Equal(expected, EventData.Matches(Data, new IntegrationCondition(field, op, value)));
        }

        [Fact]
        public void AllConditionsMustMatchAndEmptyRowsAreIgnored()
        {
            var conditions = new[]
            {
                new IntegrationCondition("client.coalitionName", ConditionOperator.Is, "Red"),
                new IntegrationCondition("", ConditionOperator.Is, "ignored"),
                new IntegrationCondition("client.name", ConditionOperator.Is, "Rall")
            };

            Assert.False(EventData.Matches(Data, conditions));
            Assert.True(EventData.Matches(Data, conditions.Take(2)));
        }

        [Fact]
        public void FieldsForSeveralEventsSayWhichEventsHaveThem()
        {
            var fields = EventData.Fields(new[] { SrsEventTypes.ServerStarted, SrsEventTypes.ClientConnected });

            Assert.Empty(fields.Single(f => f.Field.Path == "event.type").OnlyIn);
            Assert.Equal(new[] { SrsEventTypes.ServerStarted }, fields.Single(f => f.Field.Path == "server.port").OnlyIn);
            Assert.Equal(new[] { SrsEventTypes.ClientConnected }, fields.Single(f => f.Field.Path == "client.name").OnlyIn);
            Assert.Equal(fields.Count, fields.Select(f => f.Field.Path).Distinct().Count());
        }

        [Fact]
        public void SampleDataCoversEveryDocumentedField()
        {
            foreach (var type in SrsEventTypes.All)
            {
                var sample = EventData.Sample(type);
                Assert.All(EventData.Fields(type), field => Assert.NotNull(EventData.Resolve(sample, field.Path)));
                Assert.Equal(type.Type, EventData.AsText(EventData.Resolve(sample, "event.type")));
            }
        }
    }

    public class IntegrationStoreTests
    {
        [Fact]
        public void EncryptsSecretsAndOnlyDecryptsThemToRun()
        {
            using var temp = new TempDirectory();
            var (store, database) = CreateStore(temp);

            var saved = store.Save(Integration(new JsonObject { ["token"] = "super-secret-token-0Jdl", ["room"] = "ops" }));

            Assert.Null(saved.ActionConfig["token"]);
            Assert.Equal("ops", saved.ActionConfig["room"]!.GetValue<string>());
            Assert.Equal("ends in 0Jdl", saved.SecretHints["token"]);
            Assert.Equal("super-secret-token-0Jdl", store.GetForExecution(saved.Id).ActionConfig["token"]!.GetValue<string>());
            Assert.DoesNotContain("super-secret-token", ReadShared(database.DatabasePath + "-wal") +
                                                         ReadShared(database.DatabasePath));
        }

        [Fact]
        public void KeepsAStoredSecretWhenSavedEmptyAndDropsItWhenTheActionChanges()
        {
            using var temp = new TempDirectory();
            var (store, _) = CreateStore(temp);
            var saved = store.Save(Integration(new JsonObject { ["token"] = "first-secret-value" }));

            var renamed = store.Save(Integration(new JsonObject { ["token"] = null }, saved.Id, "Renamed"));
            Assert.Equal("Renamed", renamed.Name);
            Assert.Equal("first-secret-value", store.GetForExecution(saved.Id).ActionConfig["token"]!.GetValue<string>());

            var replaced = store.Save(Integration(new JsonObject { ["token"] = "second-secret-value" }, saved.Id));
            Assert.Equal("second-secret-value", store.GetForExecution(replaced.Id).ActionConfig["token"]!.GetValue<string>());

            var moved = store.Save(new IntegrationDefinition
            {
                Id = saved.Id, Name = "Other", TriggerTypes = new[] { "client.connected" }, ActionType = "other",
                ActionConfig = new JsonObject()
            });
            Assert.Empty(moved.SecretHints);
        }

        [Fact]
        public void ClearsAStoredSecretWhenAsked()
        {
            using var temp = new TempDirectory();
            var (store, _) = CreateStore(temp);
            var saved = store.Save(Integration(new JsonObject { ["token"] = "first-secret-value" }));

            var cleared = store.Save(Integration(new JsonObject
            {
                ["token"] = null,
                [ActionConfigExtensions.ClearSecretsField] = new JsonArray("token")
            }, saved.Id));

            Assert.Empty(cleared.SecretHints);
            var config = store.GetForExecution(saved.Id).ActionConfig;
            Assert.False(config.ContainsKey("token"));
            Assert.False(config.ContainsKey(ActionConfigExtensions.ClearSecretsField));
        }

        [Fact]
        public void RunAgainStartsFromTheFirstAttempt()
        {
            using var temp = new TempDirectory();
            var (store, _) = CreateStore(temp);
            var saved = store.Save(Integration(new JsonObject { ["token"] = "secret-value-1234" }));
            var id = store.Enqueue(saved, "client.connected", "connected", EventData.Sample(SrsEventTypes.ClientConnected));
            Assert.True(store.TryStart(id));
            store.Finish(id, ExecutionStatus.Failed, "Bad gateway", null, null);

            Assert.True(store.Requeue(id));

            var run = store.GetExecution(id);
            Assert.Equal(ExecutionStatus.Queued, run.Status);
            Assert.Equal(0, run.Attempts);
        }

        [Fact]
        public void FindsPendingRunsWithTheirIndex()
        {
            using var temp = new TempDirectory();
            var database = temp.CreateIntegrationDatabase();
            using var connection = database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"EXPLAIN QUERY PLAN SELECT MIN(pending.id) FROM integration_executions pending
INDEXED BY ix_integration_executions_pending
WHERE pending.integration_id = 1 AND pending.status IN ('queued', 'running');";
            using var reader = command.ExecuteReader();
            var plan = new List<string>();
            while (reader.Read())
            {
                plan.Add(reader.GetString(3));
            }

            Assert.Contains(plan, step => step.Contains("ix_integration_executions_pending", StringComparison.Ordinal));
        }

        [Fact]
        public void DeletingAnIntegrationSkipsItsQueuedRunsAndKeepsHistory()
        {
            using var temp = new TempDirectory();
            var (store, _) = CreateStore(temp);
            var saved = store.Save(Integration(new JsonObject { ["token"] = "secret-value-1234" }));
            var queued = store.Enqueue(saved, "client.connected", "connected", EventData.Sample(SrsEventTypes.ClientConnected));

            Assert.True(store.Delete(saved.Id));

            Assert.Null(store.Get(saved.Id));
            var run = store.GetExecution(queued);
            Assert.Equal(ExecutionStatus.Skipped, run.Status);
            Assert.Equal("Integration deleted", run.LastError);
        }

        [Fact]
        public void KeepsActionStateUntilTheIntegrationIsDeleted()
        {
            using var temp = new TempDirectory();
            var (store, _) = CreateStore(temp);
            var saved = store.Save(Integration(new JsonObject { ["token"] = "t" }));
            var other = store.Save(Integration(new JsonObject { ["token"] = "t" }, name: "Other"));

            store.State(saved.Id).Set("message", "111");
            store.State(saved.Id).Set("message", "222");
            store.State(other.Id).Set("message", "333");
            Assert.Equal("222", store.State(saved.Id).Get("message"));

            store.State(other.Id).Remove("message");
            Assert.Null(store.State(other.Id).Get("message"));

            Assert.True(store.Delete(saved.Id));
            Assert.Null(store.State(saved.Id).Get("message"));
        }

        [Fact]
        public void RetentionOnlyRemovesFinishedRuns()
        {
            using var temp = new TempDirectory();
            var (store, _) = CreateStore(temp);
            var saved = store.Save(Integration(new JsonObject { ["token"] = "secret-value-1234" }));
            var data = EventData.Sample(SrsEventTypes.ClientConnected);
            var finished = store.Enqueue(saved, "client.connected", null, data);
            store.Finish(finished, ExecutionStatus.Succeeded, null, "ok", null);
            var queued = store.Enqueue(saved, "client.connected", null, data);

            store.ApplyRetention(DateTime.UtcNow.AddDays(31), 30, 1000);

            Assert.Null(store.GetExecution(finished));
            Assert.NotNull(store.GetExecution(queued));
        }

        private static string ReadShared(string path)
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, System.Text.Encoding.Latin1);
            return reader.ReadToEnd();
        }

        internal static (IntegrationStore, IntegrationDatabase) CreateStore(TempDirectory temp)
        {
            var database = temp.CreateIntegrationDatabase();
            var protector = new SecretProtector(new EphemeralDataProtectionProvider());
            return (new IntegrationStore(database, protector, new IIntegrationAction[] { new RecordingAction() }), database);
        }

        internal static IntegrationDefinition Integration(JsonObject config, long id = 0, string name = "Test",
            string trigger = "client.connected", params IntegrationCondition[] conditions)
        {
            return new IntegrationDefinition
            {
                Id = id,
                Name = name,
                TriggerTypes = trigger.Split(','),
                Conditions = conditions,
                ActionType = RecordingAction.ActionType,
                ActionConfig = config
            };
        }
    }

    public class IntegrationServiceTests
    {
        [Fact]
        public void ValidatesBeforeSavingAndRecordsChanges()
        {
            using var temp = new TempDirectory();
            var (service, events, _) = Create(temp);
            var published = new List<SrsEvent>();
            events.EventPublished += published.Add;

            Assert.Equal("Enter a name.", service.Save(IntegrationStoreTests.Integration(new JsonObject(), name: " "), "admin", out _));
            Assert.Equal("\"nope\" is not an event type.",
                service.Save(IntegrationStoreTests.Integration(new JsonObject(), trigger: "nope"), "admin", out _));
            Assert.Equal("Add the event that runs this integration.", service.Validate(new IntegrationDefinition
            {
                Name = "No events", ActionType = RecordingAction.ActionType, ActionConfig = new JsonObject { ["token"] = "t" }
            }).FirstError);
            Assert.Equal("Enter a token.", service.Save(IntegrationStoreTests.Integration(new JsonObject()), "admin", out _));

            Assert.Null(service.Save(IntegrationStoreTests.Integration(new JsonObject { ["token"] = "a-secret-token" }),
                "admin", out var saved));
            // The stored token satisfies validation when the form leaves it empty.
            Assert.Null(service.Save(IntegrationStoreTests.Integration(new JsonObject(), saved.Id), "admin", out _));
            Assert.True(service.SetEnabled(saved.Id, false, "admin"));
            Assert.True(service.Delete(saved.Id, "admin"));

            Assert.Equal(new[]
            {
                "admin.integration_created", "admin.integration_updated", "admin.integration_updated",
                "admin.integration_deleted"
            }, published.Select(e => e.Type));
            Assert.False(published[2].Payload.GetProperty("integration").GetProperty("enabled").GetBoolean());
        }

        [Fact]
        public void ReportsEveryProblemByFieldInTheOrderOfTheForm()
        {
            using var temp = new TempDirectory();
            var (service, _, _) = Create(temp);
            var draft = new IntegrationDefinition
            {
                Name = "",
                TriggerTypes = new[] { "client.connected" },
                Conditions = new[]
                {
                    new IntegrationCondition("", ConditionOperator.GreaterThan, "ignored: no field"),
                    new IntegrationCondition("client.coalition", ConditionOperator.GreaterThan, "red"),
                    new IntegrationCondition("client.name", ConditionOperator.Contains, " ")
                },
                ActionType = RecordingAction.ActionType,
                ActionConfig = new JsonObject(),
                RetryMode = RetryMode.Fixed,
                MaxRetries = 0
            };

            var validation = service.Validate(draft);

            Assert.False(validation.IsValid);
            Assert.Equal(new[]
            {
                "name", "conditions[1].value", "conditions[2].value", "config.token", "maxRetries"
            }, validation.Errors.Keys);
            Assert.Equal("Enter a number.", validation.Errors["conditions[1].value"]);
            Assert.Equal("Enter a value.", validation.Errors["conditions[2].value"]);
            Assert.Equal("Enter a name.", validation.FirstError);
        }

        [Fact]
        public void SavingDropsConditionRowsWithoutAField()
        {
            using var temp = new TempDirectory();
            var (service, _, _) = Create(temp);

            Assert.Null(service.Save(IntegrationStoreTests.Integration(new JsonObject { ["token"] = "t" }, conditions: new[]
            {
                new IntegrationCondition("", ConditionOperator.Is, null),
                new IntegrationCondition("client.name", ConditionOperator.Is, "Hartmann")
            }), "admin", out var saved));

            Assert.Equal("client.name", Assert.Single(service.Store.Get(saved.Id).Conditions).Field);
        }

        [Fact]
        public async Task TestRunsTheActionWithSampleDataAndTheStoredSecret()
        {
            using var temp = new TempDirectory();
            var (service, _, action) = Create(temp);
            service.Save(IntegrationStoreTests.Integration(new JsonObject { ["token"] = "stored-secret-value" }), "admin",
                out var saved);

            var draft = IntegrationStoreTests.Integration(new JsonObject(), saved.Id, conditions: new[]
            {
                new IntegrationCondition("client.coalitionName", ConditionOperator.Is, "Blue")
            });
            var test = await service.TestAsync(draft, CancellationToken.None);

            var single = Assert.Single(test.Runs);
            Assert.True(test.Succeeded);
            Assert.Equal(SrsEventTypes.ClientConnected, single.Event);
            Assert.False(single.ConditionsMatchSample);
            Assert.True(action.Runs.TryDequeue(out var run));
            Assert.True(run.IsTest);
            Assert.Equal("stored-secret-value", run.Config["token"]!.GetValue<string>());
            Assert.Equal("Hartmann", EventData.AsText(EventData.Resolve(run.Data, "client.name")));
            Assert.Empty(service.Store.QueryExecutions());
        }

        [Fact]
        public async Task TestRunsOncePerTriggerEventWithItsExampleData()
        {
            using var temp = new TempDirectory();
            var (service, _, action) = Create(temp);

            var test = await service.TestAsync(IntegrationStoreTests.Integration(new JsonObject { ["token"] = "t" },
                trigger: "server.started,server.stopped"), CancellationToken.None);

            Assert.True(test.Succeeded);
            Assert.Equal(new[] { SrsEventTypes.ServerStarted, SrsEventTypes.ServerStopped }, test.Runs.Select(r => r.Event));
            Assert.Equal(new[] { "server.started", "server.stopped" },
                action.Runs.Select(r => EventData.AsText(EventData.Resolve(r.Data, "event.type"))));
        }

        /// <summary>The server's event types and the plugin's, as a server running the plugin has them.</summary>
        internal static readonly EventTypeRegistry EventTypes =
            new(new[] { new EventTypeRegistration(IntegrationEventTypes.All) });

        internal static (IntegrationService, ServerEvents, RecordingAction) Create(TempDirectory temp)
        {
            var action = new RecordingAction();
            var store = new IntegrationStore(temp.CreateIntegrationDatabase(), new SecretProtector(new EphemeralDataProtectionProvider()),
                new IIntegrationAction[] { action });
            var events = new ServerEvents();
            return (new IntegrationService(store, events, new IIntegrationAction[] { action }, EventTypes), events, action);
        }
    }

    public class IntegrationDispatcherTests
    {
        [Fact]
        public void QueuesRunsForEnabledIntegrationsWhoseConditionsMatch()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            Save(service, "Red joins", new IntegrationCondition("client.coalitionName", ConditionOperator.Is, "Red"));
            Save(service, "Blue joins", new IntegrationCondition("client.coalitionName", ConditionOperator.Is, "Blue"));
            var disabled = Save(service, "Disabled");
            service.SetEnabled(disabled.Id, false, "admin");

            dispatcher.Match(Connected(coalition: 1));
            dispatcher.Match(SrsEvent.Create(SrsEventTypes.ServerStarted, "system", "started"));

            var run = Assert.Single(service.Store.QueryExecutions());
            Assert.Equal("Red joins", run.IntegrationName);
            Assert.Equal(ExecutionStatus.Queued, run.Status);
            Assert.Equal("Hartmann", EventData.AsText(EventData.Resolve(JsonDocument.Parse(run.Data).RootElement, "client.name")));
        }

        [Fact]
        public void QueuesRunsForEveryTriggerEventOfAnIntegration()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            Assert.Null(service.Save(IntegrationStoreTests.Integration(new JsonObject { ["token"] = "t" }, name: "Status",
                trigger: "server.started,server.stopped"), "admin", out var saved));
            Assert.Equal(new[] { "server.started", "server.stopped" }, service.Store.Get(saved.Id).TriggerTypes);

            dispatcher.Match(SrsEvent.Create(SrsEventTypes.ServerStarted, "system", "started", EventPayload.Server(6002, 0, null)));
            dispatcher.Match(Connected());
            dispatcher.Match(SrsEvent.Create(SrsEventTypes.ServerStopped, "admin", "stopped", EventPayload.Server(6002, 3, null)));

            Assert.Equal(new[] { "server.stopped", "server.started" },
                service.Store.QueryExecutions().Select(r => r.EventType));
        }

        [Fact]
        public async Task NewerRunsWaitWhileAnOlderRunOfTheIntegrationWaitsForARetry()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            Save(service, "Status", retryMode: RetryMode.Fixed, maxRetries: 3);
            var other = Save(service, "Zulu");
            action.Results.Enqueue(IntegrationResult.Retry("Bad gateway"));
            dispatcher.Match(Connected(coalition: 1));
            dispatcher.Match(Connected(coalition: 2));

            var first = await RunNext(service, dispatcher);
            Assert.Equal(ExecutionStatus.Queued, first.Status);

            // Only the other integration's run can start; the newer "Status" run waits for the retry.
            Assert.Equal(new[] { other.Id }, service.Store.Due(DateTime.UtcNow, 100).Select(r => r.IntegrationId));
            Assert.Equal(first.Id, service.Store.Due(DateTime.UtcNow.AddHours(1), 100).First().Id);
        }

        [Fact]
        public async Task EventsPublishedWhileShuttingDownAreStillSent()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            Save(service, "Joins");

            events.Publish(Connected());
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await dispatcher.SendDueRunsAsync(deadline.Token);

            Assert.Single(action.Runs);
            Assert.Equal(ExecutionStatus.Succeeded, Assert.Single(service.Store.QueryExecutions()).Status);
        }

        [Fact]
        public async Task RetriesTemporaryFailuresUntilMaxRetriesThenFails()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            var integration = Save(service, "Flaky", retryMode: RetryMode.Exponential, maxRetries: 1);
            action.Results.Enqueue(IntegrationResult.Retry("Service unavailable"));
            action.Results.Enqueue(IntegrationResult.Retry("Still unavailable", TimeSpan.FromMinutes(5)));
            dispatcher.Match(Connected());

            var first = await RunNext(service, dispatcher);
            Assert.Equal(ExecutionStatus.Queued, first.Status);
            Assert.Equal(1, first.Attempts);
            Assert.Equal("Service unavailable", first.LastError);
            Assert.InRange(first.NextAttemptAtUtc!.Value - DateTime.UtcNow, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(11));

            var second = await RunNext(service, dispatcher, force: true);
            Assert.Equal(ExecutionStatus.Failed, second.Status);
            Assert.Equal(2, second.Attempts);
            Assert.Equal("Still unavailable", second.LastError);
            Assert.Equal(new[] { 1, 2 }, action.Runs.Select(r => r.Attempt));
            Assert.All(action.Runs, r => Assert.Equal(integration.Id, r.Integration.Id));
        }

        [Fact]
        public async Task RunsKeepTheirIntegrationsStateInTheDatabase()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            var integration = Save(service, "Status");
            dispatcher.Match(Connected());

            await RunNext(service, dispatcher);
            Assert.True(action.Runs.TryDequeue(out var run));
            run.State.Set("discord.message.abc", "111");

            Assert.Equal("111", service.Store.State(integration.Id).Get("discord.message.abc"));
        }

        [Fact]
        public async Task RateLimitedRunsWaitWithoutUsingUpARetry()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            Save(service, "No retries");
            action.Results.Enqueue(IntegrationResult.Defer("Slow down", TimeSpan.FromSeconds(20)));
            dispatcher.Match(Connected());

            var deferred = await RunNext(service, dispatcher);
            Assert.Equal(ExecutionStatus.Queued, deferred.Status);
            Assert.Equal(0, deferred.Attempts);
            Assert.Equal("Slow down", deferred.LastError);
            Assert.InRange(deferred.NextAttemptAtUtc!.Value - DateTime.UtcNow, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(21));

            var succeeded = await RunNext(service, dispatcher, force: true);
            Assert.Equal(ExecutionStatus.Succeeded, succeeded.Status);
            Assert.Equal(1, succeeded.Attempts);
        }

        [Fact]
        public async Task RecordsSuccessAndPermanentFailureAndSkipsDisabledIntegrations()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            var integration = Save(service, "Joins", retryMode: RetryMode.Fixed, maxRetries: 3);

            dispatcher.Match(Connected());
            var succeeded = await RunNext(service, dispatcher);
            Assert.Equal(ExecutionStatus.Succeeded, succeeded.Status);
            Assert.Equal("recorded", succeeded.Result);
            Assert.NotNull(succeeded.FinishedAtUtc);

            action.Results.Enqueue(IntegrationResult.Fail("Webhook deleted"));
            dispatcher.Match(Connected());
            var failed = await RunNext(service, dispatcher);
            Assert.Equal(ExecutionStatus.Failed, failed.Status);
            Assert.Equal(1, failed.Attempts);

            dispatcher.Match(Connected());
            service.SetEnabled(integration.Id, false, "admin");
            var skipped = await RunNext(service, dispatcher);
            Assert.Equal(ExecutionStatus.Skipped, skipped.Status);
            Assert.Equal(2, action.Runs.Count);
        }

        [Fact]
        public async Task AnUnexpectedErrorFailsTheRunWithoutHoldingBackLaterOnes()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            Save(service, "Joins");
            dispatcher.Match(Connected());
            dispatcher.Match(Connected());
            var broken = service.Store.QueryExecutions().Min(r => r.Id);
            using (var connection = temp.CreateIntegrationDatabase().Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE integration_executions SET data = 'not json' WHERE id = $id;";
                command.Parameters.AddWithValue("$id", broken);
                command.ExecuteNonQuery();
            }

            // Two passes, as the dispatcher's loop makes when the finished run wakes it.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await dispatcher.SendDueRunsAsync(deadline.Token);
            await dispatcher.SendDueRunsAsync(deadline.Token);

            var runs = service.Store.QueryExecutions().OrderBy(r => r.Id).ToList();
            Assert.Equal(ExecutionStatus.Failed, runs[0].Status);
            Assert.StartsWith("Failed unexpectedly:", runs[0].LastError);
            Assert.Equal(ExecutionStatus.Succeeded, runs[1].Status);
        }

        [Fact]
        public void MatchesAnIntegrationSavedAfterTheListWasRead()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var dispatcher = new IntegrationDispatcher(service.Store, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            Save(service, "First");
            dispatcher.Match(Connected());

            Save(service, "Second");
            dispatcher.Match(Connected());

            Assert.Equal(new[] { "First", "First", "Second" },
                service.Store.QueryExecutions().Select(r => r.IntegrationName).OrderBy(n => n));
        }

        [Fact]
        public async Task FailsARunWhoseStoredSecretCantBeRead()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var integration = Save(service, "Joins", retryMode: RetryMode.Fixed, maxRetries: 3);
            // As after the keys folder was replaced: the same database, read with other keys.
            var otherKeys = new IntegrationStore(temp.CreateIntegrationDatabase(),
                new SecretProtector(new EphemeralDataProtectionProvider()), new IIntegrationAction[] { action });
            var dispatcher = new IntegrationDispatcher(otherKeys, events, new IIntegrationAction[] { action },
                service.EventTypes, new AuditRetention());
            otherKeys.Enqueue(integration, "client.connected", null, EventData.Sample(SrsEventTypes.ClientConnected));

            var due = otherKeys.Due(DateTime.UtcNow, 1).Single();
            Assert.True(otherKeys.TryStart(due.Id));
            await dispatcher.RunAsync(due, CancellationToken.None);

            var run = otherKeys.GetExecution(due.Id);
            Assert.Equal(ExecutionStatus.Failed, run.Status);
            Assert.Equal("The stored token can't be read; enter it again on the integration page.", run.LastError);
            Assert.Empty(action.Runs);
        }

        [Fact]
        public void IgnoresASecondActionWithATypeAlreadyTaken()
        {
            using var temp = new TempDirectory();
            var (service, events, action) = IntegrationServiceTests.Create(temp);
            var actions = new IIntegrationAction[] { action, new RecordingAction() };

            var dispatcher = new IntegrationDispatcher(service.Store, events, actions, service.EventTypes, new AuditRetention());
            var withBoth = new IntegrationService(service.Store, events, actions, service.EventTypes);

            Assert.NotNull(dispatcher);
            Assert.Same(action, Assert.Single(withBoth.Actions));
        }

        [Fact]
        public void RunsLeftRunningAreQueuedAgainAtStartup()
        {
            using var temp = new TempDirectory();
            var (service, _, _) = IntegrationServiceTests.Create(temp);
            var integration = Save(service, "Joins");
            var id = service.Store.Enqueue(integration, "client.connected", null, EventData.Sample(SrsEventTypes.ClientConnected));
            Assert.True(service.Store.TryStart(id));
            Assert.False(service.Store.TryStart(id));

            Assert.Equal(1, service.Store.ResetRunning());
            var run = service.Store.GetExecution(id);
            Assert.Equal(ExecutionStatus.Queued, run.Status);
            // The interrupted attempt does not use up one of the integration's retries.
            Assert.Equal(0, run.Attempts);
        }

        private static IntegrationDefinition Save(IntegrationService service, string name,
            IntegrationCondition condition = null, RetryMode retryMode = RetryMode.None, int maxRetries = 0)
        {
            var error = service.Save(new IntegrationDefinition
            {
                Name = name,
                TriggerTypes = new[] { "client.connected" },
                Conditions = condition == null ? Array.Empty<IntegrationCondition>() : new[] { condition },
                ActionType = RecordingAction.ActionType,
                ActionConfig = new JsonObject { ["token"] = "a-secret-token" },
                RetryMode = retryMode,
                MaxRetries = maxRetries
            }, "admin", out var saved);
            Assert.Null(error);
            return saved;
        }

        private static async Task<IntegrationExecution> RunNext(IntegrationService service, IntegrationDispatcher dispatcher,
            bool force = false)
        {
            var due = service.Store.Due(force ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow, 1).Single();
            Assert.True(service.Store.TryStart(due.Id));
            await dispatcher.RunAsync(due, CancellationToken.None);
            return service.Store.GetExecution(due.Id);
        }

        private static SrsEvent Connected(int coalition = 1)
        {
            return SrsEvent.Create(SrsEventTypes.ClientConnected, "system", "Hartmann connected",
                EventPayload.Client(new SRClient { ClientGuid = "g", Name = "Hartmann", Coalition = coalition }));
        }
    }
}
