namespace Bruinet.WinForms.Collections;

/// <summary>
/// A scope that hands out item rows: what an <see cref="ItemObjectBase{TSelf}"/> asks for its root.
/// </summary>
public interface IWinFormsCollection : IWinFormsElementScope, IItemRootSource<IWinFormsElement>;
