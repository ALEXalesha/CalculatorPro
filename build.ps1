<#
.SYNOPSIS
  Tests and packages all three calculators into .\dist — a portable .exe and a
  setup .exe for each.

.DESCRIPTION
  calcpro-wpf     dotnet test → dotnet publish (single-file, self-contained) → Inno Setup
  calcpro-glass   npm test → e2e smoke (Electron) → electron-builder (portable + NSIS)
  ios-calculator  same as calcpro-glass

  Requirements: .NET SDK 8+, Node.js 20+, Inno Setup 6 (ISCC.exe).

.EXAMPLE
  .\build.ps1                 # everything
  .\build.ps1 -Only wpf       # one app: wpf | glass | ios
  .\build.ps1 -SkipTests      # package only
#>
param(
    [ValidateSet('all', 'wpf', 'glass', 'ios')]
    [string]$Only = 'all',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist '.stage'

function Step([string]$text) { Write-Host "`n=== $text" -ForegroundColor Cyan }

function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)" }
}

function Find-Iscc {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($found) { return $found }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw 'Inno Setup 6 (ISCC.exe) not found. Install it: winget install JRSoftware.InnoSetup'
}

function Build-Wpf {
    $project = Join-Path $root 'calcpro-wpf'
    $csproj = Join-Path $project 'src\CalcPro.Wpf\CalcPro.Wpf.csproj'
    $version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

    if (-not $SkipTests) {
        Step "calcpro-wpf ${version}: tests"
        Invoke-Checked 'dotnet test' { dotnet test (Join-Path $project 'CalcPro.sln') -c Release --nologo }
    }

    Step "calcpro-wpf ${version}: portable"
    $out = Join-Path $stage 'calcpro-wpf'
    Invoke-Checked 'dotnet publish' {
        dotnet publish $csproj -c Release -r win-x64 --nologo -o $out `
            -p:SelfContained=true -p:PublishSingleFile=true
    }
    Copy-Item (Join-Path $out 'CalcPro.exe') (Join-Path $dist "CalcPro-$version-Portable.exe")

    Step "calcpro-wpf ${version}: setup"
    $iscc = Find-Iscc
    Invoke-Checked 'ISCC' {
        & $iscc /Q "/DMyAppVersion=$version" "/DSourceExe=$(Join-Path $out 'CalcPro.exe')" "/DOutputDir=$dist" `
            (Join-Path $project 'installer\CalcPro.iss')
    }
}

function Build-Electron([string]$folder) {
    $project = Join-Path $root $folder
    $version = (Get-Content (Join-Path $project 'package.json') -Raw | ConvertFrom-Json).version
    Push-Location $project
    try {
        if (-not (Test-Path 'node_modules')) {
            Step "${folder}: npm ci"
            Invoke-Checked 'npm ci' { npm ci --no-audit --no-fund }
        }
        if (-not $SkipTests) {
            Step "$folder ${version}: unit tests"
            Invoke-Checked 'npm test' { npm test --silent }
            Step "$folder ${version}: e2e smoke"
            Invoke-Checked 'e2e' { npx electron e2e/smoke.js }
        }
        Step "$folder ${version}: portable + setup"
        if (Test-Path 'dist') { Remove-Item -Recurse -Force 'dist' }
        Invoke-Checked 'electron-builder' { npx electron-builder --win --x64 --publish never }
        Get-ChildItem 'dist' -Filter '*.exe' | Where-Object { $_.Name -match '-(Portable|Setup)\.exe$' } |
            Copy-Item -Destination $dist
    }
    finally {
        Pop-Location
    }
}

# A full build starts from an empty dist; -Only replaces just that app's files.
$prefixes = @{ wpf = 'CalcPro-'; glass = 'CalcProGlass-'; ios = 'CalculatorIOS26-' }
if ($Only -eq 'all') {
    if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
} elseif (Test-Path $dist) {
    Get-ChildItem $dist -Filter "$($prefixes[$Only])*.exe" | Remove-Item -Force
}
New-Item -ItemType Directory -Force $stage | Out-Null

$started = Get-Date
if ($Only -in 'all', 'wpf') { Build-Wpf }
if ($Only -in 'all', 'glass') { Build-Electron 'calcpro-glass' }
if ($Only -in 'all', 'ios') { Build-Electron 'ios-calculator' }

Remove-Item -Recurse -Force $stage
Step "done in $([int]((Get-Date) - $started).TotalSeconds) s"
Get-ChildItem $dist -Filter '*.exe' | ForEach-Object { '{0,8:N1} MB  {1}' -f ($_.Length / 1MB), $_.Name }
