using System.Runtime.CompilerServices;

// The calls layer (Bruinet.Core.Calls) is internal: a mechanism for the stacks' control bases,
// not an API for tests.
[assembly: InternalsVisibleTo("Bruinet.Maui")]
[assembly: InternalsVisibleTo("Bruinet.Maui.Tests")]
[assembly: InternalsVisibleTo("Bruinet.Html")]
[assembly: InternalsVisibleTo("Bruinet.Html.Tests")]
[assembly: InternalsVisibleTo("Bruinet.Core.Tests")]
[assembly: InternalsVisibleTo("Bruinet.Bdd.Tests")]
[assembly: InternalsVisibleTo("Bruinet.NativeAndroid")]
[assembly: InternalsVisibleTo("Bruinet.NativeAndroid.Tests")]
