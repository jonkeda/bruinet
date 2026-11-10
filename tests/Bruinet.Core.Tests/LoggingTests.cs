using System.Text.Json.Nodes;
using Bruinet.Core.Logging;
using Bruinet.Core.Settings;

namespace Bruinet.Core.Tests;

// The CSV logger: one header, a line per call with its direction, and values a CSV reader can split.
public sealed class CsvTestLoggerTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "Bruinet", "CsvTestLoggerTests", $"{Guid.NewGuid():N}", "log.csv");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_file)!, recursive: true);

    [Fact]
    public void ItWritesOneHeader_ThenALinePerCall_WithItsDirection()
    {
        using (var logger = new CsvTestLogger(_file))
        {
            logger.Write(LogEvent.Entry("Saves", "Edit", "Save", "Click", null));
            logger.Write(LogEvent.Exit("Saves", "Edit", "Save", "Click", LogResult.Success, TimeSpan.FromMilliseconds(42)));
            logger.Write(new LogEvent(LogEventKind.Exit, "Saves", "Edit") { ControlId = "Title", Action = "AssertText", Actual = "Apple", Expected = "Pear", Result = LogResult.Fail, Duration = TimeSpan.FromMilliseconds(7), Message = "differs" });
        }

        var lines = File.ReadAllLines(_file);

        Assert.Equal("Timestamp;Direction;TestName;PageName;ControlId;Action;Value;Expected;Result;DurationMs;Message", lines[0]);
        Assert.Equal(4, lines.Length);
        Assert.Equal(["→", "Saves", "Edit", "Save", "Click", "", "", "", "", ""], lines[1].Split(';')[1..]);
        Assert.Equal(["←", "Saves", "Edit", "Save", "Click", "", "", "Success", "42", ""], lines[2].Split(';')[1..]);
        Assert.Equal(["←", "Saves", "Edit", "Title", "AssertText", "Apple", "Pear", "Fail", "7", "differs"], lines[3].Split(';')[1..]);
    }

    [Fact]
    public void AValueWithTheDelimiterOrAQuote_IsQuoted()
    {
        using (var logger = new CsvTestLogger(_file))
        {
            logger.Write(LogEvent.Info("Saves", "Edit", "a;b \"c\""));
        }

        Assert.EndsWith(";\"a;b \"\"c\"\"\"", File.ReadAllLines(_file)[1]);
    }

    [Fact]
    public void TheOtherEvents_WriteTheirResult()
    {
        using (var logger = new CsvTestLogger(_file))
        {
            logger.Write(LogEvent.Navigation("T", "List", "Edit"));
            logger.Write(LogEvent.Failed("T", "P", "C", "Click", new InvalidOperationException("gone")));
            logger.Write(LogEvent.Screenshot("T", "P", "shot.png", ScreenshotReason.AssertionFailure));
        }

        var results = File.ReadAllLines(_file).Skip(1).Select(line => line.Split(';')[8]).ToList();

        Assert.Equal(["Info", "Error", "Info"], results);
    }
}

// The console logger: a line per call, in the format a person reads in the test output.
[Collection(ConsoleOutput.Name)]
public sealed class ConsoleTestLoggerTests
{
    [Fact]
    public void ItWritesALinePerCall()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            var logger = new ConsoleTestLogger();
            logger.Write(LogEvent.Entry("Saves", "Edit", "Save", "Click", null));
            logger.Write(LogEvent.Exit("Saves", "Edit", "Save", "Click", LogResult.Success, TimeSpan.FromMilliseconds(42), "done"));
            logger.Write(new LogEvent(LogEventKind.Exit, "Saves", "Edit")
            {
                ControlId = "Title", Action = "AssertText", Expected = "Pear", Result = LogResult.Fail, Duration = TimeSpan.FromMilliseconds(7),
            });
            logger.Write(LogEvent.Screenshot("Saves", "Edit", "shot.png", ScreenshotReason.AssertionFailure));
            logger.Flush();
            logger.Dispose();
        }
        finally
        {
            Console.SetOut(original);
        }

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("[→] [Saves][Edit] Save.Click()", lines[0]);
        Assert.Equal("[←] [Saves][Edit] Save.Click Success 42ms (done)", lines[1]);
        Assert.Equal("[←] [Saves][Edit] Title.AssertText Actual=null Expected=Pear Fail 7ms", lines[2]);
        Assert.Contains("Screenshot (AssertionFailure): shot.png", lines[3]);
    }

    [Fact]
    public void TheOtherEvents_WriteTheirResultAndWhatTheyNamed()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            var logger = new ConsoleTestLogger();
            logger.Write(LogEvent.Navigation("T", "List", "Edit"));
            logger.Write(LogEvent.Info("T", "P", "ready"));
            logger.Write(LogEvent.Failed("T", "P", "Save", "Click", new InvalidOperationException("gone")));
        }
        finally
        {
            Console.SetOut(original);
        }

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(
        [
            "[Info] [T][List] .Navigate Value=Edit",
            "[Info] [T][P] .Info (ready)",
            "[Error] [T][P] Save.Click (gone)",
        ], lines);
    }
}

/// <summary>Tests that swap Console.Out run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleOutput
{
    public const string Name = "Console output";
}

// A setting read as an enum: any case of its name, its number, or an error naming the valid values.
public sealed class TestSettingsEnumTests
{
    [Theory]
    [InlineData("\"monday\"", DayOfWeek.Monday)]
    [InlineData("\"FRIDAY\"", DayOfWeek.Friday)]
    [InlineData("3", DayOfWeek.Wednesday)]
    public void AnEnumSetting_ReadsAnyCaseOfItsName_OrItsNumber(string json, DayOfWeek expected)
    {
        var settings = new TestSettings(JsonNode.Parse($$"""{ "day": {{json}} }""")!.AsObject());

        Assert.True(settings.TryGetValue<DayOfWeek>("day", out var day));
        Assert.Equal(expected, day);
    }

    [Fact]
    public void AnEnumSetting_ThatIsNoneOfItsNames_SaysWhichAre()
    {
        var settings = new TestSettings(JsonNode.Parse("""{ "day": "someday" }""")!.AsObject());

        var error = Assert.ThrowsAny<Exception>(() => settings.TryGetValue<DayOfWeek>("day", out _));

        Assert.Contains("Valid values are: Sunday, Monday", error.ToString());
    }
}

// A context with no pixels saves its tree as text beside the screenshot's path: the service gives that file.
public sealed class ScreenshotServiceTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-shots-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void AContextWithNoPixels_GivesTheTreeItSavedInstead()
    {
        var service = new Bruinet.Core.Services.ScreenshotService(
            path => File.WriteAllText(Bruinet.Core.Services.ScreenshotService.TreePathFor(path), "Page\n  Button #Save"),
            NullTestLogger.Instance,
            new Bruinet.Core.Configuration.ScreenshotSettings { OutputDirectory = _folder, IncludeTimestamp = false });

        var saved = service.Capture("Tests", "Saves", "failure");

        Assert.EndsWith(".tree.txt", saved);
        Assert.Contains("Button #Save", File.ReadAllText(saved));
    }

    // bUnit's context saves the page's markup under the screenshot's name (the benchmark's Tomorrow round: a .png path
    // given that was never written).
    [Fact]
    public void AContextThatSavesMarkup_GivesThatFile()
    {
        var service = new Bruinet.Core.Services.ScreenshotService(
            path => File.WriteAllText(Path.ChangeExtension(path, ".html"), "<button>Save</button>"),
            NullTestLogger.Instance,
            new Bruinet.Core.Configuration.ScreenshotSettings { OutputDirectory = _folder, IncludeTimestamp = false });

        var saved = service.Capture("Tests", "Saves", "paused");

        Assert.EndsWith(".html", saved);
        Assert.True(File.Exists(saved));
    }
}
