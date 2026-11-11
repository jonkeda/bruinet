using Bruinet.Mocking.Data.Replay;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Bruinet.Mocking.Data.AspNetCore;

/// <summary>Adds the SQL replay test-id hook (<see cref="ReplayHook"/>) to an ASP.NET Core app.</summary>
public static class SqlReplayHook
{
    /// <summary>
    /// Serves the hook's control paths and runs each request that names a test in that test's session
    /// (<see cref="ReplayScope"/>), for the app's <see cref="ReplayingDbProviderFactory"/> or EF's <c>UseSqlReplay()</c>.
    /// Without <paramref name="registry"/> it reads the app's environment (<see cref="ReplayRegistry.FromEnvironment"/>);
    /// with replay off there, it adds nothing.
    /// </summary>
    public static IApplicationBuilder UseSqlReplayHook(this IApplicationBuilder app, ReplayRegistry? registry = null)
    {
        registry ??= ReplayRegistry.FromEnvironment();
        if (registry is null)
        {
            return app;
        }

        return app.Use(async (context, next) =>
        {
            var request = context.Request;
            var remote = context.Connection.RemoteIpAddress;
            var loopback = remote is null || System.Net.IPAddress.IsLoopback(remote);
            if (ReplayHook.Control(registry, request.Path.Value ?? string.Empty, name => request.Query[name].FirstOrDefault(), name => request.Headers[name].FirstOrDefault(), loopback) is { } control)
            {
                if (control.Cookie is { } test)
                {
                    context.Response.Cookies.Append(ReplayHook.Cookie, test, new CookieOptions { Path = "/", HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = request.IsHttps });
                }

                context.Response.StatusCode = control.Status;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(control.Body);
                return;
            }

            var session = ReplayHook.TestOf(name => request.Headers[name].FirstOrDefault(), name => request.Cookies[name]) is { } named ? registry.Find(named) : null;
            using (ReplayScope.Enter(session))
            {
                await next(context);
            }
        });
    }
}
