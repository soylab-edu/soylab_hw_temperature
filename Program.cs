using System.Security.Principal;
using System.Text.Json;

namespace SoyTemperature;

internal static class Program
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            Environment.ExitCode = SelfTest.Run();
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject.ToString() ?? "Unknown error");
        try { Application.Run(new MainForm(args)); }
        catch (Exception ex) { ShowError(ex); Environment.ExitCode = 1; }
    }

    internal static void LogError(string error)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoyTemperature");
            Directory.CreateDirectory(path);
            File.AppendAllText(Path.Combine(path, "errors.log"), $"{DateTimeOffset.Now:O} {error}{Environment.NewLine}");
        }
        catch { /* Logging must never stop monitoring. */ }
    }

    private static void ShowError(Exception ex)
    {
        LogError(ex.ToString());
        MessageBox.Show($"오류가 발생했습니다.\n{ex.Message}\n\n로그: %LOCALAPPDATA%\\SoyTemperature\\errors.log",
            "SOY Temperature", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
