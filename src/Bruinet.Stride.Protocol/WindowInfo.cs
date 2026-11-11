namespace Bruinet.Stride.Communication;

/// <summary>
/// Information about the game window position and size.
/// </summary>
public class WindowInfo
{
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public int UiResolutionX { get; set; } = 1280;
    public int UiResolutionY { get; set; } = 720;
}
