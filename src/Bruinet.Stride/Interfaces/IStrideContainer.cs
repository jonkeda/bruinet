namespace Bruinet.Stride.Interfaces;

/// <summary>A scope inside another scope, found by its own root element.</summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TSelf">The container's own type.</typeparam>
public interface IStrideContainer<TParent, TSelf> : IStrideScope<TSelf>
    where TParent : IStrideScope<TParent>
    where TSelf : IStrideContainer<TParent, TSelf>
{
    /// <summary>The scope the container is declared in.</summary>
    TParent Parent { get; }
}
