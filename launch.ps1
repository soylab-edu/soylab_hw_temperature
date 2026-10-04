param([switch]$Normal)
$ErrorActionPreference = 'Stop'
$releaseVersion = ([xml](Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'SoyTemperature.csproj'))).Project.PropertyGroup.Version
$exePath = Join-Path $PSScriptRoot ('release\' + $releaseVersion + '\SoyTemperature.exe')
if (-not (Test-Path -LiteralPath $exePath)) { throw '먼저 build.ps1을 실행하세요.' }
if ($Normal) { Start-Process -FilePath $exePath }
else { Start-Process -FilePath $exePath -Verb RunAs }
