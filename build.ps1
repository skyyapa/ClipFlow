param(
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src\ClipFlow'
$outputRoot = Join-Path $projectRoot 'dist'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpfRoot = Join-Path $frameworkRoot 'WPF'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Windows C# compiler was not found.'
}

New-Item -ItemType Directory -Force $outputRoot | Out-Null

$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName }
$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    ('/win32manifest:' + (Join-Path $sourceRoot 'app.manifest')),
    ('/out:' + (Join-Path $outputRoot 'ClipFlow.exe')),
    ('/reference:' + (Join-Path $wpfRoot 'PresentationCore.dll')),
    ('/reference:' + (Join-Path $wpfRoot 'PresentationFramework.dll')),
    ('/reference:' + (Join-Path $wpfRoot 'WindowsBase.dll')),
    ('/reference:' + (Join-Path $frameworkRoot 'System.Xaml.dll')),
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Runtime.Serialization.dll'
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'ClipFlow build failed.' }

Write-Output (Join-Path $outputRoot 'ClipFlow.exe')
if ($Run) {
    Start-Process -FilePath (Join-Path $outputRoot 'ClipFlow.exe')
}
