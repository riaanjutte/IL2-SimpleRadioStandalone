using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Data;
using Microsoft.Data.Sqlite;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// Integrations and their runs in srs.db. Secret action settings are encrypted when saved; integrations
    /// are returned with secrets masked unless loaded to run (<see cref="GetForExecution"/>).
    /// </summary>
    public sealed class IntegrationStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private const string IntegrationColumns =
            "id, name, description, enabled, trigger_type, conditions, action_type, action_config, retry_mode, " +
            "max_retries, created_at, updated_at";

        private const string ExecutionColumns =
            "id, integration_id, integration_name, action_type, event_type, event_message, data, status, attempts, " +
            "created_at, next_attempt_at, finished_at, last_error, result";

        private readonly IntegrationDatabase _database;
        private readonly SecretProtector _secrets;
        private readonly Dictionary<string, IIntegrationAction> _actions;

        public IntegrationStore(IntegrationDatabase database, SecretProtector secrets, IEnumerable<IIntegrationAction> actions)
        {
            _database = database;
            _secrets = secrets;
            _actions = IntegrationActions.ByType(actions);
        }

        /// <summary>An integration was created, changed or deleted.</summary>
        public event Action IntegrationsChanged;

        /// <summary>A run was queued, started or finished.</summary>
        public event Action ExecutionsChanged;

        // Integrations

        public IReadOnlyList<IntegrationDefinition> List()
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {IntegrationColumns} FROM integrations ORDER BY name COLLATE NOCASE, id;";
            return ReadIntegrations(command, decrypt: false);
        }

        public IntegrationDefinition Get(long id) => Find(id, decrypt: false);

        /// <summary>The integration with secrets decrypted, for running its action. Never show this to a user.</summary>
        public IntegrationDefinition GetForExecution(long id) => Find(id, decrypt: true);

        /// <summary>
        /// Creates (Id 0) or updates an integration. Secret settings that are empty keep their stored value
        /// when the action type is unchanged.
        /// </summary>
        public IntegrationDefinition Save(IntegrationDefinition integration)
        {
            var now = DateTime.UtcNow;
            var existing = integration.Id == 0 ? null : FindStoredConfig(integration.Id);
            if (integration.Id != 0 && existing == null)
            {
                return null;
            }

            var config = ProtectSecrets(integration, existing);

            long id;
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = integration.Id == 0
                    ? @"INSERT INTO integrations (name, description, enabled, trigger_type, conditions, action_type,
    action_config, retry_mode, max_retries, created_at, updated_at)
VALUES ($name, $description, $enabled, $trigger, $conditions, $action, $config, $retryMode, $maxRetries, $now, $now);
SELECT last_insert_rowid();"
                    : @"UPDATE integrations SET name = $name, description = $description, enabled = $enabled,
    trigger_type = $trigger, conditions = $conditions, action_type = $action, action_config = $config,
    retry_mode = $retryMode, max_retries = $maxRetries, updated_at = $now
WHERE id = $id;
SELECT $id;";
                command.Parameters.AddWithValue("$id", integration.Id);
                command.Parameters.AddWithValue("$name", integration.Name.Trim());
                command.Parameters.AddWithValue("$description",
                    string.IsNullOrWhiteSpace(integration.Description) ? DBNull.Value : integration.Description.Trim());
                command.Parameters.AddWithValue("$enabled", integration.Enabled ? 1 : 0);
                // Comma-separated; a single type (as stored by older builds) is a list of one.
                command.Parameters.AddWithValue("$trigger", string.Join(",", integration.TriggerTypes));
                command.Parameters.AddWithValue("$conditions", JsonSerializer.Serialize(
                    integration.Conditions.Where(c => !string.IsNullOrWhiteSpace(c.Field)), JsonOptions));
                command.Parameters.AddWithValue("$action", integration.ActionType);
                command.Parameters.AddWithValue("$config", config.ToJsonString());
                command.Parameters.AddWithValue("$retryMode", integration.RetryMode.ToString().ToLowerInvariant());
                command.Parameters.AddWithValue("$maxRetries",
                    integration.RetryMode == RetryMode.None ? 0 : integration.MaxRetries);
                command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(now));
                id = (long)command.ExecuteScalar();
            }

            RaiseIntegrationsChanged();
            return Get(id);
        }

        public bool SetEnabled(long id, bool enabled)
        {
            int updated;
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE integrations SET enabled = $enabled, updated_at = $now WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
                command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(DateTime.UtcNow));
                updated = command.ExecuteNonQuery();
            }

            if (updated > 0)
            {
                RaiseIntegrationsChanged();
            }

            return updated > 0;
        }

        /// <summary>Deletes an integration; its queued runs are skipped and its history is kept.</summary>
        public bool Delete(long id)
        {
            int deleted;
            using (var connection = _database.Open())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE integration_executions SET status = 'skipped', finished_at = $now, last_error = 'Integration deleted'
WHERE integration_id = $id AND status = 'queued';
DELETE FROM integration_state WHERE integration_id = $id;
DELETE FROM integrations WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(DateTime.UtcNow));
                command.ExecuteNonQuery();
                using var changes = connection.CreateCommand();
                changes.Transaction = transaction;
                changes.CommandText = "SELECT changes();";
                deleted = Convert.ToInt32(changes.ExecuteScalar());
                transaction.Commit();
            }

            if (deleted > 0)
            {
                RaiseIntegrationsChanged();
                RaiseExecutionsChanged();
            }

            return deleted > 0;
        }

        // Runs

        public long Enqueue(IntegrationDefinition integration, string eventType, string eventMessage, JsonElement data)
        {
            long id;
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO integration_executions (integration_id, integration_name, action_type, event_type, event_message, data,
    status, attempts, created_at, next_attempt_at)
VALUES ($integration, $name, $action, $eventType, $message, $data, 'queued', 0, $now, $now);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("$integration", integration.Id);
                command.Parameters.AddWithValue("$name", integration.Name);
                command.Parameters.AddWithValue("$action", integration.ActionType);
                command.Parameters.AddWithValue("$eventType", eventType);
                command.Parameters.AddWithValue("$message", (object)eventMessage ?? DBNull.Value);
                command.Parameters.AddWithValue("$data", data.GetRawText());
                command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(DateTime.UtcNow));
                id = (long)command.ExecuteScalar();
            }

            RaiseExecutionsChanged();
            return id;
        }

        /// <summary>Queued runs that are due, oldest first.</summary>
        public IReadOnlyList<IntegrationExecution> Due(DateTime utcNow, int limit)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $@"SELECT {ExecutionColumns} FROM integration_executions
WHERE status = 'queued' AND next_attempt_at <= $now AND {IsOldestPending}
ORDER BY id LIMIT $limit;";
            command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(utcNow));
            command.Parameters.AddWithValue("$limit", limit);
            return ReadExecutions(command);
        }

        /// <summary>
        /// Only an integration's oldest queued or running run may start, so runs go out in event order: a newer
        /// run waits while an older one waits for a retry, e.g. a "started" status is never sent after "stopped".
        /// The index holds only pending runs; without it SQLite picks the one on integration_id and reads the
        /// integration's whole history for every check.
        /// </summary>
        private const string IsOldestPending = @"id = (SELECT MIN(pending.id) FROM integration_executions pending
    INDEXED BY ix_integration_executions_pending
    WHERE pending.integration_id = integration_executions.integration_id AND pending.status IN ('queued', 'running'))";

        /// <summary>When the next run that can start is due, or null if there is none.</summary>
        public DateTime? NextDueAt()
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT MIN(next_attempt_at) FROM integration_executions WHERE status = 'queued' AND {IsOldestPending};";
            return SqliteDatabase.ParseNullableTimestamp(command.ExecuteScalar());
        }

        /// <summary>Marks a queued run as running and counts the attempt; false if another worker took it.</summary>
        public bool TryStart(long executionId)
        {
            int updated;
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"UPDATE integration_executions SET status = 'running', attempts = attempts + 1
WHERE id = $id AND status = 'queued';";
                command.Parameters.AddWithValue("$id", executionId);
                updated = command.ExecuteNonQuery();
            }

            if (updated > 0)
            {
                RaiseExecutionsChanged();
            }

            return updated > 0;
        }

        /// <summary>Records the outcome of an attempt; a queued status with a time schedules a retry.</summary>
        public void Finish(long executionId, string status, string error, string result, DateTime? nextAttemptAtUtc)
        {
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"UPDATE integration_executions SET status = $status, last_error = $error,
    result = $result, next_attempt_at = $next, finished_at = $finished
WHERE id = $id;";
                command.Parameters.AddWithValue("$id", executionId);
                command.Parameters.AddWithValue("$status", status);
                command.Parameters.AddWithValue("$error", (object)Truncate(error) ?? DBNull.Value);
                command.Parameters.AddWithValue("$result", (object)Truncate(result) ?? DBNull.Value);
                command.Parameters.AddWithValue("$next",
                    nextAttemptAtUtc.HasValue ? SqliteDatabase.FormatTimestamp(nextAttemptAtUtc.Value) : DBNull.Value);
                command.Parameters.AddWithValue("$finished", ExecutionStatus.IsFinished(status)
                    ? SqliteDatabase.FormatTimestamp(DateTime.UtcNow)
                    : DBNull.Value);
                command.ExecuteNonQuery();
            }

            RaiseExecutionsChanged();
        }

        /// <summary>Queues a run again for later without counting the attempt it just made (rate limits).</summary>
        public void Defer(long executionId, string reason, DateTime nextAttemptAtUtc)
        {
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"UPDATE integration_executions SET status = 'queued', attempts = MAX(attempts - 1, 0),
    last_error = $reason, next_attempt_at = $next
WHERE id = $id;";
                command.Parameters.AddWithValue("$id", executionId);
                command.Parameters.AddWithValue("$reason", (object)Truncate(reason) ?? DBNull.Value);
                command.Parameters.AddWithValue("$next", SqliteDatabase.FormatTimestamp(nextAttemptAtUtc));
                command.ExecuteNonQuery();
            }

            RaiseExecutionsChanged();
        }

        /// <summary>Queues a finished run again, to run now.</summary>
        public bool Requeue(long executionId)
        {
            int updated;
            using (var connection = _database.Open())
            using (var command = connection.CreateCommand())
            {
                // From attempt 1 again, so it gets the integration's retries.
                command.CommandText = @"UPDATE integration_executions SET status = 'queued', attempts = 0,
    next_attempt_at = $now, finished_at = NULL
WHERE id = $id AND status IN ('failed', 'skipped', 'succeeded');";
                command.Parameters.AddWithValue("$id", executionId);
                command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(DateTime.UtcNow));
                updated = command.ExecuteNonQuery();
            }

            if (updated > 0)
            {
                RaiseExecutionsChanged();
            }

            return updated > 0;
        }

        /// <summary>
        /// Queues runs left running by a previous process (it stopped mid-run) again. The interrupted attempt is
        /// not counted, as with <see cref="Defer"/>, so a shutdown does not use up one of the integration's retries.
        /// </summary>
        public int ResetRunning()
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE integration_executions SET status = 'queued', attempts = MAX(attempts - 1, 0),
    next_attempt_at = $now
WHERE status = 'running';";
            command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(DateTime.UtcNow));
            return command.ExecuteNonQuery();
        }

        public IReadOnlyList<IntegrationExecution> QueryExecutions(string status = null, long? integrationId = null,
            int limit = 200)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            var where = new List<string>();
            if (!string.IsNullOrWhiteSpace(status))
            {
                where.Add("status = $status");
                command.Parameters.AddWithValue("$status", status.Trim());
            }

            if (integrationId.HasValue)
            {
                where.Add("integration_id = $integration");
                command.Parameters.AddWithValue("$integration", integrationId.Value);
            }

            command.CommandText = $"SELECT {ExecutionColumns} FROM integration_executions" +
                                  (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty) +
                                  " ORDER BY id DESC LIMIT $limit;";
            command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
            return ReadExecutions(command);
        }

        public IntegrationExecution GetExecution(long id)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {ExecutionColumns} FROM integration_executions WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            return ReadExecutions(command).FirstOrDefault();
        }

        /// <summary>The newest run of each integration, keyed by integration id.</summary>
        public IReadOnlyDictionary<long, IntegrationExecution> LatestExecutions()
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $@"SELECT {ExecutionColumns} FROM integration_executions
WHERE id IN (SELECT MAX(id) FROM integration_executions GROUP BY integration_id);";
            return ReadExecutions(command).ToDictionary(e => e.IntegrationId);
        }

        /// <summary>Removes finished runs older than <paramref name="days"/> and beyond the newest <paramref name="maxRows"/>.</summary>
        public long ApplyRetention(DateTime utcNow, int days, int maxRows)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
DELETE FROM integration_executions WHERE status IN ('succeeded', 'failed', 'skipped') AND created_at < $cutoff;
DELETE FROM integration_executions WHERE status IN ('succeeded', 'failed', 'skipped')
    AND id <= (SELECT id FROM integration_executions ORDER BY id DESC LIMIT 1 OFFSET $maxRows);";
            command.Parameters.AddWithValue("$cutoff", SqliteDatabase.FormatTimestamp(utcNow.AddDays(-days)));
            command.Parameters.AddWithValue("$maxRows", maxRows);
            return command.ExecuteNonQuery();
        }

        // Secrets

        private JsonObject ProtectSecrets(IntegrationDefinition integration, (string ActionType, JsonObject Config)? existing)
        {
            var config = (JsonObject)(integration.ActionConfig?.DeepClone() ?? new JsonObject());
            var secretFields = _actions.TryGetValue(integration.ActionType, out var action)
                ? action.SecretFields
                : Array.Empty<string>();
            var keepStored = existing.HasValue && existing.Value.ActionType == integration.ActionType;

            foreach (var field in secretFields)
            {
                var value = config.TryGetPropertyValue(field, out var node) && node is JsonValue jsonValue &&
                            jsonValue.TryGetValue<string>(out var text)
                    ? text?.Trim()
                    : null;

                if (!string.IsNullOrEmpty(value))
                {
                    config[field] = _secrets.Protect(value);
                }
                else if (keepStored && !config.ClearsSecret(field) &&
                         existing.Value.Config.TryGetPropertyValue(field, out var stored) && stored != null)
                {
                    config[field] = stored.DeepClone();
                }
                else
                {
                    config.Remove(field);
                }
            }

            config.Remove(ActionConfigExtensions.ClearSecretsField);
            return config;
        }

        private (string ActionType, JsonObject Config)? FindStoredConfig(long id)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT action_type, action_config FROM integrations WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            return reader.Read() ? (reader.GetString(0), ParseObject(reader.GetString(1))) : null;
        }

        private IntegrationDefinition Find(long id, bool decrypt)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {IntegrationColumns} FROM integrations WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            return ReadIntegrations(command, decrypt).FirstOrDefault();
        }

        private List<IntegrationDefinition> ReadIntegrations(SqliteCommand command, bool decrypt)
        {
            var integrations = new List<IntegrationDefinition>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var config = ParseObject(reader.GetString(7));
                var hints = new Dictionary<string, string>(StringComparer.Ordinal);
                var unreadable = new List<string>();

                // Secrets are recognised by their stored form, so they stay hidden even if the action is gone.
                foreach (var property in config.ToList())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var text) &&
                        text.StartsWith("protected:", StringComparison.Ordinal))
                    {
                        var secret = _secrets.Unprotect(text);
                        hints[property.Key] = SecretProtector.Hint(secret);
                        config[property.Key] = decrypt ? secret : null;
                        if (secret == null)
                        {
                            unreadable.Add(property.Key);
                        }
                    }
                }

                integrations.Add(new IntegrationDefinition
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Enabled = reader.GetInt64(3) != 0,
                    TriggerTypes = reader.GetString(4)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    Conditions = ParseConditions(reader.GetString(5)),
                    ActionType = reader.GetString(6),
                    ActionConfig = config,
                    SecretHints = hints,
                    UnreadableSecrets = unreadable,
                    RetryMode = Enum.TryParse<RetryMode>(reader.GetString(8), true, out var mode) ? mode : RetryMode.None,
                    MaxRetries = (int)reader.GetInt64(9),
                    CreatedAtUtc = SqliteDatabase.ParseTimestamp(reader.GetString(10)),
                    UpdatedAtUtc = SqliteDatabase.ParseTimestamp(reader.GetString(11))
                });
            }

            return integrations;
        }

        private static List<IntegrationExecution> ReadExecutions(SqliteCommand command)
        {
            var executions = new List<IntegrationExecution>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                executions.Add(new IntegrationExecution
                {
                    Id = reader.GetInt64(0),
                    IntegrationId = reader.GetInt64(1),
                    IntegrationName = reader.GetString(2),
                    ActionType = reader.GetString(3),
                    EventType = reader.GetString(4),
                    EventMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Data = reader.GetString(6),
                    Status = reader.GetString(7),
                    Attempts = (int)reader.GetInt64(8),
                    CreatedAtUtc = SqliteDatabase.ParseTimestamp(reader.GetString(9)),
                    NextAttemptAtUtc = SqliteDatabase.ParseNullableTimestamp(reader.GetValue(10)),
                    FinishedAtUtc = SqliteDatabase.ParseNullableTimestamp(reader.GetValue(11)),
                    LastError = reader.IsDBNull(12) ? null : reader.GetString(12),
                    Result = reader.IsDBNull(13) ? null : reader.GetString(13)
                });
            }

            return executions;
        }

        private static JsonObject ParseObject(string json)
        {
            try
            {
                return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                return new JsonObject();
            }
        }

        private static IReadOnlyList<IntegrationCondition> ParseConditions(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<IntegrationCondition>>(json, JsonOptions) ??
                       new List<IntegrationCondition>();
            }
            catch (JsonException)
            {
                return new List<IntegrationCondition>();
            }
        }

        /// <summary>The state an integration's action keeps between runs (see <see cref="IIntegrationState"/>).</summary>
        public IIntegrationState State(long integrationId) => new StoredState(this, integrationId);

        internal string GetState(long integrationId, string key)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM integration_state WHERE integration_id = $id AND key = $key;";
            command.Parameters.AddWithValue("$id", integrationId);
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string;
        }

        internal void SetState(long integrationId, string key, string value)
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = value == null
                ? "DELETE FROM integration_state WHERE integration_id = $id AND key = $key;"
                : @"INSERT INTO integration_state (integration_id, key, value, updated_at) VALUES ($id, $key, $value, $now)
ON CONFLICT (integration_id, key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;";
            command.Parameters.AddWithValue("$id", integrationId);
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", (object)value ?? DBNull.Value);
            command.Parameters.AddWithValue("$now", SqliteDatabase.FormatTimestamp(DateTime.UtcNow));
            command.ExecuteNonQuery();
        }

        private sealed class StoredState : IIntegrationState
        {
            private readonly IntegrationStore _store;
            private readonly long _integrationId;

            public StoredState(IntegrationStore store, long integrationId)
            {
                _store = store;
                _integrationId = integrationId;
            }

            public string Get(string key) => _store.GetState(_integrationId, key);
            public void Set(string key, string value) => _store.SetState(_integrationId, key, value);
            public void Remove(string key) => _store.SetState(_integrationId, key, null);
        }

        private static string Truncate(string text)
        {
            return text == null || text.Length <= 2000 ? text : text.Substring(0, 2000) + "…";
        }

        private void RaiseIntegrationsChanged() => Raise(IntegrationsChanged);

        private void RaiseExecutionsChanged() => Raise(ExecutionsChanged);

        private static void Raise(Action handlers)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Integration store subscriber failed");
                }
            }
        }
    }
}
