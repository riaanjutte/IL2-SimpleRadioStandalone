using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Api
{
    /// <summary>Authenticates API requests from "Authorization: Bearer &lt;key&gt;" or "X-Api-Key: &lt;key&gt;".</summary>
    public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ApiKey";
        public const string ScopeClaim = "scope";
        public const string ReadPolicy = ServerApi.ReadPolicy;
        public const string WritePolicy = ServerApi.WritePolicy;

        private readonly ApiKeyStore _keys;

        public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            UrlEncoder encoder, ApiKeyStore keys) : base(options, logger, encoder)
        {
            _keys = keys;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var key = ReadKey();
            if (key == null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var record = _keys.Validate(key);
            if (record == null)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid or revoked API key."));
            }

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "api:" + record.Name),
                new Claim(ScopeClaim, record.Scope)
            }, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
            return Task.CompletedTask;
        }

        private string ReadKey()
        {
            string authorization = Request.Headers[HeaderNames.Authorization];
            if (!string.IsNullOrEmpty(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authorization.Substring("Bearer ".Length).Trim();
            }

            string apiKey = Request.Headers["X-Api-Key"];
            return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        }
    }
}
