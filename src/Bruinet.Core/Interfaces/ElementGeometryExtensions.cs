using System.Drawing;

namespace Bruinet.Core.Interfaces;

/// <summary>
/// Geometry every stack uses.
/// </summary>
public static class ElementGeometryExtensions
{
    /// <summary>
    /// The centre point of a rectangle.
    /// </summary>
    public static Point CenterOf(Rectangle rectangle)
        => new(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2);
}
