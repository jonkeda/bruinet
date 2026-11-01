using System.Globalization;
using CsCheck;
using Xunit;

namespace Bruinet.Bdd.Tests;

/// <summary>
/// Properties of the one value reader and writer (AD-016), over generated values rather than examples
/// (AD-033): whatever <see cref="BddValues.Write"/> writes, <see cref="BddValues.Read"/> reads back as
/// the same value, and on a Dutch machine as on any other.
/// </summary>
public sealed class BddValuesPropertyTests
{
    private static readonly BddValueContext Context = new(new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero), []);

    [Fact]
    public void Integers_ReadBackAsWritten() => RoundTrips(Gen.Long, typeof(long));

    [Fact]
    public void Decimals_ReadBackAsWritten() => RoundTrips(Gen.Decimal, typeof(decimal));

    [Fact]
    public void Booleans_ReadBackAsWritten() => RoundTrips(Gen.Bool, typeof(bool));

    [Fact]
    public void Dates_ReadBackAsWritten()
        => RoundTrips(Gen.Int[DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber].Select(DateOnly.FromDayNumber), typeof(DateOnly));

    [Fact]
    public void TimesToTheSecond_ReadBackAsWritten()
        => RoundTrips(Gen.Int[0, 24 * 60 * 60 - 1].Select(seconds => TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(seconds))), typeof(TimeOnly));

    [Fact]
    public void Text_ReadsBackAsWritten()
        => RoundTrips(Gen.String[Gen.Char["abcXYZ019 ,.-'\""], 1, 20], typeof(string));

    private static void RoundTrips<T>(Gen<T> values, Type type) where T : notnull
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            // Dutch writes 1.250,75 and di 12-5-2026: the reader must not care.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
            values.Sample(value =>
            {
                var written = BddValues.Write(value);
                Assert.True(BddValues.TryRead(written, type, Context, out var read, out var problem),
                    $"'{written}' (from {value}) did not read back: {problem?.Message}");
                Assert.Equal(value, read);
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
