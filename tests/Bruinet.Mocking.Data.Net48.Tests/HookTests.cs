using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using Bruinet.Mocking.Data.Replay;
using Bruinet.Mocking.Data.SystemWeb;
using Xunit;

namespace Bruinet.Mocking.Data.Net48.Tests;

/// <summary>
/// The test-id hook's core on .NET Framework (H6 of .my/sql-replay/plan.md, step 4): what System.Web's module does with a
/// request, without IIS. The module itself only builds here.
/// </summary>
public sealed class HookTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "HookTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        ReplayScope.Fallback = null;
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Hook_BeginsFindsAndEnds_AndTheFactoryFindsTheSessionThroughTheFallback()
    {
        Assert.True(typeof(IHttpModule).IsAssignableFrom(typeof(SqlReplayModule)));
        var registry = new ReplayRegistry(ReplayMode.Record, _folder);
        var query = new Dictionary<string, string> { ["test"] = "Framework.Test" };

        var begin = ReplayHook.Control(registry, ReplayHook.BeginPath, name => query.TryGetValue(name, out var value) ? value : null, _ => null, loopback: true);
        var test = ReplayHook.TestOf(_ => null, name => name == ReplayHook.Cookie ? "Framework.Test" : null);
        ReplaySession? request = null;
        ReplayScope.Fallback = () => request;
        request = registry.Find(test!);
        var factory = new ReplayingDbProviderFactory(SqlClientFactory.Instance);

        Assert.Equal(200, begin!.Status);
        Assert.Equal("Framework.Test", begin.Cookie);
        Assert.IsType<ReplayingDbConnection>(factory.CreateConnection());
        request = null;
        Assert.IsType<SqlConnection>(factory.CreateConnection());
        Assert.Equal(200, ReplayHook.Control(registry, ReplayHook.EndPath, name => query.TryGetValue(name, out var value) ? value : null, _ => null, loopback: true)!.Status);
        Assert.Null(ReplayHook.Control(registry, "/places", _ => null, _ => null, loopback: true));
        Assert.Equal(400, ReplayHook.Control(registry, ReplayHook.BeginPath, _ => "../escape", _ => null, loopback: true)!.Status);
    }

    [Fact]
    public void Hook_WithNoKey_AnswersACallerOnAnotherMachine_AsIfItWereNotThere()
    {
        var registry = new ReplayRegistry(ReplayMode.Record, _folder);

        var answer = ReplayHook.Control(registry, ReplayHook.BeginPath, name => name == "test" ? "Remote.Test" : null, _ => null, loopback: false);

        Assert.Equal(404, answer!.Status);
        Assert.Null(registry.Find("Remote.Test"));
    }
}
