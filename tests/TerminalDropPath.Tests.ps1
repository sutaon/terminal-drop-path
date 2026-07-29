[CmdletBinding()]
param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\bin\TerminalDropPath.exe')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    & (Join-Path $PSScriptRoot '..\build.ps1')
}

$unicodeFileName = [string]::Concat(
    [char]0x793A,
    [char]0x4F8B,
    [char]0x9879,
    [char]0x76EE,
    '-v3.txt'
)
$unicodePath = 'C:\Users\Example\Desktop\' + $unicodeFileName

$cases = @(
    @{
        Name = 'CMD keeps a simple absolute path unquoted'
        Shell = 'cmd'
        Paths = @('C:\Users\Example\Desktop\report.txt')
        Expected = 'C:\Users\Example\Desktop\report.txt'
    },
    @{
        Name = 'CMD quotes a path containing spaces'
        Shell = 'cmd'
        Paths = @('C:\Users\Example\My Files\report.txt')
        Expected = '"C:\Users\Example\My Files\report.txt"'
    },
    @{
        Name = 'PowerShell keeps a Unicode path unquoted'
        Shell = 'powershell'
        Paths = @($unicodePath)
        Expected = $unicodePath
    },
    @{
        Name = 'PowerShell quotes spaces with single quotes'
        Shell = 'powershell'
        Paths = @('C:\Users\Example\My Files\report.txt')
        Expected = "'C:\Users\Example\My Files\report.txt'"
    },
    @{
        Name = 'PowerShell escapes an apostrophe'
        Shell = 'powershell'
        Paths = @("C:\Users\Example\Sam's Files\report.txt")
        Expected = "'C:\Users\Example\Sam''s Files\report.txt'"
    },
    @{
        Name = 'Multiple paths are separated by one space'
        Shell = 'cmd'
        Paths = @('C:\one.txt', 'C:\Two Files\two.txt')
        Expected = 'C:\one.txt "C:\Two Files\two.txt"'
    }
)

$failures = @()

foreach ($case in $cases) {
    $arguments = @('--format-only', '--shell', $case.Shell, '--') + $case.Paths
    $actualLines = & $ExecutablePath $arguments
    if ($LASTEXITCODE -ne 0) {
        $failures += "$($case.Name): process exited with $LASTEXITCODE"
        continue
    }

    $actual = ($actualLines -join [Environment]::NewLine).TrimEnd("`r", "`n")
    if ($actual -cne $case.Expected) {
        $failures += "$($case.Name): expected [$($case.Expected)] but got [$actual]"
    }
    else {
        Write-Host "PASS  $($case.Name)"
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "All $($cases.Count) tests passed."
