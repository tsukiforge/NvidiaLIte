# =============================================================================
# NvidiaLite Release Script
#
# Usage:
#   .\scripts\release-version.ps1 -Version 1.0.0
#   .\scripts\release-version.ps1 -Version 1.0.0 -DryRun
#   .\scripts\release-version.ps1 -Version 1.0.0 -ForceBuild
# =============================================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$DryRun,      # Preview saja, tidak commit/push
    [switch]$ForceBuild   # Re-release tag yang sama (hapus tag lama, push ulang)
)

$ErrorActionPreference = "Stop"

# ── Validasi format versi (semver: x.y.z atau x.y.z-suffix) ─────────────────
if ($Version -notmatch '^\d+\.\d+\.\d+(-[\w\.]+)?$') {
    Write-Host "  [FAIL] Format versi salah. Gunakan: 1.0.0 atau 1.0.0-beta" -ForegroundColor Red
    exit 1
}

$Tag = "v$Version"

# ── Banner ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  =============================================" -ForegroundColor DarkGreen
Write-Host "   NvidiaLite Release — $Tag"                   -ForegroundColor Green
Write-Host "  =============================================" -ForegroundColor DarkGreen
if ($DryRun)    { Write-Host "  [DRY RUN — tidak ada yang di-commit/push]"             -ForegroundColor Yellow }
if ($ForceBuild){ Write-Host "  [FORCE BUILD — tag lama akan dihapus & di-push ulang]" -ForegroundColor Magenta }
Write-Host ""

# ── Cek prerequisites ────────────────────────────────────────────────────────
Write-Host "0. Checking prerequisites..." -ForegroundColor Yellow

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "  [FAIL] .NET SDK tidak ditemukan." -ForegroundColor Red; exit 1
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "  [FAIL] Git tidak ditemukan." -ForegroundColor Red; exit 1
}
if (-not (Test-Path "NvidiaCi.csproj")) {
    Write-Host "  [FAIL] Jalankan script dari root project (tempat NvidiaCi.csproj berada)." -ForegroundColor Red; exit 1
}
Write-Host "   .NET $(dotnet --version) | $(git --version)" -ForegroundColor Green

# ── Step 1: Update versi di csproj ───────────────────────────────────────────
Write-Host ""
Write-Host "1. Updating version in NvidiaCi.csproj..." -ForegroundColor Yellow

$csproj  = "NvidiaCi.csproj"
$content = Get-Content $csproj -Raw

# Gunakan simple string replace, bukan regex, agar path seperti Assets\icon.ico aman
function Set-XmlTag([string]$xml, [string]$tag, [string]$value) {
    $open  = "<$tag>"
    $close = "</$tag>"
    if ($xml -match [regex]::Escape($open)) {
        # Tag sudah ada — replace isinya
        return [regex]::Replace($xml, "$([regex]::Escape($open)).*?$([regex]::Escape($close))", "$open$value$close")
    } else {
        # Tag belum ada — inject sebelum </PropertyGroup> pertama
        return $xml -replace '</PropertyGroup>', "$open$value$close`n  </PropertyGroup>", 1
    }
}

# Version boleh pakai suffix (1.6.0-beta)
$newContent = Set-XmlTag $content "Version" $Version

# AssemblyVersion harus pure numeric — strip suffix
$numericVersion = $Version -replace '-.*$', ''
$newContent = Set-XmlTag $newContent "AssemblyVersion" "$numericVersion.0"

if ($content -eq $newContent) {
    Write-Host "   unchanged: $csproj" -ForegroundColor Gray
} else {
    if (-not $DryRun) { Set-Content $csproj $newContent -NoNewline }
    Write-Host "   updated:   $csproj  →  Version=$Version" -ForegroundColor Green
}

# ── Step 2: Cek RELEASES.md punya entry versi ini ────────────────────────────
Write-Host ""
Write-Host "2. Checking RELEASES.md..." -ForegroundColor Yellow

if (Test-Path "RELEASES.md") {
    $cl = Get-Content "RELEASES.md" -Raw
    if ($cl -match "## \[v?$([regex]::Escape($Version))\]") {
        Write-Host "   found: v$Version entry exists" -ForegroundColor Green
    } else {
        Write-Host "   WARNING: Tidak ada entry v$Version di RELEASES.md" -ForegroundColor Red
        Write-Host "   Tambahkan entry changelog sebelum release." -ForegroundColor Yellow
        if (-not $DryRun) {
            $confirm = Read-Host "   Lanjut tanpa entry RELEASES.md? (y/N)"
            if ($confirm -ne "y") { exit 0 }
        }
    }
} else {
    Write-Host "   skip (RELEASES.md tidak ditemukan)" -ForegroundColor Gray
}

# ── Step 3: Cek git working tree ─────────────────────────────────────────────
Write-Host ""
Write-Host "3. Checking git status..." -ForegroundColor Yellow

$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "   Uncommitted changes:" -ForegroundColor Yellow
    $gitStatus | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkYellow }
}

# ── Step 4: Commit ───────────────────────────────────────────────────────────
Write-Host ""
Write-Host "4. Committing..." -ForegroundColor Yellow

if (-not $DryRun) {
    git add NvidiaCi.csproj
    $staged = git diff --cached --name-only
    if ($staged) {
        git commit -m "chore: release $Tag"
        Write-Host "   committed: chore: release $Tag" -ForegroundColor Green
    } else {
        Write-Host "   nothing to commit" -ForegroundColor Gray
    }
} else {
    Write-Host "   [dry run] would commit: chore: release $Tag" -ForegroundColor Gray
}

# ── Step 5: Tag ──────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "5. Tagging..." -ForegroundColor Yellow

if (-not $DryRun) {
    $existing = git tag -l $Tag
    if ($existing) {
        if ($ForceBuild) {
            Write-Host "   [ForceBuild] deleting local tag $Tag..." -ForegroundColor Magenta
            git tag -d $Tag | Out-Null
            Write-Host "   [ForceBuild] deleting remote tag $Tag..." -ForegroundColor Magenta
            git push origin ":refs/tags/$Tag" 2>$null
        } else {
            Write-Host "   Tag $Tag sudah ada." -ForegroundColor Yellow
            $confirm = Read-Host "   Hapus dan buat ulang? (y/N)"
            if ($confirm -eq "y") {
                git tag -d $Tag | Out-Null
                git push origin ":refs/tags/$Tag" 2>$null
            } else {
                Write-Host "  [FAIL] Aborted. Gunakan -ForceBuild untuk skip konfirmasi." -ForegroundColor Red
                exit 1
            }
        }
    }
    git tag -a $Tag -m "Release $Version"
    Write-Host "   created: $Tag" -ForegroundColor Green
} else {
    Write-Host "   [dry run] would create tag: $Tag" -ForegroundColor Gray
}

# ── Step 6: Push ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "6. Pushing..." -ForegroundColor Yellow

if (-not $DryRun) {
    $branch = git branch --show-current
    git pull --rebase origin $branch

    git push origin $branch
    Write-Host "   pushed branch: $branch" -ForegroundColor Green

    if ($ForceBuild) {
        git push origin $Tag --force
        Write-Host "   force pushed tag: $Tag" -ForegroundColor Magenta
    } else {
        git push origin $Tag
        Write-Host "   pushed tag: $Tag" -ForegroundColor Green
    }
} else {
    Write-Host "   [dry run] would push branch + tag $Tag" -ForegroundColor Gray
}

# ── Done ─────────────────────────────────────────────────────────────────────
Write-Host ""
if ($DryRun) {
    Write-Host "  Dry run selesai. Jalankan tanpa -DryRun untuk release." -ForegroundColor Yellow
} else {
    $repoUrl = (git config --get remote.origin.url) `
        -replace '\.git$', '' `
        -replace '^git@github\.com:', 'https://github.com/'

    Write-Host "  =============================================" -ForegroundColor DarkGreen
    Write-Host "   Released $Tag " -ForegroundColor Green
    Write-Host "  =============================================" -ForegroundColor DarkGreen
    Write-Host ""
    Write-Host "   Releases : $repoUrl/releases/tag/$Tag" -ForegroundColor Cyan
    Write-Host "   Actions  : $repoUrl/actions"           -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   Next steps:"                                       -ForegroundColor Gray
    Write-Host "   - Tunggu GitHub Actions selesai build (~2 menit)"  -ForegroundColor Gray
    Write-Host "   - Cek tab Releases untuk download installer .exe dan ZIP portable" -ForegroundColor Gray
    Write-Host "   - Update RELEASES.md jika belum"                   -ForegroundColor Gray
}
Write-Host ""
