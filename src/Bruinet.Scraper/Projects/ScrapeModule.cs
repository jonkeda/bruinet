namespace Bruinet.Scraper.Projects;

/// <summary>
/// A part of the app with a folder of its own (<c>## Modules</c>): the pages whose key starts with
/// <paramref name="Path"/> are drafted into <paramref name="Pages"/>, in <paramref name="Namespace"/>.
/// </summary>
/// <param name="Path">The start of a page key, such as <c>/invoice</c>.</param>
/// <param name="Pages">The folder in the project, such as <c>Modules/Invoices/Pages</c>.</param>
/// <param name="Namespace">The namespace of the page objects drafted there.</param>
public sealed record ScrapeModule(string Path, string Pages, string Namespace);
