# XNB Exporter Pro v2.0 - PowerShell Build Script
# Run: powershell -ExecutionPolicy Bypass -File build.ps1

Write-Host "========================================"
Write-Host "  XNB Exporter Pro v2.0 - Build Script"
Write-Host "========================================" 
Write-Host ""

# Find C# compiler
$csc = $null

# Try .NET Framework 4.x
$fwPath = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
$fw64Path = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (Test-Path $fw64Path) {
    $csc = $fw64Path
    Write-Host "Using: .NET Framework 4.x (x64)"
} elseif (Test-Path $fwPath) {
    $csc = $fwPath
    Write-Host "Using: .NET Framework 4.x (x86)"
}

# Try dotnet if no csc found
if (-not $csc) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        Write-Host "Using: dotnet build"
        dotnet build -c Release
        exit $LASTEXITCODE
    }
    
    Write-Host "ERROR: No C# compiler found!"
    Write-Host "Install .NET Framework SDK or Visual Studio"
    exit 1
}

# Create output directory
if (-not (Test-Path "bin")) { New-Item -ItemType Directory -Path "bin" | Out-Null }

# Source files
$sources = @(
    "Program.cs",
    "MainForm.cs", 
    "XnbReader.cs",
    "DxtDecoder.cs",
    "LzxDecompressor.cs",
    "ImageWriter.cs"
)

# References
$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.IO.Compression.dll",
    "System.Windows.Forms.dll"
)

$refArgs = ($refs | ForEach-Object { "/reference:$_" }) -join " "
$srcArgs = $sources -join " "

Write-Host "Compiling..."
$cmd = "& `"$csc`" /nologo /optimize+ /target:winexe /out:bin\XNBExporterPro.exe $refArgs $srcArgs"
Invoke-Expression $cmd

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================"
    Write-Host "  Build successful!"
    Write-Host "  Output: bin\XNBExporterPro.exe"
    Write-Host "========================================"
    
    $size = (Get-Item "bin\XNBExporterPro.exe").Length / 1KB
    Write-Host "  Size: $([math]::Round($size, 1)) KB"
} else {
    Write-Host ""
    Write-Host "Build FAILED!"
}
