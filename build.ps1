[CmdletBinding()]
param(
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$outputDirectory = Join-Path $repoRoot 'bin'
$outputPath = Join-Path $outputDirectory 'TerminalDropPath.exe'
$sourcePath = Join-Path $repoRoot 'src\TerminalDropPath.cs'

if ($Clean -and (Test-Path -LiteralPath $outputDirectory)) {
    $resolvedOutput = [IO.Path]::GetFullPath($outputDirectory)
    $resolvedRoot = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
    if (-not $resolvedOutput.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside the repository: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $compiler) {
    throw 'The .NET Framework C# compiler was not found. Install .NET Framework 4.8 Developer Pack.'
}

$arguments = @(
    '/nologo',
    '/target:exe',
    '/platform:anycpu',
    '/optimize+',
    '/warn:4',
    '/warnaserror+',
    "/out:$outputPath",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    $sourcePath
)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built $outputPath"
