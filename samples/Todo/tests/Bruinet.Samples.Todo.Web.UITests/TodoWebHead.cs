using Bruinet.Core.Testing;
using Bruinet.Html.Bunit;
using Bruinet.Html.Context;
using Bruinet.Html.Playwright;
using Bruinet.Html.Testing;
using Bruinet.Samples.Todo.Core.Services;
using Bruinet.Samples.Todo.Infrastructure;
using Bruinet.Samples.Todo.Infrastructure.Data;
using Bruinet.Samples.Todo.Web.Services;
using Bruinet.Samples.Todo.Web.UITests.Spike;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Samples.Todo.Web.UITests;

/// <summary>
/// Where one device's app runs: its own host process behind a browser, or its composition rendered by bUnit
/// (<see cref="WebHeads"/>, <c>BRUINET_HEAD=bunit</c>). Started again on a restart with the device's settings.
/// </summary>
public interface ITodoWebHead : IAsyncDisposable
{
    /// <summary>The context page objects are built on.</summary>
    IHtmlTestContext Context { get; }

    /// <summary>The app's address.</summary>
    string BaseUrl { get; }

    /// <summary>The time the app spent in its repositories, on a head that measures it; else null.</summary>
    DatabaseClock? Clock { get; }

    /// <summary>Starts the app with <paramref name="settings"/>, stopping it first if it runs.</summary>
    void Start(LaunchSettings settings);

    /// <summary>SQL replay of the app's file: on bUnit, where the app is composed here; off in the browser.</summary>
    TodoSqlReplay Replay { get; }
}

/// <summary>The device's head for this run.</summary>
public static class TodoWebHeads
{
    /// <param name="name">The device's name.</param>
    /// <param name="httpHandler">The app's HTTP handler on bUnit (an HTTP replay's); the network when null. The browser's app has its own.</param>
    public static async Task<ITodoWebHead> CreateAsync(string name, HttpMessageHandler? httpHandler = null)
        => WebHeads.IsBunit ? new BunitTodoWebHead(name, httpHandler) : await BrowserTodoWebHead.CreateAsync(name);
}

/// <summary>The app as its own process (<see cref="WebAppHost"/>), one browser context that outlives restarts.</summary>
public sealed class BrowserTodoWebHead : ITodoWebHead
{
    private readonly string _name;

    // The app runs in its own process, on its own file: not replayed here.
    public TodoSqlReplay Replay { get; } = new(Project, head: null);

    internal const string Project = "Bruinet.Samples.Todo.Web.UITests";

    private readonly PlaywrightTestContext _context;
    private WebAppHost? _host;

    private BrowserTodoWebHead(string name, PlaywrightTestContext context)
    {
        _name = name;
        _context = context;
    }

    public static async Task<BrowserTodoWebHead> CreateAsync(string name)
        => new(name, await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions
        {
            Headless = !string.Equals(Environment.GetEnvironmentVariable("HEADLESS"), "false", StringComparison.OrdinalIgnoreCase),
            Name = name,
        }));

    public IHtmlTestContext Context => _context;

    public string BaseUrl => _host?.BaseUrl ?? throw new InvalidOperationException($"{_name} is not running.");

    // The app runs in its own process: its repositories are not reachable from here.
    public DatabaseClock? Clock => null;

    public void Start(LaunchSettings settings)
    {
        _host?.Dispose();
        _host = WebAppHost.Start(new WebAppHostOptions
        {
            AppPath = WebApp.Assembly(),
            Environment = settings.ToVariables(),
            ReadyPath = "/health",
            Name = $"Todo web ({_name})",
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        _host?.Dispose();
    }
}

/// <summary>
/// The app's composition (<c>Program.cs</c>'s registrations) as the fallback of a bUnit context over its
/// <c>Routes</c>: the singletons outlive page loads as in the host, and a restart builds them again, in the same
/// context (<see cref="BunitTestContext.RestartApp"/>), as the browser head keeps its browser context.
/// </summary>
public sealed class BunitTodoWebHead(string name, HttpMessageHandler? httpHandler = null) : ITodoWebHead
{
    private const string Address = "http://localhost";
    private ServiceProvider? _services;
    private BunitTestContext? _context;

    public TodoSqlReplay Replay { get; } = new(BrowserTodoWebHead.Project, "bunit", name == "todo-web" ? null : name);

    public IHtmlTestContext Context => _context ?? throw new InvalidOperationException($"{name} is not running.");

    public string BaseUrl => Address;

    public DatabaseClock? Clock { get; private set; }

    public void Start(LaunchSettings settings)
    {
        Clock = new DatabaseClock();
        var old = _services;
        _services = Compose(settings, Clock, Replay, httpHandler);

        // Started again: the same context with the app's new services, as the browser head keeps its browser context, so
        // what holds it (the UAT scenario's composition, its page objects) is not left with a disposed one.
        if (_context is not null)
        {
            _context.RestartApp(_services);
            old?.Dispose();
            return;
        }

        _context = new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Web.Components.Routes),
            StartBlank = true,
            FallbackServices = _services,
            BusyScript = "bruinetTodo.setBusy",
            Name = name,
        });
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }

    // Program.cs's registrations, with the repositories timed for the spike's measurement (.my/sql-replay/plan.md, 0.4).
    private static ServiceProvider Compose(LaunchSettings settings, DatabaseClock clock, TodoSqlReplay replay, HttpMessageHandler? httpHandler)
    {
        var services = new ServiceCollection();
        services.AddTodoServices(settings, new TodoPlatform(HttpHandler: httpHandler));
        services.AddSingleton<WebNavigator>();
        services.AddSingleton<INavigator>(provider => provider.GetRequiredService<WebNavigator>());
        services.AddSingleton<WebDialogs>();
        services.AddSingleton<IDialogs>(provider => provider.GetRequiredService<WebDialogs>());

        // The SQL log (BRUINET_SQL_LOG): every command the repositories send, through Bruinet's ADO.NET wrapper; and SQL
        // replay (BRUINET_SQL_REPLAY): the commands answered in the test's session (.my/sql-replay/plan.md, step 8).
        var log = Bruinet.Mocking.Data.SqlLog.FromEnvironment();
        services.AddSingleton(new TodoDatabase(settings.DatabasePath)
        {
            Wrap = connection => replay.Wrap(log is null ? connection : new Bruinet.Mocking.Data.LoggingDbConnection(connection, log)),
        });

        services.AddSingleton(clock);
        services.AddSingleton<SqliteTodoRepository>();
        services.AddSingleton<ITodoRepository>(provider => new TimedTodoRepository(provider.GetRequiredService<SqliteTodoRepository>(), clock));
        services.AddSingleton<SqliteUserRepository>();
        services.AddSingleton<IUserRepository>(provider => new TimedUserRepository(provider.GetRequiredService<SqliteUserRepository>(), clock));
        return services.BuildServiceProvider();
    }

    private void Stop()
    {
        _context?.Dispose();
        _context = null;
        _services?.Dispose();
        _services = null;
    }
}
