[CmdletBinding()]
param(
    [string]$Version = '0.3.0',
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use the major.minor.patch format: $Version"
}

$repoRoot = $PSScriptRoot
$distRoot = Join-Path $repoRoot 'dist'
$packageName = "TerminalDropPath-v$Version-windows"
$stagingPath = Join-Path $distRoot $packageName
$archivePath = Join-Path $distRoot "$packageName.zip"
$checksumPath = "$archivePath.sha256"
$smokeTestPath = Join-Path $distRoot ".package-smoke-v$Version"
$executablePath = Join-Path $repoRoot 'bin\TerminalDropPath.exe'

function Assert-RepositoryChildPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolvedRoot = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $resolvedPath"
    }
}

if (-not $SkipBuild) {
    & (Join-Path $repoRoot 'build.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Built executable was not found: $executablePath"
}

$binaryVersion = (Get-Item -LiteralPath $executablePath).VersionInfo.FileVersion
if ($binaryVersion -ne "$Version.0") {
    throw "Binary version $binaryVersion does not match package version $Version."
}

Assert-RepositoryChildPath -Path $stagingPath
Assert-RepositoryChildPath -Path $archivePath
Assert-RepositoryChildPath -Path $checksumPath
Assert-RepositoryChildPath -Path $smokeTestPath
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

if (Test-Path -LiteralPath $stagingPath) {
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}
if (Test-Path -LiteralPath $smokeTestPath) {
    Remove-Item -LiteralPath $smokeTestPath -Recurse -Force
}

$packageBinPath = Join-Path $stagingPath 'bin'
New-Item -ItemType Directory -Path $packageBinPath -Force | Out-Null

Copy-Item -LiteralPath $executablePath -Destination $packageBinPath
foreach ($fileName in @('DropPath.cmd', 'DropPath.ps1', 'LICENSE', 'README.md', 'README.en.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $fileName) -Destination $stagingPath
}

Compress-Archive -Path (Join-Path $stagingPath '*') -DestinationPath $archivePath -CompressionLevel Optimal

$expectedEntries = @(
    'bin/TerminalDropPath.exe',
    'DropPath.cmd',
    'DropPath.ps1',
    'LICENSE',
    'README.en.md',
    'README.md'
) | Sort-Object

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $actualEntries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') } | Sort-Object)
}
finally {
    $archive.Dispose()
}

$manifestDifference = @(Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $actualEntries)
if ($manifestDifference.Count -ne 0) {
    throw 'The release archive manifest does not match the expected runtime package layout.'
}

New-Item -ItemType Directory -Path $smokeTestPath -Force | Out-Null
try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $smokeTestPath
    foreach ($entry in $expectedEntries) {
        if (-not (Test-Path -LiteralPath (Join-Path $smokeTestPath $entry) -PathType Leaf)) {
            throw "The extracted package is missing $entry."
        }
    }

    $smokeExecutable = Join-Path $smokeTestPath 'bin\TerminalDropPath.exe'
    $helpOutput = (& $smokeExecutable --help) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $helpOutput -notmatch "Terminal Drop Path $([regex]::Escape($Version))") {
        throw 'The packaged executable did not report the expected version.'
    }

    $unicodeName = [string]::Concat([char]0x793A, [char]0x4F8B, [char]0x9879, [char]0x76EE, '.txt')
    $unicodePath = 'C:\Users\Example\Desktop\' + $unicodeName
    $formatOutput = (& $smokeExecutable --format-only --shell powershell -- $unicodePath) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $formatOutput.TrimEnd("`r", "`n") -cne $unicodePath) {
        throw 'The packaged executable failed the Unicode path-formatting smoke test.'
    }
}
finally {
    if (Test-Path -LiteralPath $smokeTestPath) {
        Remove-Item -LiteralPath $smokeTestPath -Recurse -Force
    }
}

$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = "$archiveHash  $([IO.Path]::GetFileName($archivePath))`r`n"
[IO.File]::WriteAllText($checksumPath, $checksumLine, [Text.Encoding]::ASCII)

Write-Host "Packaged $archivePath"
Write-Host "SHA256  $archiveHash"
Write-Host "Checksum $checksumPath"
