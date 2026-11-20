using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>Shared HTTP handling for webhook actions.</summary>
    public static class WebhookHttp
    {
        /// <summary>Name of the <see cref="IHttpClientFactory"/> client the webhook actions use.</summary>
        public const string ClientName = "integrations";

        /// <summary>How long a 429 response asks to wait: Retry-After, or Discord's retry_after, or 10 s.</summary>
        public static async Task<TimeSpan> RetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var header = response.Headers.RetryAfter;
            if (header?.Delta is { } delta)
            {
                return Clamp(delta);
            }

            if (header?.Date is { } date)
            {
                return Clamp(date - DateTimeOffset.UtcNow);
            }

            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (json.RootElement.ValueKind == JsonValueKind.Object &&
                    json.RootElement.TryGetProperty("retry_after", out var seconds) &&
                    seconds.TryGetDouble(out var value))
                {
                    return Clamp(TimeSpan.FromSeconds(value));
                }
            }
            catch (JsonException)
            {
            }

            return TimeSpan.FromSeconds(10);
        }

        /// <summary>The start of a response body for error messages.</summary>
        public static async Task<string> BodyExcerptAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            return MessageTemplate.Truncate(body, 200);
        }

        public static string Describe(HttpStatusCode status)
        {
            return ((int)status).ToString(CultureInfo.InvariantCulture) + " " + status;
        }

        /// <summary>Server errors and timeouts may pass; other client errors will not.</summary>
        public static bool IsTemporary(HttpStatusCode status)
        {
            return (int)status >= 500 || status == HttpStatusCode.RequestTimeout;
        }

        private static TimeSpan Clamp(TimeSpan wait)
        {
            return wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1)
                : wait > TimeSpan.FromMinutes(10) ? TimeSpan.FromMinutes(10)
                : wait;
        }
    }
}
