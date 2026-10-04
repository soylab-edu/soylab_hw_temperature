using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace SoyTemperature;

internal sealed class AppFonts : IDisposable
{
    private readonly PrivateFontCollection _fonts = new();
    private readonly IntPtr _memory;
    private bool _disposed;
    public FontFamily Family => _fonts.Families[0];

    public AppFonts()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("SoyTemperature.UbuntuBold")
            ?? throw new InvalidOperationException("Ubuntu 글꼴 리소스가 없습니다.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        _memory = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, _memory, bytes.Length);
        _fonts.AddMemoryFont(_memory, bytes.Length);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fonts.Dispose();
        Marshal.FreeHGlobal(_memory);
    }
}
