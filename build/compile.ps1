# ==============================================================================
# NVIDIA LITE Build & Installer Automation Script
# Clean, Professional, Maintainable
# ==============================================================================

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------------------------
# CONFIG
# ------------------------------------------------------------------------------
$BuildDir         = $PSScriptRoot
$ProjectRoot      = (Get-Item "$BuildDir\..").FullName
$Csproj           = "$ProjectRoot\NvidiaCi.csproj"
$IssScript        = "$BuildDir\compile.iss"
$InnoSetup        = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

$AppName          = "NvidiaLite"
$ProjectXml       = [xml](Get-Content $Csproj -Raw)
$Version          = ($ProjectXml.Project.PropertyGroup |
    Where-Object { $_.Version } |
    Select-Object -First 1).Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version tidak ditemukan di NvidiaCi.csproj"
}

$InstallerName    = "NvidiaLite_Setup_v$Version.exe"
$InstallerPath    = "$BuildDir\Output\$InstallerName"
$SignedInstaller  = "$BuildDir\Output\$AppName-Setup-v$Version-signed.exe"

# Signing
$SignTool         = "$BuildDir\bin\osslsigncode.exe"
$CertFile         = "$BuildDir\ZeroMixCert.pfx"
$CertPassword     = "ZeroMixPass"
$TimestampServer  = "http://timestamp.digicert.com"

# Helper Batch for Inno Setup
$SignBat          = "$BuildDir\sign.bat"

# ------------------------------------------------------------------------------
# HELPER FUNCTIONS
# ------------------------------------------------------------------------------
function Info($msg)  { Write-Host $msg -ForegroundColor Cyan }
function Ok($msg)    { Write-Host "✅ $msg" -ForegroundColor Green }
function Warn($msg)  { Write-Host "⚠️  $msg" -ForegroundColor Yellow }
function Fail($msg)  { Write-Host "❌ $msg" -ForegroundColor Red; exit 1 }

# Trick to get Short Path (8.3) to avoid space issues in ISCC
function Get-ShortPath($path) {
    try {
        $fso = New-Object -ComObject Scripting.FileSystemObject
        if (Test-Path -Path $path -PathType Container) {
            return $fso.GetFolder($path).ShortPath
        } else {
            return $fso.GetFile($path).ShortPath
        }
    } catch {
        return $path # Fallback to original if fails
    }
}

# ------------------------------------------------------------------------------
# START
# ------------------------------------------------------------------------------
Info "`n=== NVIDIA LITE Build Script ==="

# 0. Stop instance
Info "`n[0/4] Stopping any running application instances..."
Stop-Process -Name "NvidiaCi" -Force -ErrorAction SilentlyContinue

# ------------------------------------------------------------------------------
# STEP 1: Publish App
# ------------------------------------------------------------------------------
Info "`n[1/4] Publishing application..."

try {
    dotnet publish "$Csproj" `
        -c Release `
        -o "$ProjectRoot\publish\win-x64" `
        -r win-x64 `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        --self-contained true

    # Sign main executable
    $MainExe = "$ProjectRoot\publish\win-x64\NvidiaCi.exe"
    if (Test-Path $MainExe) {
        Info "Signing main executable..."
        & "$SignTool" sign -pkcs12 "$CertFile" -pass "$CertPassword" -ts "$TimestampServer" -in "$MainExe" -out "$MainExe.signed"
        if (Test-Path "$MainExe.signed") {
            Move-Item -Path "$MainExe.signed" -Destination "$MainExe" -Force
            Ok "Executable berhasil ditandatangani"
        }
    }
    
    Ok "Publish aplikasi berhasil"
}
catch {
    Fail "Publish aplikasi gagal: $_"
}

# ------------------------------------------------------------------------------
# STEP 2: Compile Installer
# ------------------------------------------------------------------------------
Info "`n[2/4] Compiling installer with Inno Setup..."

if (-not (Test-Path $InnoSetup)) { Fail "Inno Setup tidak ditemukan" }

# GET SHORT PATH TO AVOID SPACES!
$ShortSignBat = Get-ShortPath $SignBat
Info "Using short path for SignTool: $ShortSignBat"

# Sekarang kita kirim Short Path yang bersih dari spasi
$InnoArgs = @(
    "/Sstandard=$ShortSignBat `$f",
    "/DAppVersion=$Version",
    "compile.iss"
)

Push-Location "$BuildDir"
& "$InnoSetup" $InnoArgs
$exitCode = $LASTEXITCODE
Pop-Location

if ($exitCode -ne 0) {
    Fail "Kompilasi installer gagal (Exit code: $exitCode)"
}

Ok "Installer berhasil dikompilasi"

# ------------------------------------------------------------------------------
# STEP 3: Code Signing
# ------------------------------------------------------------------------------
Info "`n[3/4] Signing installer package..."

if (-not (Test-Path $InstallerPath)) { Fail "Installer tidak ditemukan: $InstallerPath" }
$ActualInstaller = $InstallerPath

& "$SignTool" sign `
    -pkcs12 "$CertFile" `
    -pass "$CertPassword" `
    -ts "$TimestampServer" `
    -in  "$ActualInstaller" `
    -out "$SignedInstaller"

if ($LASTEXITCODE -ne 0) {
    Fail "Penandatanganan installer gagal"
}

# Final Move
$FinalFileName = Split-Path $ActualInstaller -Leaf
Remove-Item "$ActualInstaller" -Force
Move-Item "$SignedInstaller" "$BuildDir\Output\$FinalFileName" -Force

Ok "Installer berhasil ditandatangani"

# ------------------------------------------------------------------------------
# STEP 4: Verify Output
# ------------------------------------------------------------------------------
Info "`n[4/4] Verifying output..."

$FinalFile = "$BuildDir\Output\$FinalFileName"
$SizeMB = [Math]::Round((Get-Item "$FinalFile").Length / 1MB, 2)

Ok "Build selesai!"
Info "📦 File  : $FinalFile"
Info "📊 Ukuran: $SizeMB MB"

# ------------------------------------------------------------------------------
# DONE
# ------------------------------------------------------------------------------
Info "`n=== BUILD COMPLETE ==="
Write-Host "🎉 NVIDIA LITE siap didistribusikan!" -ForegroundColor Green
Write-Host ""
