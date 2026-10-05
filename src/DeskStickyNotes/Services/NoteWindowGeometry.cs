using System.Windows;

namespace DeskStickyNotes.Services;

internal enum NoteResizeEdge
{
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

internal static class NoteWindowGeometry
{
    public static Rect Resize(Rect bounds, NoteResizeEdge edge, Vector delta, System.Windows.Size minimum)
    {
        var resizeLeft = edge is NoteResizeEdge.Left or NoteResizeEdge.TopLeft or NoteResizeEdge.BottomLeft;
        var resizeRight = edge is NoteResizeEdge.Right or NoteResizeEdge.TopRight or NoteResizeEdge.BottomRight;
        var resizeTop = edge is NoteResizeEdge.Top or NoteResizeEdge.TopLeft or NoteResizeEdge.TopRight;
        var resizeBottom = edge is NoteResizeEdge.Bottom or NoteResizeEdge.BottomLeft or NoteResizeEdge.BottomRight;
        var width = Math.Max(minimum.Width, bounds.Width + (resizeLeft ? -delta.X : resizeRight ? delta.X : 0));
        var height = Math.Max(minimum.Height, bounds.Height + (resizeTop ? -delta.Y : resizeBottom ? delta.Y : 0));

        return new Rect(
            resizeLeft ? bounds.Right - width : bounds.Left,
            resizeTop ? bounds.Bottom - height : bounds.Top,
            width,
            height);
    }

    public static Rect FitToWorkArea(Rect bounds, Rect workArea)
    {
        var width = Math.Min(bounds.Width, workArea.Width);
        var height = Math.Min(bounds.Height, workArea.Height);
        return new Rect(
            Math.Clamp(bounds.Left, workArea.Left, workArea.Right - width),
            Math.Clamp(bounds.Top, workArea.Top, workArea.Bottom - height),
            width,
            height);
    }
}
