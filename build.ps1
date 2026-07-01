# Build script for FileFinder
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Clean,
    [switch]$Rebuild,
    [switch]$Run
)

$SolutionFile = "$PSScriptRoot\FileFinder.slnx"
$MSBuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

function Invoke-MSBuild([string]$Target) {
    if (-not (Test-Path $MSBuild)) {
        Write-Host "MSBuild not found, using dotnet CLI..." -ForegroundColor Yellow
        switch ($Target) {
            "Clean"   { dotnet clean $SolutionFile --configuration $Configuration }
            "Rebuild" { dotnet build $SolutionFile --configuration $Configuration --no-incremental }
            default   { dotnet build $SolutionFile --configuration $Configuration }
        }
    } else {
        $MSBuildTarget = switch ($Target) {
            "Clean"   { "/t:Clean" }
            "Rebuild" { "/t:Clean;Restore;Build" }
            default   { "/t:Restore;Build" }
        }
        & $MSBuild $SolutionFile $MSBuildTarget /p:Configuration=$Configuration "/p:Platform=Any CPU" /v:minimal
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "$Target FAILED." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

if ($Clean -and -not $Rebuild) {
    Write-Host "Cleaning FileFinder ($Configuration)..." -ForegroundColor Cyan
    Invoke-MSBuild "Clean"
    Write-Host "Clean succeeded." -ForegroundColor Green
    exit 0
}

if ($Rebuild) {
    Write-Host "Rebuilding FileFinder ($Configuration)..." -ForegroundColor Cyan
    Invoke-MSBuild "Rebuild"
} else {
    Write-Host "Building FileFinder ($Configuration)..." -ForegroundColor Cyan
    Invoke-MSBuild "Build"
}

Write-Host "Build succeeded." -ForegroundColor Green

if ($Run) {
    $Exe = "$PSScriptRoot\FileFinder\bin\$Configuration\net8.0-windows\FileFinder.exe"
    if (Test-Path $Exe) {
        Write-Host "Launching $Exe..." -ForegroundColor Cyan
        Start-Process $Exe
    } else {
        Write-Host "Executable not found: $Exe" -ForegroundColor Red
        exit 1
    }
}
