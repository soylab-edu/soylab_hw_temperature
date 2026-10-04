param(
    [string]$Executable = (Join-Path $PSScriptRoot 'release\1.0.0\SoyTemperature.exe'),
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'
$startupExecutable = [System.IO.Path]::GetFullPath($Executable)
$startupTaskName = 'SOY Temperature'
$startupIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$startupAdmin = ([Security.Principal.WindowsPrincipal]$startupIdentity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $startupAdmin) {
    $startupArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'),
        '-Executable', ('"' + $startupExecutable + '"'))
    if ($Remove) { $startupArguments += '-Remove' }
    $startupSetup = Start-Process powershell.exe -ArgumentList $startupArguments -Verb RunAs -WindowStyle Hidden -Wait -PassThru
    if ($startupSetup.ExitCode -ne 0) { throw 'Startup configuration failed.' }
    exit 0
}
if ($Remove) {
    $startupExisting = Get-ScheduledTask -TaskName $startupTaskName -ErrorAction SilentlyContinue
    if ($startupExisting) { Unregister-ScheduledTask -TaskName $startupTaskName -Confirm:$false }
    Write-Host 'Login startup disabled. Shortcuts are preserved.'
    exit 0
}
if (-not (Test-Path -LiteralPath $startupExecutable -PathType Leaf)) { throw 'Executable not found. Run build.ps1 first.' }
$startupAction = New-ScheduledTaskAction -Execute $startupExecutable -Argument '--tray' -WorkingDirectory (Split-Path -Parent $startupExecutable)
$startupTrigger = New-ScheduledTaskTrigger -AtLogOn -User $startupIdentity.Name
$startupPrincipal = New-ScheduledTaskPrincipal -UserId $startupIdentity.Name -LogonType Interactive -RunLevel Highest
$startupSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
    -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero)
Register-ScheduledTask -TaskName $startupTaskName -Action $startupAction -Trigger $startupTrigger `
    -Principal $startupPrincipal -Settings $startupSettings -Description 'SOY Temperature: CPU/GPU temperatures in the tray at sign-in.' -Force | Out-Null
$startupShell = New-Object -ComObject WScript.Shell
foreach ($startupShortcutFolder in @([Environment]::GetFolderPath('DesktopDirectory'), [Environment]::GetFolderPath('Programs'))) {
    New-Item -ItemType Directory -Path $startupShortcutFolder -Force | Out-Null
    $startupShortcut = $startupShell.CreateShortcut((Join-Path $startupShortcutFolder 'SOY Temperature.lnk'))
    $startupShortcut.TargetPath = $startupExecutable
    $startupShortcut.WorkingDirectory = Split-Path -Parent $startupExecutable
    $startupShortcut.IconLocation = $startupExecutable + ',0'
    $startupShortcut.Description = 'SOY Temperature - CPU / GPU'
    $startupShortcut.Save()
}
Write-Host 'Login startup registered (--tray, interactive, highest privileges). Desktop and Start menu shortcuts created.'
