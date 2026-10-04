using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace SoyTemperature;

internal sealed class TemperatureTile : Control
{
    private readonly FontFamily _family;
    private readonly Color _accent;
    private readonly string _category;
    private string _value = "—°";
    public string DisplayValue => _value;
    public bool TextFits { get; private set; }

    public TemperatureTile(string category, FontFamily family, Color accent)
    {
        _category = category;
        _family = family;
        _accent = accent;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        AccessibleName = category;
        AccessibleRole = AccessibleRole.StaticText;
    }

    public void UpdateTemperature(float? value)
    {
        var next = value is float number ? $"{number:F1}°" : "—°";
        if (next == _value) return;
        _value = next;
        AccessibleDescription = next;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Surface);
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(Width / 220f, Height / 210f);
        var padding = Math.Max(8, 22 * scale);
        using var categoryFont = new Font(_family, Math.Max(12, 17 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
        using var accentBrush = new SolidBrush(_accent);
        using var whiteBrush = new SolidBrush(Color.FromArgb(245, 244, 249));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        var headingArea = new RectangleF(padding, padding, Width - padding * 2, Height * .2f);
        e.Graphics.DrawString(_category, categoryFont, accentBrush, headingArea, format);
        var valueArea = new RectangleF(padding * .5f, Height * .28f, Width - padding, Height * .55f);
        var size = Math.Max(12, 61 * scale);
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
}
