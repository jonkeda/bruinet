using System;
using System.Web;
using Bruinet.Mocking.Data.Replay;

namespace Bruinet.Mocking.Data.SystemWeb;

/// <summary>
/// The SQL replay test-id hook (<see cref="ReplayHook"/>) for a System.Web app on IIS. Registered in web.config
/// (<c>&lt;system.webServer&gt;&lt;modules&gt;&lt;add name="SqlReplay" type="Bruinet.Mocking.Data.SystemWeb.SqlReplayModule, Bruinet.Mocking.Data.SystemWeb" /&gt;</c>);
/// with replay off in the app's environment it does nothing. The pipeline's events don't flow an <c>AsyncLocal</c>, so a
/// request's session is kept in its <see cref="HttpContext.Items"/> and found through <see cref="ReplayScope.Fallback"/>.
/// </summary>
public sealed class SqlReplayModule : IHttpModule
{
    private const string Key = "Bruinet.SqlReplay.Session";
    private static readonly Lazy<ReplayRegistry?> Registry = new(ReplayRegistry.FromEnvironment);

    public void Init(HttpApplication context)
    {
        if (Registry.Value is null)
        {
            return;
        }

        ReplayScope.Fallback = () => HttpContext.Current?.Items[Key] as ReplaySession;
        context.BeginRequest += (sender, _) => BeginRequest(((HttpApplication)sender).Context, Registry.Value);
    }

    public void Dispose()
    {
    }

    private static void BeginRequest(HttpContext context, ReplayRegistry registry)
    {
        var request = context.Request;
        if (ReplayHook.Control(registry, request.Path, name => request.QueryString[name], name => request.Headers[name], request.IsLocal) is { } control)
        {
            if (control.Cookie is { } test)
            {
                context.Response.Cookies.Add(new HttpCookie(ReplayHook.Cookie, test) { Path = "/", HttpOnly = true, Secure = request.IsSecureConnection });
            }

            context.Response.StatusCode = control.Status;
            context.Response.ContentType = "text/plain";
            context.Response.Write(control.Body);
            context.ApplicationInstance.CompleteRequest();
            return;
        }

        if (ReplayHook.TestOf(name => request.Headers[name], name => request.Cookies[name]?.Value) is { } named && registry.Find(named) is { } session)
        {
            context.Items[Key] = session;
        }
    }
}
