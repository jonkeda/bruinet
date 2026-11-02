using Bruinet.Mocking;
using Bruinet.Bdd;

namespace Bruinet.Samples.Todo.TestSupport.Uat;

/// <summary>
/// What a hermetic Todo fixture arranges, whatever head it drives: the test set, the fake server,
/// the device's network and the app's database. The MAUI and web UAT fixtures both are one, so
/// steps that reach past the screen (<see cref="BackendPhrases"/>, <see cref="DevicePhrases"/>,
/// <see cref="StoredDataPhrases"/>) are written once.
/// </summary>
public interface ITodoArrangement : IBddClock
{
    /// <summary>The test set the app runs on.</summary>
    TodoTestSet TestSet { get; }

    /// <summary>Relative dates in steps (<c>+2d</c>) resolve against the test set's now.</summary>
    DateTimeOffset IBddClock.Now => TestSet.Now;

    /// <summary>The fake server's todos.</summary>
    TodoServerStub Server { get; }

    /// <summary>The fake server itself, for arranging faults and delays.</summary>
    MockApiServer Backend { get; }

    /// <summary>The device's network, as the app reads it.</summary>
    NetworkStateFile Network { get; }

    /// <summary>Drops everything arranged on the fake server, its todos included.</summary>
    void ResetBackend();

    /// <summary>The app's SQLite file.</summary>
    string DatabasePath { get; }

    /// <summary>
    /// The running scenario's HTTP calls while it is replayed (<see cref="TodoHttpReplay.ReplayedCalls"/>): a replay sends the
    /// stub server nothing, so what it received is read here. Null when the backend is the stub.
    /// </summary>
    IReadOnlyList<Bruinet.Mocking.Http.HttpCall>? ReplayedCalls => null;
}
