[CmdletBinding()]
param(
    [string]$RepoRoot = ''
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

$ErrorActionPreference = 'Stop'

$installerDir = Join-Path $RepoRoot 'installer'
$binDir       = Join-Path $installerDir 'bin'
$srcCleanDir  = Join-Path $installerDir 'src-clean'
$stripperProj = Join-Path $installerDir 'NSIS\PortableStripper\PortableStripper.csproj'
$stripperBin  = Join-Path $installerDir 'NSIS\PortableStripper\bin'
$uiProj       = Join-Path $RepoRoot 'Ferrolite.UI\Ferrolite.UI.csproj'

Write-Host '=== [1/5] Building PortableStripper ===' -ForegroundColor Cyan
dotnet build $stripperProj -c Release -o $stripperBin --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'PortableStripper build failed.' }
$stripperExe = Join-Path $stripperBin 'PortableStripper.exe'
if (-not (Test-Path $stripperExe)) { throw "Stripper exe not found: $stripperExe" }
Write-Host "OK: $stripperExe"

Write-Host '=== [2/5] Publishing Ferrolite Studio (self-contained win-x64) ===' -ForegroundColor Cyan
if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }
dotnet publish $uiProj -c Release -r win-x64 --self-contained true -o $binDir --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
if (-not (Test-Path (Join-Path $binDir 'Ferrolite.exe'))) { throw 'Ferrolite.exe missing from publish output.' }
Write-Host "OK: $binDir"

Write-Host '=== [3/5] Staging comment-free source (src-clean) ===' -ForegroundColor Cyan
if (Test-Path $srcCleanDir) { Remove-Item $srcCleanDir -Recurse -Force }
New-Item -ItemType Directory -Path $srcCleanDir | Out-Null

$sourceFiles = @()
foreach ($proj in @('Ferrolite.Core','Ferrolite.UI','Ferrolite.Core.Tests')) {
    $projDir = Join-Path $RepoRoot $proj
    $sourceFiles += Get-ChildItem $projDir -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        Where-Object { $_.Extension -in '.cs', '.xaml', '.csproj', '.ico', '.png', '.slnx', '.md', '.txt', '.manifest' }
}
$sourceFiles += Get-Item (Join-Path $RepoRoot 'FerroliteStudio.slnx')
$sourceFiles += Get-Item (Join-Path $RepoRoot 'BUILD.md')
$sourceFiles += Get-Item (Join-Path $RepoRoot 'LICENSE.txt')
$sourceFiles += Get-Item (Join-Path $RepoRoot 'THIRD-PARTY-NOTICES.md')
$sourceFiles += Get-Item (Join-Path $RepoRoot 'SECURITY.md')
$sourceFiles += Get-Item (Join-Path $RepoRoot 'global.json')
$sourceFiles += Get-Item (Join-Path $RepoRoot 'Directory.Build.props')

foreach ($lockProj in @('Ferrolite.Core','Ferrolite.UI','Ferrolite.Core.Tests')) {
    $lockFile = Join-Path $RepoRoot "$lockProj\packages.lock.json"
    if (Test-Path $lockFile) { $sourceFiles += Get-Item $lockFile }
}

foreach ($file in $sourceFiles) {
    $rel = $file.FullName.Substring($RepoRoot.Length + 1)
    $dest = Join-Path $srcCleanDir $rel
    $destDir = Split-Path -Parent $dest
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
    Copy-Item $file.FullName $dest -Force
}

Copy-Item (Join-Path $RepoRoot 'LICENSE.txt') (Join-Path $installerDir 'LICENSE.txt') -Force
Write-Host "OK: staged $installerDir\LICENSE.txt"

& $stripperExe /s $srcCleanDir /e 'cs;xaml;csproj;slnx' /q
if ($LASTEXITCODE -ne 0) { throw 'PortableStripper failed.' }
Write-Host "OK: staged $($sourceFiles.Count) files into $srcCleanDir"

Write-Host '=== [4/5] Verifying NO comments remain in staged source ===' -ForegroundColor Cyan
$violations = Get-ChildItem $srcCleanDir -Recurse -File |
    Where-Object { $_.FullName -match '\\(Ferrolite\.Core|Ferrolite\.UI)\\' } |
    Where-Object { $_.Extension -in '.cs', '.xaml', '.csproj', '.slnx' } |
    Where-Object {
        $text = Get-Content $_.FullName -Raw
        $text -match '(?m)^\s*//' -or $text -match '/\*' -or $text -match '<!--'
    }
if ($violations) {
    Write-Warning 'Comment markers found in:'
    $violations | ForEach-Object { Write-Warning "  $($_.FullName)" }
    throw 'Staged source is not comment-free.'
}
Write-Host 'OK: staged source is comment-free.'

Write-Host '=== [5/5] Staging complete. ===' -ForegroundColor Green
Write-Host ''
Write-Host 'Next: build the installer with makensis:'
Write-Host "  `"$env:ProgramFiles(x86)\NSIS\makensis.exe`" `"$installerDir\NSIS\FerroliteStudio.nsi`""
