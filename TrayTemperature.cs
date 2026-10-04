using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SoyTemperature;

internal sealed class TrayTemperature : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly string _category;
    private readonly Color _color;
    private Icon? _icon;
    private string? _lastNumber;
    public bool Visible { get => _notify.Visible; set => _notify.Visible = value; }
    public string Text => _notify.Text;

    public TrayTemperature(string category, Color color, ContextMenuStrip menu, Action restore)
    {
        _category = category;
        _color = color;
        _notify = new NotifyIcon { ContextMenuStrip = menu };
        _notify.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) restore(); };
        Update(null);
    }

    public void Update(float? value)
    {
        var number = value is float temperature ? Math.Round(temperature).ToString("0") : "--";
        _notify.Text = value is float current
            ? $"SOY {_category} {current:F1} °C · 클릭하면 창 열기"
            : $"SOY {_category} 측정값 없음 · 권한 / 드라이버 확인";
        if (number == _lastNumber) return;
        _lastNumber = number;
        var next = RenderIcon(number, _category == "CPU" ? "C" : "G", _color);
        var previous = _icon;
        _icon = next;
        _notify.Icon = next;
        previous?.Dispose();
    }

    public void SavePreview(string path)
    {
        using var bitmap = _icon!.ToBitmap();
        bitmap.Save(path, ImageFormat.Png);
    }

    private static Icon RenderIcon(string number, string label, Color color)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var background = new SolidBrush(Color.FromArgb(235, 19, 27, 40));
            graphics.FillRectangle(background, 0, 0, 32, 32);
            using var brush = new SolidBrush(color);
            using var font = new Font("Segoe UI", number.Length > 2 ? 16 : 19, FontStyle.Bold, GraphicsUnit.Pixel);
            using var small = new Font("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(number, font, brush, new RectangleF(-1, -2, 34, 25), format);
            graphics.DrawString(label, small, brush, new RectangleF(0, 21, 32, 11), format);
        }
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _icon?.Dispose();
    }
}
