namespace Bruinet.Contracts;

/// <summary>
/// The minimum shape of one endpoint response. The DTO the app deserializes into is the
/// contract; this record only adds what the DTO cannot express.
/// </summary>
/// <remarks>
/// Paths use the validator's JSONPath-like notation with collapsed indexes, for example
/// <c>$[*].contact_type_id</c> or <c>$.items[*].type</c>.
/// </remarks>
public sealed record DtoContract(string Endpoint, Type DtoType)
{
    /// <summary>The HTTP method from <see cref="Endpoint"/>, for example <c>GET</c>.</summary>
    public string Method => Endpoint.Split(' ', 2)[0];

    /// <summary>
    /// True when an absolute request path such as <c>/v2/contact/101</c> matches the endpoint
    /// template. An API version segment such as <c>/v2</c> may precede the template, <c>{id}</c>
    /// matches one numeric segment, and any other <c>{name}</c> matches one segment.
    /// </summary>
    public bool MatchesPath(string absolutePath)
    {
        var template = System.Text.RegularExpressions.Regex.Escape(Endpoint.Split(' ', 2)[1].Trim('/'));
        template = template.Replace(@"\{id}", "[0-9]+", StringComparison.Ordinal);
        var pattern = "^(?:/v[0-9]+)?/" + System.Text.RegularExpressions.Regex.Replace(template, @"\\\{[^/]+?}", "[^/]+") + "/?$";
        return System.Text.RegularExpressions.Regex.IsMatch(absolutePath, pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>Fields the app cannot work without. Missing or <c>null</c> is a failure.</summary>
    public IReadOnlySet<string> Required { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Integer fields whose value must be a defined member of an enum.</summary>
    public IReadOnlyDictionary<string, Type> CodedValues { get; init; } = new Dictionary<string, Type>(StringComparer.Ordinal);

    /// <summary>Response fields the app reads without a DTO property, so they are not reported as unknown.</summary>
    public IReadOnlySet<string> KnownUnmapped { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// For a write, the request Android sends. The live run and every hermetic UI test compare the
    /// app's request with it; see <see cref="ExpectedRequestCheck"/>.
    /// </summary>
    public ExpectedRequest? Request { get; init; }
}
