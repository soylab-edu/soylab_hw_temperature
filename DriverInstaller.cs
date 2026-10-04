using System.Diagnostics;
using System.Security.Cryptography;

namespace SoyTemperature;

internal static class DriverInstaller
{
    private const string DownloadUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    private const string Sha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";

    public static async Task InstallAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SoyTemperature", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "PawnIO_setup.exe");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var bytes = await client.GetByteArrayAsync(DownloadUrl, cancellationToken);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != Sha256)
                throw new InvalidDataException("공식 설치 파일의 SHA-256이 일치하지 않습니다.");
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            using var installer = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true, Verb = "runas", Arguments = "-install -silent", WindowStyle = ProcessWindowStyle.Hidden
            }) ?? throw new InvalidOperationException("설치 프로그램을 실행하지 못했습니다.");
            // Once started, let the installer finish before cleaning its file or closing the app.
            await installer.WaitForExitAsync();
            if (installer.ExitCode != 0) throw new InvalidOperationException($"설치 종료 코드: {installer.ExitCode}");
        }
        finally
        {
            try { File.Delete(path); Directory.Delete(directory); }
            catch (IOException) { }
        }
    }
}
