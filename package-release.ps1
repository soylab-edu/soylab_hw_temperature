param(
    [string]$Executable,
    [string]$Output = 'artifacts\SoyTemperature-win-x64.zip'
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not $Executable) {
    $packageProjectVersion = ([xml](Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'SoyTemperature.csproj'))).Project.PropertyGroup.Version
    $Executable = 'release\' + $packageProjectVersion + '\SoyTemperature.exe'
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageExecutable = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Executable))
$packageOutput = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Output))
if (-not (Test-Path -LiteralPath $packageExecutable -PathType Leaf)) { throw '먼저 build.ps1로 exe를 빌드하세요.' }
$packageVersion = (Get-Item -LiteralPath $packageExecutable).VersionInfo.ProductVersion.Split('+')[0]
if ($packageVersion -notmatch '^\d+\.\d+\.\d+$') { throw '실행 파일의 버전 정보를 확인할 수 없습니다.' }
$packageEntryName = 'release/' + $packageVersion + '/SoyTemperature.exe'
if ($packageOutput -eq $packageExecutable) { throw 'ZIP 경로가 실행 파일과 같습니다.' }
New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($packageOutput)) -Force | Out-Null
$packageStream = [System.IO.File]::Open($packageOutput, [System.IO.FileMode]::Create)
$packageArchive = [System.IO.Compression.ZipArchive]::new($packageStream, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($packageArchive, $packageExecutable,
        $packageEntryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally { $packageArchive.Dispose(); $packageStream.Dispose() }
$packageCheck = [System.IO.Compression.ZipFile]::OpenRead($packageOutput)
try {
    if ($packageCheck.Entries.Count -ne 1 -or $packageCheck.Entries[0].FullName -ne $packageEntryName) {
        throw 'ZIP에 단일 exe 외 파일이 들어 있습니다.'
    }
}
finally { $packageCheck.Dispose() }
Write-Host "배포 ZIP: $packageOutput ($packageEntryName 한 파일)"
