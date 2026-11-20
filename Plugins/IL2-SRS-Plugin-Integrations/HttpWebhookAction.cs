using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations.Components;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// Sends an HTTP request with the event to any URL, for services other than Discord. The body is a template,
    /// or the event data as JSON when left empty.
    /// </summary>
    public sealed class HttpWebhookAction : IIntegrationAction
    {
        public const string ActionType = "http-webhook";
        public const string UrlField = "url";
        public const string MethodField = "method";
        public const string HeadersField = "headers";
        public const string ContentTypeField = "contentType";
        public const string BodyField = "body";

        public const string DefaultContentType = "application/json";
        public static readonly IReadOnlyList<string> Methods = new[] { "POST", "PUT", "PATCH" };

        private static readonly HashSet<string> ReservedHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Host", "Content-Length", "Content-Type", "Transfer-Encoding", "Connection"
        };

        private readonly IHttpClientFactory _http;

        public HttpWebhookAction(IHttpClientFactory http)
        {
            _http = http;
        }

        public string Type => ActionType;
        public string Label => "Send an HTTP request";

        public string Description =>
            "Sends the event to a URL, for example a Slack or Teams webhook or your own service. " +
            "Any 2xx response counts as success.";

        public Type ConfigEditor => typeof(HttpWebhookEditor);
        public IReadOnlyCollection<string> SecretFields { get; } = new[] { UrlField, HeadersField };

        public IReadOnlyDictionary<string, string> Validate(JsonObject config)
        {
            var errors = new Dictionary<string, string>(StringComparer.Ordinal);
            var url = config.GetText(UrlField);
            if (url == null)
            {
                errors[UrlField] = "Enter the URL.";
            }
            else if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors[UrlField] = "Enter a URL starting with http:// or https://.";
            }

            if (!Methods.Contains(Method(config)))
            {
                errors[MethodField] = "Choose POST, PUT or PATCH.";
            }

            if (!MediaTypeHeaderValue.TryParse(ContentType(config), out _))
            {
                errors[ContentTypeField] = "Enter a content type such as application/json.";
            }

            if (ParseHeaders(config.GetText(HeadersField), out _) is { } headerError)
            {
                errors[HeadersField] = headerError;
            }

            if (MessageTemplate.Check(config.GetText(BodyField)) is { } bodyError)
            {
                errors[BodyField] = bodyError;
            }

            return errors;
        }

        public async Task<IntegrationResult> ExecuteAsync(IntegrationExecutionContext context,
            CancellationToken cancellationToken)
        {
            string body;
            try
            {
                body = BuildBody(context.Config, context.Data);
            }
            catch (TemplateException ex)
            {
                return IntegrationResult.Fail(ex.Message);
            }

            ParseHeaders(context.Config.GetText(HeadersField), out var headers);
            using var request = new HttpRequestMessage(new HttpMethod(Method(context.Config)),
                context.Config.GetText(UrlField).Trim());
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ContentType(context.Config));
            if (request.Content.Headers.ContentType.CharSet == null)
            {
                request.Content.Headers.ContentType.CharSet = "utf-8";
            }

            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }

            using var response = await _http.CreateClient(WebhookHttp.ClientName).SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return IntegrationResult.Success($"Responded {WebhookHttp.Describe(response.StatusCode)}.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return IntegrationResult.Defer("The service asked to slow down (429).",
                    await WebhookHttp.RetryAfterAsync(response, cancellationToken));
            }

            var error = $"Responded {WebhookHttp.Describe(response.StatusCode)}";
            var excerpt = await WebhookHttp.BodyExcerptAsync(response, cancellationToken);
            if (excerpt.Length > 0)
            {
                error += ": " + excerpt;
            }

            return WebhookHttp.IsTemporary(response.StatusCode)
                ? IntegrationResult.Retry(error)
                : IntegrationResult.Fail(error);
        }

        /// <summary>The request body: the rendered template, or the event data as JSON without one.</summary>
        internal static string BuildBody(JsonObject config, JsonElement data)
        {
            var template = config.GetText(BodyField);
            if (template == null)
            {
                return data.GetRawText();
            }

            var json = IsJson(ContentType(config));
            var body = MessageTemplate.Render(template, data, json ? TemplateEscape.Json : TemplateEscape.None);
            if (json)
            {
                try
                {
                    using var _ = JsonDocument.Parse(body);
                }
                catch (JsonException ex)
                {
                    throw new TemplateException("The body is not valid JSON for this event: " + ex.Message);
                }
            }

            return body;
        }

        /// <summary>Parses "Name: value" lines; returns an error message or null.</summary>
        internal static string ParseHeaders(string text, out List<(string Name, string Value)> headers)
        {
            headers = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var colon = line.IndexOf(':');
                var name = colon > 0 ? line[..colon].Trim() : null;
                if (string.IsNullOrEmpty(name) || name.Any(c => c <= ' ' || c >= 127 || "()<>@,;:\\\"/[]?={}".Contains(c)))
                {
                    return $"Write each header as Name: value (\"{MessageTemplate.Truncate(line, 40)}\").";
                }

                if (ReservedHeaders.Contains(name))
                {
                    return name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                        ? "Set the content type in its own field, not as a header."
                        : $"The {name} header is set by the server.";
                }

                headers.Add((name, line[(colon + 1)..].Trim()));
            }

            return null;
        }

        public static bool IsJson(string contentType)
        {
            return MediaTypeHeaderValue.TryParse(contentType, out var parsed) && parsed.MediaType != null &&
                   (parsed.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                    parsed.MediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
        }

        internal static string Method(JsonObject config)
        {
            return (config.GetText(MethodField) ?? "POST").Trim().ToUpperInvariant();
        }

        internal static string ContentType(JsonObject config)
        {
            return config.GetText(ContentTypeField)?.Trim() ?? DefaultContentType;
        }
    }
}
