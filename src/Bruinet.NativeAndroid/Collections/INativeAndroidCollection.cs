namespace Bruinet.NativeAndroid.Collections;

/// <summary>
/// A scope that hands out item rows: what an <see cref="ItemObjectBase{TSelf}"/> asks for its root.
/// </summary>
public interface INativeAndroidCollection : INativeAndroidElementScope, IItemRootSource<INativeAndroidElement>;
