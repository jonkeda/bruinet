using Bruinet.Html.Controls.Display;

namespace Bruinet.Sql.Spike.Tests.PageObjects;

/// <summary>The list of places (/), filtered by kind.</summary>
public sealed class PlacesPage(IHtmlTestContext context) : BlazorPageObjectBase<PlacesPage>(context)
{
    // The count shows once the first load is back, not with the heading.
    public override bool IsLoaded() => base.IsLoaded() && Count.Exists();

    public LabelControl<PlacesPage> Heading => new(this, Locator.ByDataTestId("places-heading"));

    public CheckBoxControl<PlacesPage> Cities => new(this, Locator.ByDataTestId("kind-city"));

    public CheckBoxControl<PlacesPage> Towns => new(this, Locator.ByDataTestId("kind-town"));

    public LabelControl<PlacesPage> Count => new(this, Locator.ByDataTestId("place-count"));

    public LabelControl<PlacesPage> Names => new(this, Locator.ByDataTestId("place-names"));

    public LabelControl<PlacesPage> KindCounts => new(this, Locator.ByDataTestId("kind-counts"));

    public ButtonControl<PlacesPage> AddButton => new(this, Locator.ByDataTestId("add-place"));

    public NewPlacePage Add()
    {
        AddButton.Click();
        var page = new NewPlacePage(Context);
        page.AssertLoaded(true);
        return page;
    }
}

/// <summary>The form for a new place (/places/new).</summary>
public sealed class NewPlacePage(IHtmlTestContext context) : BlazorPageObjectBase<NewPlacePage>(context)
{
    public override bool IsLoaded() => base.IsLoaded() && Heading.Exists();

    public LabelControl<NewPlacePage> Heading => new(this, Locator.ByDataTestId("new-place-heading"));

    public TextInputControl<NewPlacePage> PlaceName => new(this, Locator.ByDataTestId("name"));

    public SelectControl<NewPlacePage> Kind => new(this, Locator.ByDataTestId("kind"));

    public TextInputControl<NewPlacePage> Latitude => new(this, Locator.ByDataTestId("latitude"));

    public TextInputControl<NewPlacePage> Longitude => new(this, Locator.ByDataTestId("longitude"));

    public TextInputControl<NewPlacePage> Population => new(this, Locator.ByDataTestId("population"));

    public LabelControl<NewPlacePage> Error => new(this, Locator.ByDataTestId("error"));

    public ButtonControl<NewPlacePage> SaveButton => new(this, Locator.ByDataTestId("save"));

    public NewPlacePage Enter(string name, string kind, string latitude, string longitude, string population)
    {
        PlaceName.SetText(name);
        Kind.SelectByText(kind);
        Latitude.SetText(latitude);
        Longitude.SetText(longitude);
        Population.SetText(population);
        return this;
    }

    public PlacePage Save()
    {
        SaveButton.Click();
        var page = new PlacePage(Context);
        page.AssertLoaded(true);
        return page;
    }

    public NewPlacePage SaveExpectingError()
    {
        SaveButton.Click();
        Error.AssertExists(true);
        return this;
    }
}

/// <summary>One place (/places/{id}), with its distance from the origin.</summary>
public sealed class PlacePage(IHtmlTestContext context) : BlazorPageObjectBase<PlacePage>(context)
{
    public override bool IsLoaded() => base.IsLoaded() && PlaceName.Exists();

    public LabelControl<PlacePage> PlaceName => new(this, Locator.ByDataTestId("place-name"));

    public LabelControl<PlacePage> Kind => new(this, Locator.ByDataTestId("place-kind"));

    public LabelControl<PlacePage> Population => new(this, Locator.ByDataTestId("place-population"));

    public LabelControl<PlacePage> Distance => new(this, Locator.ByDataTestId("place-distance"));

    public LabelControl<PlacePage> Visits => new(this, Locator.ByDataTestId("place-visits"));
}
