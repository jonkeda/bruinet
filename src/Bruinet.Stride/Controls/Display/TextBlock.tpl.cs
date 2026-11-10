using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls.Display;

/// <summary>
/// A text block: its text, read from the game.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TextBlock<TScope> : ViewBase<TScope>, ITextControlObject<TScope>
    where TScope : IStrideScope<TScope>
{
    public TextBlock(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TextBlock(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
