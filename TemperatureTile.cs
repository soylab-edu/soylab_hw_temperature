using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace SoyTemperature;

internal sealed class TemperatureTile : Control
{
    private readonly FontFamily _family;
    private readonly Color _accent;
    private readonly string _category;
    private readonly Color _surface;
    private readonly Queue<float?> _history = new();
    private string _value = "—°";
    public string DisplayValue => _value;
    public bool TextFits { get; private set; }
    public int GraphSamples => _history.Count;

    public TemperatureTile(string category, FontFamily family, Color accent)
    {
        _category = category;
        _family = family;
        _accent = accent;
        _surface = ColorTranslator.FromHtml(category == "CPU" ? "#2D222D" : "#242239");
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleName = category;
        AccessibleRole = AccessibleRole.StaticText;
    }

    public void UpdateTemperature(float? value, bool repaint = true)
    {
        _history.Enqueue(value);
        while (_history.Count > 30) _history.Dequeue();
        var next = value is float number ? $"{number:F1}°" : "—°";
        _value = next;
        AccessibleDescription = next;
        if (repaint) Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(Width / 230f, Height / 200f);
        var padding = Math.Max(8, 26 * scale);
        using var cardShape = RoundedShape.Create(new RectangleF(0, 0, Width - 1, Height - 1), 22 * scale);
        using var cardFill = new SolidBrush(_surface);
        e.Graphics.FillPath(cardFill, cardShape);
        using var cardBorder = new Pen(Color.FromArgb(30, _accent), Math.Max(1, scale * .5f));
        e.Graphics.DrawPath(cardBorder, cardShape);
        using var categoryFont = new Font(_family, Math.Max(12, 16 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
        using var accentBrush = new SolidBrush(_accent);
        using var whiteBrush = new SolidBrush(Color.FromArgb(245, 244, 249));
        using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        var headingArea = new RectangleF(padding + 15 * scale, padding, Width - padding * 2 - 15 * scale, Height * .16f);
        e.Graphics.FillEllipse(accentBrush, padding, headingArea.Top + headingArea.Height / 2 - 3 * scale, 6 * scale, 6 * scale);
        e.Graphics.DrawString(_category, categoryFont, accentBrush, headingArea, format);
        DrawTrend(e.Graphics, new RectangleF(padding, Height * .31f, Width - padding * 2, Height * .15f), scale);
        var valueArea = new RectangleF(padding * .8f, Height * .48f, Width - padding * 1.6f, Height * .4f);
        var size = Math.Max(12, 63 * scale);
        Font? valueFont = null;
        SizeF measured;
        do
        {
            valueFont?.Dispose();
            valueFont = new Font(_family, size, FontStyle.Bold, GraphicsUnit.Pixel);
            measured = e.Graphics.MeasureString(_value, valueFont, int.MaxValue, format);
            size -= 1;
        } while ((measured.Width > valueArea.Width || measured.Height > valueArea.Height) && size >= 10);
        using (valueFont)
            e.Graphics.DrawString(_value, valueFont, whiteBrush, valueArea, format);
        var headingSize = e.Graphics.MeasureString(_category, categoryFont, int.MaxValue, format);
        TextFits = measured.Width <= valueArea.Width && measured.Height <= valueArea.Height
            && headingSize.Width <= headingArea.Width && headingSize.Height <= headingArea.Height;
    }

    private void DrawTrend(Graphics graphics, RectangleF area, float scale)
    {
        var samples = _history.ToArray();
        var valid = samples.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (valid.Length == 0) return;
        var minimum = valid.Min() - 2;
        var maximum = valid.Max() + 2;
        using var pen = new Pen(Color.FromArgb(155, _accent), Math.Max(1.2f, scale * 1.35f))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(Color.FromArgb(12, _accent));
        var segment = new List<PointF>();
        for (var index = 0; index < samples.Length; index++)
        {
            if (samples[index] is not float value) { DrawSegment(); segment.Clear(); continue; }
            segment.Add(new PointF(area.Left + (30 - samples.Length + index) * area.Width / 29f,
                area.Bottom - (value - minimum) / (maximum - minimum) * area.Height));
        }
        DrawSegment();

        void DrawSegment()
        {
            if (segment.Count < 2) return;
            var points = segment.ToArray();
            var polygon = points.Concat([new PointF(points[^1].X, area.Bottom), new PointF(points[0].X, area.Bottom)]).ToArray();
            graphics.FillPolygon(fill, polygon);
            graphics.DrawLines(pen, points);
        }
    }
}
