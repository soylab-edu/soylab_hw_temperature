using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace SoyTemperature;

internal sealed class TelemetryPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Accent { get; init; } = Theme.Coral;
    public TelemetryPanel() => DoubleBuffered = true;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Surface);
        using var border = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        var tick = Math.Min(Width / 4, 10 * DeviceDpi / 96);
        using var accent = new Pen(Accent, Math.Max(1, DeviceDpi / 96f));
        e.Graphics.DrawLine(accent, 0, 0, tick, 0);
        e.Graphics.DrawLine(accent, 0, 0, 0, tick);
        e.Graphics.DrawLine(accent, Width - tick - 1, Height - 1, Width - 1, Height - 1);
        e.Graphics.DrawLine(accent, Width - 1, Height - tick - 1, Width - 1, Height - 1);
    }
}

internal sealed class SignalTrace : Control
{
    private readonly Queue<float?> _values = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Accent { get; init; } = Theme.Coral;
    public SignalTrace()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    public void Add(float? value)
    {
        _values.Enqueue(value);
        while (_values.Count > 30) _values.Dequeue();
        if (Visible) Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var inset = Math.Max(2, 2 * DeviceDpi / 96);
        var area = new Rectangle(inset, inset, Math.Max(1, Width - 2 * inset), Math.Max(1, Height - 2 * inset));
        using var grid = new Pen(Color.FromArgb(60, Theme.Muted));
        e.Graphics.DrawLine(grid, area.Left, area.Bottom - 1, area.Right, area.Bottom - 1);
        for (var x = area.Left; x < area.Right; x += Math.Max(4, area.Width / 12))
            e.Graphics.DrawLine(grid, x, area.Bottom - 4, x, area.Bottom);
        var samples = _values.ToArray();
        var valid = samples.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        if (valid.Length == 0) return;
        var minimum = valid.Min() - 2;
        var maximum = valid.Max() + 2;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Accent, Math.Max(1.3f, DeviceDpi / 96f));
        PointF? previous = null;
        for (var i = 0; i < samples.Length; i++)
        {
            if (samples[i] is not float value) { previous = null; continue; }
            var point = new PointF(area.Left + i * area.Width / 29f,
                area.Bottom - 5 - (value - minimum) / (maximum - minimum) * Math.Max(1, area.Height - 10));
            if (previous.HasValue) e.Graphics.DrawLine(pen, previous.Value, point);
            previous = point;
        }
    }
}

internal sealed class HeaderRuler : Control
{
    public HeaderRuler() { DoubleBuffered = true; BackColor = Theme.Background; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        using var accent = new Pen(Theme.Coral);
        var step = Math.Max(4, 4 * DeviceDpi / 96);
        for (var x = 0; x < Width; x += step)
            e.Graphics.DrawLine(x < Width / 9 ? accent : pen, x, x % (step * 5) == 0 ? 0 : Height / 2, x, Height - 1);
    }
}
