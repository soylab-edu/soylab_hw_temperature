param([switch]$SkipInstall)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$sdkPath = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdkPath)) {
    $installedSdk = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($installedSdk) { $sdkPath = $installedSdk.Source }
    elseif (-not $SkipInstall) {
        New-Item -ItemType Directory -Path '.tools' -Force | Out-Null
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '.tools\dotnet-install.ps1' -UseBasicParsing
        & '.\.tools\dotnet-install.ps1' -Channel '10.0' -InstallDir (Join-Path $PSScriptRoot '.tools\dotnet') -NoPath
    }
    else { throw '.NET 10 SDK가 없습니다. -SkipInstall 없이 실행하세요.' }
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
& $sdkPath publish '.\SoyTemperature.csproj' -c Release -r win-x64 --self-contained true -o '.\release\1.0.0'
if ($LASTEXITCODE -ne 0) { throw '빌드 실패' }
Write-Host "실행 파일: $PSScriptRoot\release\1.0.0\SoyTemperature.exe"
