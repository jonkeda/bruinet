namespace Bruinet.Stride.Interfaces;

/// <summary>A scope that returns itself, so a control's actions chain back to it.</summary>
/// <typeparam name="TScope">The scope's own type.</typeparam>
public interface IStrideScope<TScope> : IStrideElementScope
    where TScope : IStrideScope<TScope>
{
    /// <summary>The scope itself, typed.</summary>
    TScope Self { get; }
}
