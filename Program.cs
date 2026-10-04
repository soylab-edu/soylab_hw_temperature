using System.Security.Principal;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SoyTemperature;

internal static class Program
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);
    private static readonly string UserKey = WindowsIdentity.GetCurrent().User!.Value;
    internal static readonly uint RestoreWindowMessage = RegisterWindowMessage("SoyTemperature.Restore." + UserKey);
    internal static bool RestartRequested { get; set; }

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            Environment.ExitCode = SelfTest.Run();
            return;
        }
        if (args.Contains("--install-startup") || args.Contains("--remove-startup"))
        {
            try { ConfigureStartup(args.Contains("--remove-startup")); }
            catch (Exception ex) { ShowError(ex); Environment.ExitCode = 1; }
            return;
        }
        // Verification uses a separate instance so it cannot disturb the user's running monitor.
        var instanceName = "Local\\SoyTemperature." + UserKey
            + (args.Contains("--verify") ? ".Verification." + Environment.ProcessId : "");
        using var instance = new Mutex(true, instanceName, out var firstInstance);
        if (!firstInstance)
        {
            if (!args.Contains("--tray")) PostMessage(new IntPtr(0xffff), RestoreWindowMessage, IntPtr.Zero, IntPtr.Zero);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject.ToString() ?? "Unknown error");
        try { Application.Run(new MainForm(args)); }
        catch (Exception ex) { ShowError(ex); Environment.ExitCode = 1; }
        finally { instance.ReleaseMutex(); }
        if (RestartRequested) Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
    }

    internal static void ConfigureStartup(bool remove)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoyTemperature");
        Directory.CreateDirectory(folder);
        var script = Path.Combine(folder, "setup-startup.ps1");
        using (var source = typeof(Program).Assembly.GetManifestResourceStream("SoyTemperature.StartupSetup")!)
        using (var destination = File.Create(script)) source.CopyTo(destination);
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
            "-Executable", Environment.ProcessPath! }) start.ArgumentList.Add(argument);
        if (remove) start.ArgumentList.Add("-Remove");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(output, error);
        if (process.ExitCode != 0) throw new InvalidOperationException(error.Result + output.Result);
    }

    internal static bool StartupEnabled
    {
        get
        {
            object? service = null, folder = null, task = null, definition = null, actions = null, action = null;
            try
            {
                service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
                ((dynamic)service!).Connect();
                folder = ((dynamic)service).GetFolder("\\");
                task = ((dynamic)folder).GetTask("SOY Temperature");
                definition = ((dynamic)task).Definition;
                actions = ((dynamic)definition).Actions;
                action = ((dynamic)actions).Item(1);
                return ((dynamic)task).Enabled && string.Equals((string)((dynamic)action).Path,
                    Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) && (string)((dynamic)action).Arguments == "--tray";
            }
            catch (Exception ex) when ((uint)ex.HResult is 0x80070002 or 0x8004130F) { return false; }
            finally
            {
                foreach (var value in new[] { action, actions, definition, task, folder, service })
                    if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
            }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

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
