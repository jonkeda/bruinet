namespace Bruinet.WinForms.Interfaces;

/// <summary>A scope that returns itself, so a control's actions chain back to it.</summary>
/// <typeparam name="TScope">The scope's own type.</typeparam>
public interface IWinFormsScope<TScope> : IWinFormsElementScope
    where TScope : IWinFormsScope<TScope>
{
    /// <summary>The scope itself, typed.</summary>
    TScope Self { get; }
}
