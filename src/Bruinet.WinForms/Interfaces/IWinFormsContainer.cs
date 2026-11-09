namespace Bruinet.WinForms.Interfaces;

/// <summary>A scope inside another scope, found by its own root element.</summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
/// <typeparam name="TSelf">The container's own type.</typeparam>
public interface IWinFormsContainer<TParent, TSelf> : IWinFormsScope<TSelf>
    where TParent : IWinFormsScope<TParent>
    where TSelf : IWinFormsContainer<TParent, TSelf>
{
    /// <summary>The scope the container is declared in.</summary>
    TParent Parent { get; }
}
