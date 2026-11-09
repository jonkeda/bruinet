namespace Bruinet.Wpf.Interfaces;

/// <summary>A scope inside another scope, found by its own root element.</summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TSelf">The container's own type.</typeparam>
public interface IWpfContainer<TParent, TSelf> : IWpfScope<TSelf>
    where TParent : IWpfScope<TParent>
    where TSelf : IWpfContainer<TParent, TSelf>
{
    /// <summary>The scope the container is declared in.</summary>
    TParent Parent { get; }
}
