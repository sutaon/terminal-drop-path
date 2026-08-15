[CmdletBinding()]
param(
    [string]$ExecutablePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $PSScriptRoot '..\bin\TerminalDropPath.exe'
}

if (-not ('TerminalDropPathTests.ConsoleNativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

namespace TerminalDropPathTests
{
    public static class ConsoleNativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern uint GetConsoleCP();

        [DllImport("kernel32.dll")]
        public static extern uint GetConsoleOutputCP();
    }
}
'@
}

$initialInputCodePage = [TerminalDropPathTests.ConsoleNativeMethods]::GetConsoleCP()
$initialOutputCodePage = [TerminalDropPathTests.ConsoleNativeMethods]::GetConsoleOutputCP()

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
        Name = 'PowerShell safely quotes percent and exclamation characters'
        Shell = 'powershell'
        Paths = @('C:\100%PATH%!\file.txt')
        Expected = "'C:\100%PATH%!\file.txt'"
    },
    @{
        Name = 'Multiple paths are separated by one space'
        Shell = 'cmd'
        Paths = @('C:\one.txt', 'C:\Two Files\two.txt')
        Expected = 'C:\one.txt "C:\Two Files\two.txt"'
    }
)

$failures = @()

function Invoke-NormalizeCopy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Mode,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$InputText
    )

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = (Resolve-Path -LiteralPath $ExecutablePath).Path
    $startInfo.Arguments = "--normalize-copy $Mode"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = $utf8
    $startInfo.StandardErrorEncoding = $utf8

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw 'Failed to start TerminalDropPath.exe.'
        }
        $inputBytes = $utf8.GetBytes($InputText)
        $process.StandardInput.BaseStream.Write($inputBytes, 0, $inputBytes.Length)
        $process.StandardInput.BaseStream.Close()
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $standardOutput
            Error = $standardError
        }
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-TerminalDropPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Arguments
    )

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = (Resolve-Path -LiteralPath $ExecutablePath).Path
    $startInfo.Arguments = $Arguments
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = $utf8
    $startInfo.StandardErrorEncoding = $utf8

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw 'Failed to start TerminalDropPath.exe.'
        }
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $standardOutput
            Error = $standardError
        }
    }
    finally {
        $process.Dispose()
    }
}

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

$firstLine = [string]::Concat([char]0x7B2C, [char]0x4E00, [char]0x884C)
$secondLine = [string]::Concat([char]0x7B2C, [char]0x4E8C, [char]0x884C)
$thirdLine = [string]::Concat([char]0x7B2C, [char]0x4E09, [char]0x884C)
$fourthLine = [string]::Concat([char]0x7B2C, [char]0x56DB, [char]0x884C)
$emoji = [char]::ConvertFromUtf32(0x1F642)
$rareCjk = [char]::ConvertFromUtf32(0x20000)

$normalizeCases = @(
    @{
        Name = 'Paragraph mode joins Chinese wrapped lines without spaces'
        Mode = 'paragraphs'
        Input = $firstLine + "`r`n" + $secondLine
        Expected = $firstLine + $secondLine
    },
    @{
        Name = 'Paragraph mode inserts one English word-boundary space'
        Mode = 'paragraphs'
        Input = "A wrapped`nparagraph."
        Expected = 'A wrapped paragraph.'
    },
    @{
        Name = 'Paragraph mode does not duplicate an existing space'
        Mode = 'paragraphs'
        Input = "A wrapped `rparagraph."
        Expected = 'A wrapped paragraph.'
    },
    @{
        Name = 'Paragraph mode keeps blank-line paragraph boundaries'
        Mode = 'paragraphs'
        Input = $firstLine + "`r`n" + $secondLine + "`r`n`r`n" + $thirdLine + "`n" + $fourthLine
        Expected = $firstLine + $secondLine + "`r`n`r`n" + $thirdLine + $fourthLine
    },
    @{
        Name = 'Paragraph mode normalizes preserved line endings to CRLF'
        Mode = 'paragraphs'
        Input = "`nalpha`n`nbeta`r"
        Expected = "`r`nalpha`r`n`r`nbeta`r`n"
    },
    @{
        Name = 'Paragraph mode preserves Unicode surrogate pairs'
        Mode = 'paragraphs'
        Input = "Status $emoji`ncontinues here."
        Expected = "Status $emoji continues here."
    },
    @{
        Name = 'Paragraph mode joins supplementary CJK without spaces'
        Mode = 'paragraphs'
        Input = "$rareCjk`n$rareCjk"
        Expected = "$rareCjk$rareCjk"
    },
    @{
        Name = 'Single-line mode removes mixed line endings without adding spaces'
        Mode = 'single-line'
        Input = "https://example.`r`ncom/a`nb`rc"
        Expected = 'https://example.com/abc'
    },
    @{
        Name = 'Single-line mode preserves text that has no line breaks'
        Mode = 'single-line'
        Input = 'unchanged'
        Expected = 'unchanged'
    },
    @{
        Name = 'Single-line mode accepts input containing only line breaks'
        Mode = 'single-line'
        Input = "`r`n`n`r"
        Expected = ''
    },
    @{
        Name = 'Paragraph mode accepts empty input'
        Mode = 'paragraphs'
        Input = ''
        Expected = ''
    }
)

foreach ($case in $normalizeCases) {
    $result = Invoke-NormalizeCopy -Mode $case.Mode -InputText $case.Input
    if ($result.ExitCode -ne 0) {
        $failures += "$($case.Name): process exited with $($result.ExitCode): $($result.Error)"
    }
    elseif ($result.Output -cne $case.Expected) {
        $failures += "$($case.Name): expected [$($case.Expected)] but got [$($result.Output)]"
    }
    else {
        Write-Host "PASS  $($case.Name)"
    }
}

$helpOutput = (& $ExecutablePath --help) -join "`n"
if ($LASTEXITCODE -ne 0 -or $helpOutput -notmatch 'Terminal Drop Path 0\.2\.0') {
    $failures += 'Help output does not report version 0.2.0.'
}
else {
    Write-Host 'PASS  Help reports version 0.2.0'
}

$invalidMode = Invoke-NormalizeCopy -Mode 'invalid' -InputText 'text'
if ($invalidMode.ExitCode -ne 2 -or $invalidMode.Error -notmatch 'paragraphs or single-line') {
    $failures += 'Invalid copy mode was not rejected with exit code 2 and a useful error.'
}
else {
    Write-Host 'PASS  Invalid copy mode is rejected'
}

$missingTargetProcess = Invoke-TerminalDropPath -Arguments '--target-hwnd 1'
if ($missingTargetProcess.ExitCode -ne 2 -or $missingTargetProcess.Error -notmatch 'must be provided together') {
    $failures += 'A target window without a target process ID was not rejected.'
}
else {
    Write-Host 'PASS  Target window and process ID must be paired'
}

$invalidTargetProcess = Invoke-TerminalDropPath -Arguments '--target-hwnd 1 --target-pid invalid'
if ($invalidTargetProcess.ExitCode -ne 2 -or $invalidTargetProcess.Error -notmatch 'positive process ID') {
    $failures += 'An invalid target process ID was not rejected.'
}
else {
    Write-Host 'PASS  Invalid target process ID is rejected'
}

$unsafePercentPath = Invoke-TerminalDropPath -Arguments '--format-only --shell cmd -- C:\100%PATH%\file.txt'
if ($unsafePercentPath.ExitCode -ne 1 -or $unsafePercentPath.Error -notmatch 'cannot safely preserve') {
    $failures += 'CMD did not reject a path containing an expandable percent expression.'
}
else {
    Write-Host 'PASS  CMD rejects paths containing percent expansion syntax'
}

$unsafeExclamationPath = Invoke-TerminalDropPath -Arguments '--format-only --shell cmd -- C:\bang!PATH!\file.txt'
if ($unsafeExclamationPath.ExitCode -ne 1 -or $unsafeExclamationPath.Error -notmatch 'cannot safely preserve') {
    $failures += 'CMD did not reject a path containing delayed-expansion syntax.'
}
else {
    Write-Host 'PASS  CMD rejects paths containing delayed-expansion syntax'
}

$finalInputCodePage = [TerminalDropPathTests.ConsoleNativeMethods]::GetConsoleCP()
$finalOutputCodePage = [TerminalDropPathTests.ConsoleNativeMethods]::GetConsoleOutputCP()
if (($initialInputCodePage -ne 0 -and $finalInputCodePage -ne $initialInputCodePage) -or
    ($initialOutputCodePage -ne 0 -and $finalOutputCodePage -ne $initialOutputCodePage)) {
    $failures += "Console code pages changed from $initialInputCodePage/$initialOutputCodePage to $finalInputCodePage/$finalOutputCodePage."
}
else {
    Write-Host 'PASS  Command-line modes preserve console code pages'
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

$totalTests = $cases.Count + $normalizeCases.Count + 7
Write-Host "All $totalTests tests passed."
exit 0
