// docs/guides/http-replay.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Mocking;
using Bruinet.Mocking.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Docs.Samples.Guides;

public static class HttpReplayGuide
{
    public static void InProcess(IServiceCollection services, string recordingsFolder, string testName, HttpReplayOptions options)
    {
        #region docs:session
        // The test's session: one per test, never shared; none while BRUINET_HTTP_REPLAY is unset.
        if (HttpReplaySession.FromEnvironment(recordingsFolder, testName, options) is not { } session)
        {
            return;
        }

        // An app in the test process: every IHttpClientFactory client goes through it...
        services.AddBruinetHttpReplay(session);
        // ...or a client the app makes itself, or an Azure SDK client (new HttpClientTransport(client)).
        var client = new HttpClient(HttpReplayHandler.Create(session));

        // At the test's end: save (record), write the call log, and fail on misses.
        session.Finish();
        session.ThrowIfMissed();
        #endregion
        client.Dispose();
    }

    public static void OwnProcess(MockApiServer proxy, MockApiServer replay, HttpReplaySession session, string path)
    {
        #region docs:server
        proxy.Record("https://api.example.com");   // what no stub answers goes on to the real service
        // ... the test runs ...
        proxy.CopyRecordedTo(session);
        session.Finish();

        replay.Replay(HttpRecording.Load(path)!);  // answers as recorded, in order
        Assert.Empty(replay.ReplayMisses());       // a miss answers 599 with the miss as its body
        #endregion
    }
}
