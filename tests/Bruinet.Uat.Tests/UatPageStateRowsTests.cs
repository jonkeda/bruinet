using Xunit;

namespace Bruinet.Uat.Tests;

// step_state's reading of a list: its rows, as each row's own controls show them (the benchmark's T4 runs read no titles).
public sealed class UatPageStateRowsTests
{
    [Fact]
    public void AListsRows_AreReadThroughEachRowsControls_ShownOnes_WithTheirValues()
    {
        var list = new FakeList(
            new FakeRow(new FakeText("Buy milk"), new FakeText("Open")),
            new FakeRow(new FakeText("Book dentist"), new FakeText("In progress", shown: false)));

        Assert.Equal("2 rows: [Title 'Buy milk', Status 'Open'], [Title 'Book dentist']", UatPageState.Rows(list));
    }

    [Fact]
    public void ARowThatLeadsBackToItsList_IsReadOneLevelDeep()
    {
        var list = new FakeList(new FakeRow(new FakeText("Buy milk"), new FakeText("Open")));
        list.Rows[0].Parent = list;

        Assert.Equal("1 rows: [Title 'Buy milk', Status 'Open']", UatPageState.Rows(list));
    }

    [Fact]
    public void AControlWithoutRows_IsNoList() => Assert.Null(UatPageState.Rows(new FakeText("Buy milk")));

    public sealed class FakeText(string text, bool shown = true)
    {
        public bool IsVisible() => shown;

        public string GetText(int timeoutMs) => text;
    }

    public sealed class FakeRow(FakeText title, FakeText status)
    {
        public FakeText Title => title;

        public FakeText Status => status;

        public int Index => 0;

        public FakeList? Parent { get; set; }
    }

    public sealed class FakeList(params FakeRow[] rows)
    {
        public FakeRow[] Rows => rows;

        public IReadOnlyList<FakeRow> Items => rows;

        public bool IsVisible() => true;
    }
}
