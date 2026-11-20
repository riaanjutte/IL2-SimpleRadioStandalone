using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations.Components;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations.Tests
{
    /// <summary>Answers webhook requests with scripted responses and remembers what was sent.</summary>
    internal sealed class FakeWebhook : HttpMessageHandler, IHttpClientFactory
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = new();
        public Queue<HttpResponseMessage> Responses { get; } = new();

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return Responses.Count > 0 ? Responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
        }

        public JsonElement LastJson => JsonDocument.Parse(Requests[^1].Body).RootElement;
    }

    public class MessageTemplateTests
    {
        private static readonly JsonElement Data = EventData.From(SrsEvent.Create(SrsEventTypes.ClientConnected,
            "system", "connected", EventPayload.Client(
                new SRClient { ClientGuid = "g", Name = "**Admin** [click](https://x.test) @everyone", Coalition = 1 },
                "203.0.113.7")));

        [Fact]
        public void InsertsFieldsAndSupportsConditions()
        {
            var text = MessageTemplate.Render(
                "{{ client.coalitionName }} {{ if client.coalition == 1 }}red{{ else }}blue{{ end }} {{ event.type }}",
                Data, TemplateEscape.None);

            Assert.Equal("Red red client.connected", text);
        }

        [Fact]
        public void EscapesMarkdownForDiscordButKeepsRawValues()
        {
            var text = MessageTemplate.Render("{{ client.name }}|{{ raw.client.name }}", Data,
                TemplateEscape.DiscordMarkdown);

            Assert.Equal(@"\*\*Admin\*\* \[click\]\(https://x.test\) @everyone|**Admin** [click](https://x.test) @everyone",
                text);
        }

        [Fact]
        public void LeavesTheAdminUrlUnescapedSoTheLinkWorks()
        {
            var data = EventData.From(SrsEvent.Create(SrsEventTypes.ServerStarted, "system", "started"),
                new JsonObject { ["name"] = "srs_main", ["adminUrl"] = "https://srs-admin.example.com/a_b" });

            Assert.Equal(@"srs\_main https://srs-admin.example.com/a_b",
                MessageTemplate.Render("{{ server.name }} {{ server.adminUrl }}", data, TemplateEscape.DiscordMarkdown));
        }

        [Fact]
        public void EscapesTextForJsonStrings()
        {
            var data = EventData.From(SrsEvent.Create(SrsEventTypes.ClientConnected, "system", "connected",
                EventPayload.Client(new SRClient { ClientGuid = "g", Name = "Bob \"the\" \\ pilot\n" }, "203.0.113.7")));

            var body = MessageTemplate.Render("{\"name\": \"{{ client.name }}\"}", data, TemplateEscape.Json);

            Assert.Equal("Bob \"the\" \\ pilot\n", JsonDocument.Parse(body).RootElement.GetProperty("name").GetString());
        }

        [Fact]
        public void ReportsTemplateErrors()
        {
            Assert.StartsWith("Template error at line 1", MessageTemplate.Check("{{ if }}"));
            Assert.Null(MessageTemplate.Check("{{ client.name }}"));
            Assert.Throws<TemplateException>(() => MessageTemplate.Render("{{ for i in 1..100000 }}x{{ end }}", Data,
                TemplateEscape.None));
            Assert.Throws<TemplateException>(() => MessageTemplate.Render("{{ include 'secrets.txt' }}", Data,
                TemplateEscape.None));
        }
    }

    public class DiscordWebhookActionTests
    {
        private const string Url = "https://discord.com/api/webhooks/123/abc";

        private static readonly JsonElement Data = EventData.From(SrsEvent.Create(SrsEventTypes.ClientConnected,
            "system", "connected", EventPayload.Client(
                new SRClient { ClientGuid = "g", Name = "<@1234> **Ace**", Coalition = 2 }, "203.0.113.7")));

        private static (DiscordWebhookAction, FakeWebhook) Create(int perMinute = 30)
        {
            var webhook = new FakeWebhook();
            return (new DiscordWebhookAction(webhook, new WebhookRateLimiter(perMinute, () => DateTime.UtcNow)), webhook);
        }

        private static IntegrationExecutionContext Context(JsonObject config, IIntegrationState state = null,
            long executionId = 1) => new()
        {
            ExecutionId = executionId,
            Config = config,
            EventType = SrsEventTypes.ClientConnected,
            Data = Data,
            State = state ?? new MemoryIntegrationState()
        };

        private static HttpResponseMessage Posted(string id) =>
            new(HttpStatusCode.OK) { Content = new StringContent($"{{\"id\": \"{id}\", \"content\": \"x\"}}") };

        [Fact]
        public void ValidatesTheWebhookUrlAndTemplates()
        {
            var (action, _) = Create();

            Assert.Equal("Enter the Discord webhook URL.", action.Validate(new JsonObject())["webhookUrl"]);
            Assert.Equal(new[] { "webhookUrl" },
                action.Validate(new JsonObject { ["webhookUrl"] = "https://example.com/api/webhooks/1/a" }).Keys);
            Assert.Equal(new[] { "webhookUrl" },
                action.Validate(new JsonObject { ["webhookUrl"] = "http://discord.com/api/webhooks/1/a" }).Keys);
            Assert.StartsWith("Template error at line 1",
                action.Validate(new JsonObject { ["webhookUrl"] = Url, ["content"] = "{{ if }}" })["content"]);
            Assert.Equal(new[] { "title", "color" }, action.Validate(new JsonObject
            {
                ["webhookUrl"] = Url, ["title"] = "{{ end }}", ["color"] = "red"
            }).Keys);
            Assert.Empty(action.Validate(new JsonObject { ["webhookUrl"] = Url }));
        }

        [Fact]
        public async Task PostsAnEscapedMessageThatMentionsNobody()
        {
            var (action, webhook) = Create();

            var result = await action.ExecuteAsync(Context(new JsonObject
            {
                ["webhookUrl"] = Url,
                ["content"] = "{{ client.name }} joined {{ client.coalitionName }}",
                ["username"] = "SRS"
            }), CancellationToken.None);

            Assert.True(result.IsSuccess, result.Message);
            var (request, _) = Assert.Single(webhook.Requests);
            Assert.Equal(Url + "?wait=true", request.RequestUri!.ToString());
            var body = webhook.LastJson;
            Assert.Equal(@"\<@1234\> \*\*Ace\*\* joined Blue", body.GetProperty("content").GetString());
            Assert.Equal("SRS", body.GetProperty("username").GetString());
            Assert.Equal(0, body.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
        }

        [Fact]
        public void BuildsAnEmbedWithTheDefaultTitleAndMessage()
        {
            var message = DiscordWebhookAction.BuildMessage(new JsonObject
            {
                ["style"] = "embed",
                ["color"] = "#ff0000"
            }, Data);

            var embed = message["embeds"]![0]!;
            Assert.Equal("Client connected", embed["title"]!.GetValue<string>());
            Assert.Equal("connected", embed["description"]!.GetValue<string>());
            Assert.Equal(0xff0000, embed["color"]!.GetValue<int>());
            Assert.NotNull(embed["timestamp"]);
            Assert.Null(message["content"]);
        }

        [Fact]
        public async Task WaitsForRateLimitsAndClassifiesErrors()
        {
            var (action, webhook) = Create();
            var config = new JsonObject { ["webhookUrl"] = Url };

            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"message\": \"You are being rate limited.\", \"retry_after\": 2.5}")
            });
            var limited = await action.ExecuteAsync(Context(config), CancellationToken.None);
            Assert.False(limited.CountsAsAttempt);
            Assert.Equal(TimeSpan.FromSeconds(2.5), limited.RetryAfter);

            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
            var deleted = await action.ExecuteAsync(Context(config), CancellationToken.None);
            Assert.Equal(ExecutionStatus.Failed, deleted.Status);

            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"message\": \"Cannot send an empty message\"}")
            });
            var rejected = await action.ExecuteAsync(Context(config), CancellationToken.None);
            Assert.Equal(ExecutionStatus.Failed, rejected.Status);
            Assert.Contains("Cannot send an empty message", rejected.Message);

            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.BadGateway));
            var outage = await action.ExecuteAsync(Context(config), CancellationToken.None);
            Assert.Equal(ExecutionStatus.Queued, outage.Status);
            Assert.True(outage.CountsAsAttempt);
        }

        [Fact]
        public async Task KeepsEachWebhookUnderItsRateLimit()
        {
            var (action, webhook) = Create(perMinute: 2);
            var config = new JsonObject { ["webhookUrl"] = Url };

            Assert.True((await action.ExecuteAsync(Context(config), CancellationToken.None)).IsSuccess);
            Assert.True((await action.ExecuteAsync(Context(config), CancellationToken.None)).IsSuccess);
            var third = await action.ExecuteAsync(Context(config), CancellationToken.None);

            Assert.False(third.CountsAsAttempt);
            Assert.InRange(third.RetryAfter!.Value, TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(60));
            Assert.Equal(2, webhook.Requests.Count);
        }

        [Fact]
        public async Task UpdatesClearTheOtherStyleSoAStyleChangeLeavesNothingBehind()
        {
            var (action, webhook) = Create();
            var state = new MemoryIntegrationState();
            var config = new JsonObject { ["webhookUrl"] = Url, ["messageMode"] = "update" };
            webhook.Responses.Enqueue(Posted("111"));
            await action.ExecuteAsync(Context(config, state), CancellationToken.None);

            config["style"] = "embed";
            await action.ExecuteAsync(Context(config, state), CancellationToken.None);
            var toEmbed = JsonDocument.Parse(webhook.Requests[^1].Body).RootElement;
            Assert.Equal("", toEmbed.GetProperty("content").GetString());
            Assert.Equal(1, toEmbed.GetProperty("embeds").GetArrayLength());

            config["style"] = "text";
            await action.ExecuteAsync(Context(config, state), CancellationToken.None);
            var toText = JsonDocument.Parse(webhook.Requests[^1].Body).RootElement;
            Assert.NotEqual("", toText.GetProperty("content").GetString());
            Assert.Equal(0, toText.GetProperty("embeds").GetArrayLength());
        }

        [Fact]
        public async Task KeepsOneMessageUpdatedAndPostsANewOneWhenItIsGone()
        {
            var (action, webhook) = Create();
            var state = new MemoryIntegrationState();
            var config = new JsonObject { ["webhookUrl"] = Url, ["messageMode"] = "update", ["username"] = "SRS" };

            webhook.Responses.Enqueue(Posted("111"));
            var first = await action.ExecuteAsync(Context(config, state), CancellationToken.None);
            Assert.Equal("Posted Discord message 111; the next events update it.", first.Message);
            Assert.Equal("111", action.DescribeState(config, state)[DiscordWebhookAction.MessageIdState]);

            var second = await action.ExecuteAsync(Context(config, state), CancellationToken.None);
            Assert.Equal("Updated Discord message 111.", second.Message);
            var (edit, editBody) = webhook.Requests[^1];
            Assert.Equal(HttpMethod.Patch, edit.Method);
            Assert.Equal(Url + "/messages/111", edit.RequestUri!.ToString());
            Assert.False(JsonDocument.Parse(editBody).RootElement.TryGetProperty("username", out _));

            // Someone deleted the message in Discord: post a new one and keep that one updated.
            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
            webhook.Responses.Enqueue(Posted("222"));
            var third = await action.ExecuteAsync(Context(config, state), CancellationToken.None);
            Assert.True(third.IsSuccess, third.Message);
            Assert.Equal(new[] { HttpMethod.Post, HttpMethod.Patch, HttpMethod.Patch, HttpMethod.Post },
                webhook.Requests.Select(r => r.Request.Method));

            await action.ExecuteAsync(Context(config, state), CancellationToken.None);
            Assert.Equal(Url + "/messages/222", webhook.Requests[^1].Request.RequestUri!.ToString());
        }

        [Fact]
        public async Task RemembersALinkToTheUpdatedMessage()
        {
            var (action, webhook) = Create();
            var state = new MemoryIntegrationState();
            var config = new JsonObject { ["webhookUrl"] = Url + "?thread_id=5", ["messageMode"] = "update" };
            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\": \"111\", \"channel_id\": \"5\"}")
            });
            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\": \"1\", \"guild_id\": \"42\", \"channel_id\": \"3\"}")
            });

            await action.ExecuteAsync(Context(config, state), CancellationToken.None);

            var (get, _) = webhook.Requests[^1];
            Assert.Equal(HttpMethod.Get, get.Method);
            Assert.Equal(Url, get.RequestUri!.ToString());
            var described = action.DescribeState(config, state);
            Assert.Equal("https://discord.com/channels/42/5/111", described[DiscordWebhookAction.MessageLinkState]);
            Assert.NotNull(described[DiscordWebhookAction.MessageUpdatedState]);
        }

        [Fact]
        public async Task TestRunsPostANewMessageAndLeaveTheUpdatedOneAlone()
        {
            var (action, webhook) = Create();
            var state = new MemoryIntegrationState();
            var config = new JsonObject { ["webhookUrl"] = Url, ["messageMode"] = "update" };
            webhook.Responses.Enqueue(Posted("111"));
            await action.ExecuteAsync(Context(config, state), CancellationToken.None);

            webhook.Responses.Enqueue(Posted("999"));
            var test = await action.ExecuteAsync(Context(config, state, executionId: 0), CancellationToken.None);

            Assert.True(test.IsSuccess);
            Assert.Equal(HttpMethod.Post, webhook.Requests[^1].Request.Method);
            await action.ExecuteAsync(Context(config, state), CancellationToken.None);
            Assert.Equal(Url + "/messages/111", webhook.Requests[^1].Request.RequestUri!.ToString());
        }

        [Fact]
        public void MessageUrlsKeepTheWebhookQuery()
        {
            Assert.Equal("https://discord.com/api/webhooks/1/abc/messages/9?thread_id=5",
                DiscordWebhookAction.MessageUrl("https://discord.com/api/webhooks/1/abc?thread_id=5", "9"));
        }

        [Fact]
        public void RateLimiterFreesSlotsAfterAMinute()
        {
            var now = new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
            var limiter = new WebhookRateLimiter(1, () => now);

            Assert.Null(limiter.TryAcquire("a"));
            Assert.Null(limiter.TryAcquire("b"));
            Assert.Equal(TimeSpan.FromSeconds(60), limiter.TryAcquire("a"));
            now = now.AddSeconds(60);
            Assert.Null(limiter.TryAcquire("a"));
            // "b" sent nothing in the last minute, so it is forgotten.
            Assert.Equal(1, limiter.Tracked);
        }
    }

    public class HttpWebhookActionTests
    {
        private const string Url = "https://example.com/hooks/srs";

        private static readonly JsonElement Data = EventData.Sample(SrsEventTypes.ClientConnected);

        private static IntegrationExecutionContext Context(JsonObject config) => new()
        {
            ExecutionId = 1,
            Config = config,
            EventType = SrsEventTypes.ClientConnected,
            Data = Data
        };

        [Fact]
        public void ValidatesUrlMethodHeadersAndBody()
        {
            var action = new HttpWebhookAction(new FakeWebhook());

            Assert.Equal("Enter the URL.", action.Validate(new JsonObject())["url"]);
            Assert.Contains("url", action.Validate(new JsonObject { ["url"] = "ftp://example.com" }).Keys);
            Assert.Contains("method", action.Validate(new JsonObject { ["url"] = Url, ["method"] = "DELETE" }).Keys);
            Assert.Contains("headers", action.Validate(new JsonObject { ["url"] = Url, ["headers"] = "no colon here" }).Keys);
            Assert.Contains("headers", action.Validate(new JsonObject { ["url"] = Url, ["headers"] = "Content-Type: text/plain" }).Keys);
            Assert.Contains("body", action.Validate(new JsonObject { ["url"] = Url, ["body"] = "{{ end }}" }).Keys);
            Assert.Empty(action.Validate(new JsonObject
            {
                ["url"] = Url, ["method"] = "put", ["headers"] = "Authorization: Bearer abc\nX-Source: srs"
            }));
        }

        [Fact]
        public async Task SendsTheEventDataAsJsonWithoutABodyTemplate()
        {
            var webhook = new FakeWebhook();
            var action = new HttpWebhookAction(webhook);

            var result = await action.ExecuteAsync(Context(new JsonObject
            {
                ["url"] = Url,
                ["headers"] = "Authorization: Bearer abc"
            }), CancellationToken.None);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Responded 200 OK.", result.Message);
            var (request, body) = Assert.Single(webhook.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer abc", request.Headers.Authorization!.ToString());
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal("client.connected", JsonDocument.Parse(body).RootElement.GetProperty("event").GetProperty("type").GetString());
        }

        [Fact]
        public async Task RendersTheBodyTemplate()
        {
            var webhook = new FakeWebhook();
            var action = new HttpWebhookAction(webhook);

            await action.ExecuteAsync(Context(new JsonObject
            {
                ["url"] = Url,
                ["method"] = "PUT",
                ["contentType"] = "text/plain",
                ["body"] = "{{ client.name }} joined"
            }), CancellationToken.None);

            var (request, body) = Assert.Single(webhook.Requests);
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("text/plain", request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal(EventData.AsText(EventData.Resolve(Data, "client.name")) + " joined", body);
        }

        [Fact]
        public async Task FailsOnInvalidJsonAndClassifiesResponses()
        {
            var webhook = new FakeWebhook();
            var action = new HttpWebhookAction(webhook);

            var invalid = await action.ExecuteAsync(Context(new JsonObject { ["url"] = Url, ["body"] = "{ nope" }),
                CancellationToken.None);
            Assert.Equal(ExecutionStatus.Failed, invalid.Status);
            Assert.Empty(webhook.Requests);

            var config = new JsonObject { ["url"] = Url };
            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad field") });
            var rejected = await action.ExecuteAsync(Context(config), CancellationToken.None);
            Assert.Equal(ExecutionStatus.Failed, rejected.Status);
            Assert.Equal("Responded 400 BadRequest: bad field", rejected.Message);

            webhook.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            Assert.Equal(ExecutionStatus.Queued, (await action.ExecuteAsync(Context(config), CancellationToken.None)).Status);

            var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            webhook.Responses.Enqueue(limited);
            var deferred = await action.ExecuteAsync(Context(config), CancellationToken.None);
            Assert.False(deferred.CountsAsAttempt);
            Assert.Equal(TimeSpan.FromSeconds(30), deferred.RetryAfter);
        }
    }

    public class ActionEditorTests
    {
        private static async Task<string> Render<T>(JsonObject config, IReadOnlyDictionary<string, string> hints = null,
            IReadOnlyDictionary<string, string> errors = null, IReadOnlyList<SrsEventType> triggers = null,
            IReadOnlyDictionary<string, string> state = null)
            where T : IComponent
        {
            await using var services = new ServiceCollection().BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
            return await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(new Dictionary<string, object>
                {
                    [nameof(IntegrationActionEditor.Config)] = config,
                    [nameof(IntegrationActionEditor.SecretHints)] = hints ?? new Dictionary<string, string>(),
                    [nameof(IntegrationActionEditor.Triggers)] = triggers ?? new[] { SrsEventTypes.ClientConnected },
                    [nameof(IntegrationActionEditor.Errors)] = errors ?? new Dictionary<string, string>(),
                    [nameof(IntegrationActionEditor.State)] = state ?? new Dictionary<string, string>()
                }));
                return WebUtility.HtmlDecode(output.ToHtmlString());
            });
        }

        [Fact]
        public async Task DiscordEditorPreviewsTheMessageAsDiscordShowsIt()
        {
            var html = await Render<DiscordWebhookEditor>(new JsonObject
            {
                ["style"] = "embed",
                ["content"] = "{{ client.name }} ({{ client.coalitionName }}) joined"
            }, new Dictionary<string, string> { ["webhookUrl"] = "ends in 0Jdl" });

            var name = EventData.AsText(EventData.Resolve(EventData.Sample(SrsEventTypes.ClientConnected), "client.name"));
            Assert.Contains("Stored — ends in 0Jdl", html);
            Assert.Contains("id=\"discord-color\"", html);
            Assert.Contains($"Client connected\n\n{name} (Red) joined", html);
        }

        [Fact]
        public async Task DiscordEditorShowsTheMessageItKeepsUpdated()
        {
            var config = new JsonObject { ["messageMode"] = "update" };

            var state = new Dictionary<string, string>
            {
                [DiscordWebhookAction.MessageIdState] = "1559535114692730952",
                [DiscordWebhookAction.MessageLinkState] = "https://discord.com/channels/1/2/1559535114692730952",
                [DiscordWebhookAction.MessageUpdatedState] = "2026-10-13T12:33:00Z"
            };

            Assert.Contains("None yet", await Render<DiscordWebhookEditor>(config));
            var html = await Render<DiscordWebhookEditor>(config, state: state);
            Assert.Contains("value=\"1559535114692730952\"", html);
            Assert.Contains("href=\"https://discord.com/channels/1/2/1559535114692730952\"", html);
            Assert.Contains("datetime=\"2026-10-13T12:33:00Z\"", html);
            Assert.DoesNotContain("Current message", await Render<DiscordWebhookEditor>(new JsonObject(), state: state));
        }

        [Fact]
        public async Task EditorsMarkTheFieldsThePageSaysAreInvalid()
        {
            var html = await Render<HttpWebhookEditor>(new JsonObject(),
                errors: new Dictionary<string, string> { ["url"] = "Enter the URL." });

            Assert.Contains("aria-invalid=\"true\" aria-describedby=\"config-url-error\"", html);
            Assert.Contains("<span id=\"config-url-error\" class=\"field-error\">Enter the URL.</span>", html);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "aria-invalid"));
        }

        [Fact]
        public async Task PreviewsTheMessageForEachTriggerEvent()
        {
            var html = await Render<DiscordWebhookEditor>(new JsonObject
            {
                ["content"] = "{{ if event.type == \"server.started\" }}🟢 online{{ else }}🔴 offline{{ end }}"
            }, triggers: new[] { SrsEventTypes.ServerStarted, SrsEventTypes.ServerStopped });

            Assert.Contains("Preview · Server started", html);
            Assert.Contains("🟢 online", html);
            Assert.Contains("Preview · Server stopped", html);
            Assert.Contains("🔴 offline", html);
        }

        [Fact]
        public async Task EditorsShowTemplateErrorsInThePreview()
        {
            var discord = await Render<DiscordWebhookEditor>(new JsonObject { ["content"] = "{{ for }}" });
            var http = await Render<HttpWebhookEditor>(new JsonObject { ["body"] = "{ \"name\": {{ client.name }} }" });

            Assert.Contains("Template error at line 1", discord);
            Assert.Contains("The body is not valid JSON for this event", http);
        }
    }
}
