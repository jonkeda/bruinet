using Bruinet.Html.Interfaces;

namespace Bruinet.Html;

/// <summary>
/// Base class for HTML page objects, containers, rows and controls: Core's
/// <see cref="Bruinet.Core.Scopes.ObjectBase{TContext}"/> on the HTML context.
/// </summary>
public abstract class ObjectBase : Bruinet.Core.Scopes.ObjectBase<IHtmlTestContext>
{
}
