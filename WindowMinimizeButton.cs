using System.Drawing.Drawing2D;

namespace SoyTemperature;

internal sealed class WindowMinimizeButton : Button
{
    private bool _hovered;
    public WindowMinimizeButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        AccessibleName = "트레이로 내리기";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(Width, Height) / 48f;
        e.Graphics.ScaleTransform(scale, scale);
        using var shape = RoundedShape.Create(new RectangleF(.5f, .5f, 47, 47), 14);
        using var fill = new SolidBrush(_hovered ? Theme.Button : Color.FromArgb(47, 41, 61));
        e.Graphics.FillPath(fill, shape);
        using var pen = new Pen(Color.FromArgb(225, 217, 243), 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var window = RoundedShape.Create(new RectangleF(9, 11, 22, 16), 2);
        e.Graphics.DrawPath(pen, window);
        e.Graphics.DrawLine(pen, 9, 16, 31, 16);
        using var mask = new SolidBrush(_hovered ? Theme.Button : Color.FromArgb(47, 41, 61));
        e.Graphics.FillRectangle(mask, 22, 21, 13, 10);
        e.Graphics.DrawLine(pen, 22, 22, 31, 31);
        e.Graphics.DrawLines(pen, [new PointF(25.5f, 31), new PointF(31, 31), new PointF(31, 25.5f)]);
        using var smallWindow = RoundedShape.Create(new RectangleF(33, 33, 8, 6), 1);
        e.Graphics.DrawPath(pen, smallWindow);
        if (Focused) { using var focusPen = new Pen(Theme.Lavender, 1); e.Graphics.DrawPath(focusPen, shape); }
    }
}

internal static class RoundedShape
{
    public static GraphicsPath Create(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
