namespace Bruinet.Wpf.Interfaces;

/// <summary>A scope that returns itself, so a control's actions chain back to it.</summary>
/// <typeparam name="TScope">The scope's own type.</typeparam>
public interface IWpfScope<TScope> : IWpfElementScope
    where TScope : IWpfScope<TScope>
{
    /// <summary>The scope itself, typed.</summary>
    TScope Self { get; }
}
