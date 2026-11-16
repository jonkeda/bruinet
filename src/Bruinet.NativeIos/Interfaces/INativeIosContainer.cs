namespace Bruinet.NativeIos.Interfaces;

/// <summary>A scope inside another scope, found by its own root view.</summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TSelf">The container's own type.</typeparam>
public interface INativeIosContainer<TParent, TSelf> : INativeIosScope<TSelf>
    where TParent : INativeIosScope<TParent>
    where TSelf : INativeIosContainer<TParent, TSelf>
{
    /// <summary>The scope the container is declared in.</summary>
    TParent Parent { get; }
}
