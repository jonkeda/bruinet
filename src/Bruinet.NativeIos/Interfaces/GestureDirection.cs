namespace Bruinet.NativeIos.Interfaces;

/// <summary>Which way a swipe or a scroll moves the finger.</summary>
public enum GestureDirection
{
    /// <summary>Upwards: content moves up, what is below comes into view.</summary>
    Up,

    /// <summary>Downwards: content moves down, what is above comes into view.</summary>
    Down,

    /// <summary>To the left: what is to the right comes into view.</summary>
    Left,

    /// <summary>To the right: what is to the left comes into view.</summary>
    Right,
}
