[CmdletBinding()]
param(
    [switch]$ShowWindow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$exePath = Join-Path $PSScriptRoot 'bin\TerminalDropPath.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    & (Join-Path $PSScriptRoot 'build.ps1')
}

$toolArguments = @('--shell', 'powershell')
if ($ShowWindow) {
    $toolArguments += '--show-window'
}

& $exePath @toolArguments
if ($LASTEXITCODE -ne 0) {
    throw "Terminal Drop Path exited with code $LASTEXITCODE."
}
