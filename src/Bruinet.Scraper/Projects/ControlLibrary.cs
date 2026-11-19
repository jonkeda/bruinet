namespace Bruinet.Scraper.Projects;

/// <summary>A control library outside the project that patterns whose classes start with <paramref name="ClassPrefix"/> belong in.</summary>
/// <param name="ClassPrefix">The start of a class, such as <c>v-</c> for Vuetify.</param>
/// <param name="Library">The library, such as <c>Bruinet.Web.Vuetify2</c>.</param>
public sealed record ControlLibrary(string ClassPrefix, string Library)
{
    /// <summary>
    /// The library's file of signs (<see cref="ControlSign.FileName"/>), as a path from the project's folder; empty
    /// for none. It says how each of the library's controls is recognized on a page.
    /// </summary>
    public string Signs { get; init; } = "";
}
