using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Admin
{
    public static class AdminEndpoints
    {
        public const string LoginRateLimitPolicy = "admin-login";

        public static IServiceCollection AddAdminLoginRateLimiting(this IServiceCollection services)
        {
            return services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, cancellationToken) =>
                    new System.Threading.Tasks.ValueTask(context.HttpContext.Response.WriteAsync(
                        "Too many login attempts. Wait a minute and try again.", cancellationToken));
                options.AddPolicy(LoginRateLimitPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));
            });
        }

        public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/account/login", async (
                    [FromForm] string password,
                    [FromForm] string returnUrl,
                    HttpContext context,
                    AdminAuthOptions auth,
                    ServerEvents events) =>
                {
                    var target = IsLocalUrl(returnUrl) ? returnUrl : "/";

                    if (string.IsNullOrEmpty(password))
                    {
                        return Results.LocalRedirect("/login?error=empty&returnUrl=" + Uri.EscapeDataString(target));
                    }

                    if (!auth.Verify(password))
                    {
                        events.Publish(SrsEventTypes.AdminLoginFailed, "anonymous",
                            $"Failed admin login from {RemoteAddress(context)}", EventPayload.Remote(RemoteAddress(context)));
                        return Results.LocalRedirect("/login?error=1&returnUrl=" + Uri.EscapeDataString(target));
                    }

                    var identity = new ClaimsIdentity(
                        new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, "admin"),
                            new Claim(AdminAuthOptions.SessionStampClaim, auth.SessionStamp ?? string.Empty)
                        },
                        CookieAuthenticationDefaults.AuthenticationScheme);
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(identity));

                    events.Publish(SrsEventTypes.AdminLogin, "admin", $"Admin login from {RemoteAddress(context)}",
                        EventPayload.Remote(RemoteAddress(context)));
                    return Results.LocalRedirect(target);
                })
                .RequireRateLimiting(LoginRateLimitPolicy)
                .AllowAnonymous()
                .ExcludeFromDescription();

            endpoints.MapPost("/account/logout", async (HttpContext context, ServerEvents events) =>
                {
                    if (context.User.Identity?.IsAuthenticated == true)
                    {
                        events.Publish(SrsEventTypes.AdminLogout, "admin", $"Admin logout from {RemoteAddress(context)}",
                            EventPayload.Remote(RemoteAddress(context)));
                    }

                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.LocalRedirect("/login");
                })
                .AllowAnonymous()
                .ExcludeFromDescription();

            // Unauthenticated liveness check for container orchestrators: 200 only when both listeners are up.
            endpoints.MapGet("/healthz", (ServerState state) =>
                    state.IsRunning && state.IsTcpListenerRunning && state.IsUdpListenerRunning
                        ? Results.Text("ok")
                        : Results.Text("unhealthy", statusCode: StatusCodes.Status503ServiceUnavailable))
                .AllowAnonymous()
                .ExcludeFromDescription();

            return endpoints;
        }

        private static string RemoteAddress(HttpContext context)
        {
            var address = context.Connection.RemoteIpAddress;
            return address == null ? "unknown" : BanStore.Normalize(address).ToString();
        }

        internal static bool IsLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || url[0] != '/')
            {
                return false;
            }

            // Reject protocol-relative ("//host") and backslash ("/\host") forms.
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
        }
    }
}
