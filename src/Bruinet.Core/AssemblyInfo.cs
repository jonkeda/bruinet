using System.Runtime.CompilerServices;

// The calls layer (Bruinet.Core.Calls) is public: every stack writes its control bases against
// Core's published surface (AD-010). Only Core's own test projects see its internals.
[assembly: InternalsVisibleTo("Bruinet.Maui.Tests")]
[assembly: InternalsVisibleTo("Bruinet.Html.Tests")]
[assembly: InternalsVisibleTo("Bruinet.Core.Tests")]
[assembly: InternalsVisibleTo("Bruinet.Bdd.Tests")]
[assembly: InternalsVisibleTo("Bruinet.NativeAndroid.Tests")]
