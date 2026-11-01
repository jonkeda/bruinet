using System.Globalization;
using Xunit;

namespace Bruinet.Bdd.Tests;

public sealed class BddValuesTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 9, 0, 0, TimeSpan.FromHours(2));

    private static readonly BddValueContext Context = new(Now, BddConfigParser.Parse("""
        # UAT Config

        ## Apps

        | Name | User | Role |
        | --- | --- | --- |
        | Eva's PC | Eva | Planner |
        | Bob | | |
        """).Apps);

    public enum Status
    {
        Open,
        InProgress,
        Done,
    }

    [Theory]
    [InlineData("7", 7)]
    [InlineData("-3", -3)]
    [InlineData("1,250", 1250)]
    [InlineData("1,250,000", 1250000)]
    public void Integers(string text, int expected) => Assert.Equal(expected, Read<int>(text));

    [Theory]
    [InlineData("0.5", "0.5")]
    [InlineData("1,250.75", "1250.75")]
    [InlineData("-0.25", "-0.25")]
    [InlineData("12", "12")]
    public void Decimals(string text, string expected) => Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), Read<decimal>(text));

    [Theory]
    [InlineData("1,5", "UATA003", "1.5")]
    [InlineData("1.250,75", "UATA003", "1,250.75")]
    [InlineData("12,50", "UATA003", "12.50")]
    [InlineData("1,25.0", "UATA003", "groups")]
    [InlineData("seven", "UATA001", "not a number")]
    public void Numbers_NearMisses_SayHowToWriteThem(string text, string code, string hint)
    {
        var problem = Problem<double>(text);

        Assert.Equal(code, problem.Code);
        Assert.Contains(hint, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWholeNumber_RefusesDecimals() => Assert.Equal("UATA003", Problem<int>("1.5").Code);

    [Fact]
    public void Numbers_DoNotDependOnTheMachineCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");

            Assert.Equal(1250.75m, Read<decimal>("1,250.75"));
            Assert.Equal("UATA003", Problem<decimal>("1,5").Code);
            Assert.Equal(new DateOnly(2026, 5, 12), Read<DateOnly>("12-May-2026"));
            Assert.Equal("1,250.75", BddValues.Write(1250.75m));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Theory]
    [InlineData("12-May-2026", 2026, 5, 12)]
    [InlineData("03-Jan-2027", 2027, 1, 3)]
    [InlineData("3-jan-2027", 2027, 1, 3)]
    [InlineData("3-JAN-2027", 2027, 1, 3)]
    [InlineData("today", 2026, 5, 12)]
    [InlineData("tomorrow", 2026, 5, 13)]
    [InlineData("+3d", 2026, 5, 15)]
    [InlineData("-1d", 2026, 5, 11)]
    public void Dates(string text, int year, int month, int day) => Assert.Equal(new DateOnly(year, month, day), Read<DateOnly>(text));

    [Theory]
    [InlineData("2026-05-12", "12-May-2026")]
    [InlineData("12-March-2026", "12-Mar-2026")]
    public void Dates_WithOneRightAnswer_CarryTheFix(string text, string fix)
    {
        var problem = Problem<DateOnly>(text);

        Assert.Equal("UATA002", problem.Code);
        Assert.Equal(fix, problem.Fix);
    }

    [Theory]
    [InlineData("12/05/2026")]
    [InlineData("12-mei-2026")]
    [InlineData("12-May-26")]
    [InlineData("31-Feb-2026")]
    [InlineData("12-May-2026 14:30")]
    public void Dates_NearMisses_AreRefused(string text) => Assert.Equal("UATA002", Problem<DateOnly>(text).Code);

    [Fact]
    public void DateTimes_TakeATime_AndTheClocksOffset()
    {
        Assert.Equal(new DateTime(2026, 5, 12, 14, 30, 0), Read<DateTime>("12-May-2026 14:30"));
        Assert.Equal(new DateTimeOffset(2026, 5, 12, 14, 30, 5, Now.Offset), Read<DateTimeOffset>("12-May-2026 14:30:05"));
        Assert.Equal(new DateTime(2026, 5, 12), Read<DateTime>("12-May-2026"));
    }

    [Fact]
    public void Times_AndDurations()
    {
        Assert.Equal(new TimeOnly(9, 15), Read<TimeOnly>("09:15"));
        Assert.Equal(TimeSpan.FromMinutes(90), Read<TimeSpan>("90m"));
        Assert.Equal(TimeSpan.FromMinutes(90), Read<TimeSpan>("1h 30m"));
        Assert.Equal(TimeSpan.FromDays(2), Read<TimeSpan>("2d"));
        Assert.Equal("UATA001", Problem<TimeSpan>("01:30:00").Code);
    }

    [Theory]
    [InlineData("Open", Status.Open)]
    [InlineData("in progress", Status.InProgress)]
    [InlineData("In_Progress", Status.InProgress)]
    public void Enums_ByName_AnyCase_WithSpaces(string text, Status expected) => Assert.Equal(expected, Read<Status>(text));

    [Fact]
    public void Enums_UnknownName_ListsTheNames_AndTheClosest()
    {
        var problem = Problem<Status>("Dnoe");

        Assert.Equal("UATA004", problem.Code);
        Assert.Contains("Did you mean Done?", problem.Message, StringComparison.Ordinal);
        Assert.Contains("Open, InProgress, Done", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Booleans_AreTrueOrFalse()
    {
        Assert.True(Read<bool>("True"));
        Assert.Equal("UATA001", Problem<bool>("yes").Code);
    }

    [Theory]
    [InlineData("Buy milk", "Buy milk")]
    [InlineData("\"Buy milk\"", "Buy milk")]
    [InlineData("\"\"", "")]
    [InlineData("\"  \"", "  ")]
    [InlineData("\"say \\\"hi\\\"\"", "say \"hi\"")]
    public void Strings_LoseTheirQuotes(string text, string expected) => Assert.Equal(expected, Read<string>(text));

    [Theory]
    [InlineData("true", true)]
    [InlineData("False", false)]
    [InlineData("on", true)]
    [InlineData("OFF", false)]
    public void Booleans_AreTrueFalse_OrOnOffForASwitch(string text, bool expected) => Assert.Equal(expected, Read<bool>(text));

    [Fact]
    public void Null_IsNull_WhereTheTypeAllowsIt()
    {
        Assert.Null(Read<int?>("null"));
        Assert.Null(Read<DateOnly?>(""));
        Assert.True(BddValues.TryRead("null", typeof(string), Context, out var value, out _, allowNull: true));
        Assert.Null(value);
        Assert.Equal("null", Read<string>("null"));
    }

    [Fact]
    public void Lists_SplitOnCommaSpace_AndALastAnd()
    {
        Assert.Equal(["Buy milk", "Plan the party", "Pay rent"], Read<string[]>("\"Buy milk\", \"Plan the party\" and \"Pay rent\""));
        Assert.Equal([1250.5m, 3m, 7m], Read<List<decimal>>("1,250.5, 3 and 7"));
        Assert.Equal(["salt, pepper", "bread"], Read<IReadOnlyList<string>>("\"salt, pepper\", bread"));
        Assert.Equal([new DateOnly(2026, 5, 12), new DateOnly(2026, 5, 13)], Read<DateOnly[]>("12-May-2026, +1d"));
        Assert.Equal(["Buy milk"], Read<string[]>("Buy milk"));
    }

    [Fact]
    public void Lists_NameTheItemThatDoesNotFit()
    {
        var problem = Problem<int[]>("1, two and 3");

        Assert.Equal("UATA001", problem.Code);
        Assert.StartsWith("item 2 of the list", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Users_ComeFromTheAppsTable()
    {
        var eva = Read<BddUserRole>("Eva");
        Assert.Equal("Eva", eva.Name);
        Assert.Equal("Planner", eva.Role);
        Assert.Equal("Eva's PC", Assert.Single(eva.Apps).Name);

        Assert.Equal("Eva", Read<BddUserRole>("Eva's PC").Name);
        Assert.Equal("UATA005", Problem<BddUserRole>("Carol").Code);
        Assert.Equal("UATA005", Assert.Throws<BddValueException>(() => BddValues.Read("Eva", typeof(BddUserRole), BddValueContext.Default)).Problem.Code);
    }

    [Fact]
    public void ArgumentTypes_ReadWithTheirParse()
    {
        Assert.Equal(new Money(1250m, "EUR"), Read<Money>("1,250.00 EUR"));
        Assert.Equal("UATA001", Problem<Money>("lots").Code);
        Assert.Equal("Buy milk!", BddValues.Read("Buy milk", typeof(Shout), Context with { Types = BddArgumentTypes.Discover([typeof(ShoutConverter)]) })!.ToString());
        Assert.True(BddValues.IsInline(typeof(Money)));
    }

    [Fact]
    public void Records_AreNotInline()
    {
        Assert.False(BddValues.IsInline(typeof(Money[][])));
        Assert.False(BddValues.IsInline(typeof(Row)));
        Assert.Equal("UATA001", Problem<Row>("x").Code);
    }

    [Theory]
    [MemberData(nameof(RoundTrips))]
    public void Write_ReadsBackAsTheSameValue(object value)
    {
        var text = BddValues.Write(value);

        Assert.Equal(value, BddValues.Read(text, value.GetType(), Context));
    }

    public static TheoryData<object> RoundTrips() => new()
    {
        new DateOnly(2027, 1, 3),
        new DateTime(2026, 5, 12, 14, 30, 0),
        new TimeOnly(9, 5),
        TimeSpan.FromMinutes(90),
        1250.75m,
        -1234567,
        Status.InProgress,
        true,
        "  spaced  ",
        string.Empty,
        "\"quoted\"",
    };

    [Fact]
    public void Write_ShowsTheScenarioForms()
    {
        Assert.Equal("03-Jan-2027", BddValues.Write(new DateOnly(2027, 1, 3)));
        Assert.Equal("1,250.75", BddValues.Write(1250.75));
        Assert.Equal("1h 30m", BddValues.Write(TimeSpan.FromMinutes(90)));
        Assert.Equal("a, b", BddValues.Write(new[] { "a", "b" }));
    }

    private static T Read<T>(string text) => (T)BddValues.Read(text, typeof(T), Context)!;

    private static BddValueProblem Problem<T>(string text)
    {
        Assert.False(BddValues.TryRead(text, typeof(T), Context, out _, out var problem), $"'{text}' was read as {typeof(T).Name}.");
        return problem!;
    }

    [BddArgumentType]
    public sealed record Money(decimal Amount, string Currency)
    {
        public static Money Parse(string text)
        {
            var parts = text.Split(' ');
            return parts.Length == 2
                ? new Money((decimal)BddValues.Read(parts[0], typeof(decimal), BddValueContext.Default)!, parts[1])
                : throw new FormatException("write an amount and a currency, e.g. 1,250.00 EUR");
        }
    }

    public sealed record Shout(string Text)
    {
        public override string ToString() => Text;
    }

    public sealed class ShoutConverter : IBddArgumentConverter<Shout>
    {
        public Shout Read(string text) => new(text + "!");
    }

    public sealed record Row(string Title);
}
