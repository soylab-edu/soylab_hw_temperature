using System.Drawing.Drawing2D;

namespace SoyTemperature;

internal sealed class RoundedPanel : Panel
{
    public RoundedPanel() => DoubleBuffered = true;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var diameter = Math.Min(Width, Math.Min(Height, 30 * DeviceDpi / 96));
        if (diameter <= 0) return;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter - 1, 0, diameter, diameter, 270, 90);
        path.AddArc(Width - diameter - 1, Height - diameter - 1, diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var fill = new SolidBrush(Theme.Surface);
        e.Graphics.FillPath(fill, path);
        using var border = new Pen(Theme.Border);
        e.Graphics.DrawPath(border, path);
    }
}
