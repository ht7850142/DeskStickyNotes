using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DeskStickyNotes.Services;

internal static class TrayMenuVisuals
{
    internal enum Glyph { New, Notes, Show, Hide, Settings, Exit }

    public static Forms.ToolStripRenderer CreateRenderer() => Forms.SystemInformation.HighContrast
        ? new Forms.ToolStripSystemRenderer()
        : new Renderer();

    public static Drawing.Bitmap CreateGlyph(Glyph glyph)
    {
        // Render at display density, while ToolStrip owns the logical image size.
        var scale = Math.Max(1f, Forms.SystemInformation.SmallIconSize.Width / 16f);
        var bitmap = new Drawing.Bitmap((int)(16 * scale), (int)(16 * scale));
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.ScaleTransform(scale, scale);
        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var color = Forms.SystemInformation.HighContrast ? Drawing.SystemColors.MenuText : Drawing.Color.FromArgb(52, 93, 80);
        using var pen = new Drawing.Pen(color, 1.5f)
        {
            StartCap = Drawing.Drawing2D.LineCap.Round,
            EndCap = Drawing.Drawing2D.LineCap.Round,
            LineJoin = Drawing.Drawing2D.LineJoin.Round
        };
        switch (glyph)
        {
            case Glyph.New:
                graphics.DrawLine(pen, 8, 3, 8, 13); graphics.DrawLine(pen, 3, 8, 13, 8);
                break;
            case Glyph.Notes:
                graphics.DrawRectangle(pen, 3, 2, 10, 11); graphics.DrawLine(pen, 6, 6, 10, 6);
                graphics.DrawLine(pen, 6, 9, 9, 9); graphics.DrawLine(pen, 1, 5, 1, 15); graphics.DrawLine(pen, 1, 15, 10, 15);
                break;
            case Glyph.Show:
                graphics.DrawEllipse(pen, 2, 4, 12, 8); graphics.DrawEllipse(pen, 6, 6, 4, 4);
                break;
            case Glyph.Hide:
                graphics.DrawArc(pen, 2, 4, 12, 8, 15, 150); graphics.DrawArc(pen, 2, 4, 12, 8, 195, 150);
                graphics.DrawLine(pen, 2, 14, 14, 2);
                break;
            case Glyph.Settings:
                graphics.DrawEllipse(pen, 3, 3, 10, 10); graphics.DrawEllipse(pen, 6, 6, 4, 4);
                for (var angle = 0; angle < 360; angle += 60)
                {
                    var radians = angle * Math.PI / 180;
                    graphics.DrawLine(pen, 8 + (float)Math.Cos(radians) * 5, 8 + (float)Math.Sin(radians) * 5,
                        8 + (float)Math.Cos(radians) * 7, 8 + (float)Math.Sin(radians) * 7);
                }
                break;
            case Glyph.Exit:
                graphics.DrawLine(pen, 8, 2, 3, 2); graphics.DrawLine(pen, 3, 2, 3, 14); graphics.DrawLine(pen, 3, 14, 8, 14);
                graphics.DrawLine(pen, 7, 8, 14, 8);
                graphics.DrawLines(pen, new Drawing.Point[] { new(11, 5), new(14, 8), new(11, 11) });
                break;
        }
        return bitmap;
    }

    private sealed class Colors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Surface = Drawing.Color.FromArgb(252, 252, 248);
        private static readonly Drawing.Color Selection = Drawing.Color.FromArgb(228, 237, 229);
        public override Drawing.Color ToolStripDropDownBackground => Surface;
        public override Drawing.Color ImageMarginGradientBegin => Surface;
        public override Drawing.Color ImageMarginGradientMiddle => Surface;
        public override Drawing.Color ImageMarginGradientEnd => Surface;
        public override Drawing.Color MenuItemSelected => Selection;
        public override Drawing.Color MenuItemSelectedGradientBegin => Selection;
        public override Drawing.Color MenuItemSelectedGradientEnd => Selection;
        public override Drawing.Color MenuItemPressedGradientBegin => Selection;
        public override Drawing.Color MenuItemPressedGradientMiddle => Selection;
        public override Drawing.Color MenuItemPressedGradientEnd => Selection;
        public override Drawing.Color MenuItemBorder => Selection;
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(215, 224, 213);
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(223, 228, 219);
        public override Drawing.Color SeparatorLight => Surface;
        public override Drawing.Color CheckBackground => Selection;
        public override Drawing.Color CheckSelectedBackground => Selection;
        public override Drawing.Color CheckPressedBackground => Selection;
    }

    private sealed class Renderer() : Forms.ToolStripProfessionalRenderer(new Colors())
    {
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled || e.Item is Forms.ToolStripLabel
                ? Drawing.Color.FromArgb(41, 54, 47) : Drawing.Color.FromArgb(116, 126, 114);
            base.OnRenderItemText(e);
        }
    }
}
