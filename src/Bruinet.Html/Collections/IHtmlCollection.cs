using Bruinet.Core.Collections;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Collections;

/// <summary>
/// A scope that hands out item rows: what an <see cref="ItemObjectBase{TSelf}"/> asks for its root.
/// </summary>
public interface IHtmlCollection : IHtmlElementScope, IItemRootSource<IHtmlElement>;
