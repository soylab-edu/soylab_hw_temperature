using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SoyTemperature;

internal static class StartupSettings
{
    internal const string TaskName = "SOY Temperature";

    internal static void Configure(bool remove)
    {
        using var objects = new ComObjects();
        dynamic service = objects.Add(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!);
        service.Connect();
        dynamic folder = objects.Add(service.GetFolder("\\"));
        if (remove)
        {
            try { folder.DeleteTask(TaskName, 0); }
            catch (Exception ex) when ((uint)ex.HResult is 0x80070002 or 0x8004130F) { }
            return;
        }

        var user = WindowsIdentity.GetCurrent().Name;
        var executable = Environment.ProcessPath!;
        dynamic definition = objects.Add(service.NewTask(0));
        dynamic registration = objects.Add(definition.RegistrationInfo);
        registration.Description = "SOY Temperature: CPU/GPU temperatures in the tray at sign-in.";
        dynamic principal = objects.Add(definition.Principal);
        principal.UserId = user;
        principal.LogonType = 3; // TASK_LOGON_INTERACTIVE_TOKEN: no stored password.
        principal.RunLevel = 1; // TASK_RUNLEVEL_HIGHEST: CPU sensor access.
        dynamic settings = objects.Add(definition.Settings);
        settings.Enabled = true;
        settings.StartWhenAvailable = true;
        settings.DisallowStartIfOnBatteries = false;
        settings.StopIfGoingOnBatteries = false;
        settings.ExecutionTimeLimit = "PT0S";
        settings.MultipleInstances = 2; // TASK_INSTANCES_IGNORE_NEW.
        dynamic triggers = objects.Add(definition.Triggers);
        dynamic trigger = objects.Add(triggers.Create(9)); // TASK_TRIGGER_LOGON.
        trigger.UserId = user;
        trigger.Enabled = true;
        dynamic actions = objects.Add(definition.Actions);
        dynamic action = objects.Add(actions.Create(0)); // TASK_ACTION_EXEC.
        action.Path = executable;
        action.Arguments = "--tray";
        action.WorkingDirectory = Path.GetDirectoryName(executable)!;
        objects.Add(folder.RegisterTaskDefinition(TaskName, definition, 6, user, null, 3));

        dynamic shell = objects.Add(Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!);
        foreach (var location in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.Programs })
        {
            var directory = Environment.GetFolderPath(location);
            Directory.CreateDirectory(directory);
            dynamic shortcut = objects.Add(shell.CreateShortcut(Path.Combine(directory, "SOY Temperature.lnk")));
            shortcut.TargetPath = executable;
            shortcut.WorkingDirectory = Path.GetDirectoryName(executable)!;
            shortcut.IconLocation = executable + ",0";
            shortcut.Description = "SOY Temperature - CPU / GPU";
            shortcut.Save();
        }
    }

    private sealed class ComObjects : IDisposable
    {
        private readonly List<object> _values = new();
        public dynamic Add(object value) { _values.Add(value); return value; }
        public void Dispose()
        {
            for (var index = _values.Count - 1; index >= 0; index--)
                if (Marshal.IsComObject(_values[index])) Marshal.ReleaseComObject(_values[index]);
        }
    }
}
