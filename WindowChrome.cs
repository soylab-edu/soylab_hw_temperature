using System.Drawing.Drawing2D;

namespace SoyTemperature;

internal enum WindowAction { Close, Minimize, Website }

internal sealed class TrafficLightButton : Button
{
    private readonly WindowAction _action;
    private bool _hovered;

    public TrafficLightButton(WindowAction action)
    {
        _action = action;
        AccessibleName = action switch { WindowAction.Close => "종료", WindowAction.Minimize => "트레이로 내리기", _ => "SOYLAB 웹사이트 열기" };
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(Width, Height) / 24f;
        e.Graphics.ScaleTransform(scale, scale);
        var color = _action switch
        {
            WindowAction.Close => ColorTranslator.FromHtml("#FF5F57"),
            WindowAction.Minimize => ColorTranslator.FromHtml("#FEBC2E"),
            _ => ColorTranslator.FromHtml("#28C840")
        };
        using var fill = new SolidBrush(color);
        e.Graphics.FillEllipse(fill, 5, 5, 14, 14);
        if (_hovered || Focused)
        {
            using var pen = new Pen(Color.FromArgb(155, 25, 22, 31), 1.35f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (_action == WindowAction.Close)
            {
                e.Graphics.DrawLine(pen, 9.5f, 9.5f, 14.5f, 14.5f);
                e.Graphics.DrawLine(pen, 9.5f, 14.5f, 14.5f, 9.5f);
            }
            else if (_action == WindowAction.Minimize) e.Graphics.DrawLine(pen, 9, 12, 15, 12);
            else
            {
                e.Graphics.DrawLine(pen, 9.5f, 14.5f, 14.5f, 9.5f);
                e.Graphics.DrawLines(pen, [new PointF(10.5f, 9.5f), new PointF(14.5f, 9.5f), new PointF(14.5f, 13.5f)]);
            }
        }
    }
}

internal sealed class WindowFrame : TableLayoutPanel
{
    public WindowFrame() => DoubleBuffered = true;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var width = 3.5f * scale;
        var inset = width / 2 + .5f;
        using var outline = RoundedShape.Create(new RectangleF(inset, inset, Width - inset * 2, Height - inset * 2), 26 * scale - inset);
        using var pen = new Pen(Theme.FrameEdge, width);
        e.Graphics.DrawPath(pen, outline);
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
