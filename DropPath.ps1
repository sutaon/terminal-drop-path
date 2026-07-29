[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$exePath = Join-Path $PSScriptRoot 'bin\TerminalDropPath.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    & (Join-Path $PSScriptRoot 'build.ps1')
}

& $exePath --shell powershell
if ($LASTEXITCODE -ne 0) {
    throw "Terminal Drop Path exited with code $LASTEXITCODE."
}
