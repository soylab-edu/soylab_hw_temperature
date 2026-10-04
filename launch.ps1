param([switch]$Normal)
$ErrorActionPreference = 'Stop'
$exePath = Join-Path $PSScriptRoot 'dist\SoyTemperature.exe'
if (-not (Test-Path -LiteralPath $exePath)) { throw '먼저 build.ps1을 실행하세요.' }
if ($Normal) { Start-Process -FilePath $exePath }
else { Start-Process -FilePath $exePath -Verb RunAs }
