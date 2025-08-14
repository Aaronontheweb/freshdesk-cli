# Freshdesk CLI Installer for Windows
#
# Usage:
#   iwr -useb https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.ps1 | iex
#
# Or download and run:
#   .\install.ps1
#   .\install.ps1 -InstallDir "C:\custom\path"
#

param(
    [string]$InstallDir = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Configuration
$RepoOwner = "Aaronontheweb"
$RepoName = "freshdesk-cli"
$BinaryName = "freshdesk.exe"

# Set default install directory if not provided
if ([string]::IsNullOrEmpty($InstallDir)) {
    $InstallDir = "$env:LOCALAPPDATA\Programs\freshdesk"
}

# Helper functions
function Write-Info { Write-Host "[INFO] $args" -ForegroundColor Green }
function Write-Warn { Write-Host "[WARN] $args" -ForegroundColor Yellow }
function Write-Error { Write-Host "[ERROR] $args" -ForegroundColor Red; exit 1 }

function Get-Platform {
    $arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
    return "win-$arch"
}

function Get-LatestVersion {
    Write-Info "Fetching latest release information..."
    
    try {
        $headers = @{
            "User-Agent" = "freshdesk-cli-installer"
        }
        
        $release = Invoke-RestMethod `
            -Uri "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest" `
            -Headers $headers
        
        return $release.tag_name
    }
    catch {
        Write-Error "Failed to fetch release information: $_"
    }
}

function Install-Binary {
    param(
        [string]$Version,
        [string]$Platform
    )
    
    # Support versioned artifact names (e.g., freshdesk-1.0.0-win-x64.zip)
    $versionClean = $Version.TrimStart('v')
    $downloadUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/$Version/freshdesk-$versionClean-$Platform.zip"
    $tempFile = "$env:TEMP\freshdesk-$Version.zip"
    $tempDir = "$env:TEMP\freshdesk-$Version"
    
    Write-Info "Downloading freshdesk $Version for $Platform..."
    
    try {
        # Download with progress
        $ProgressPreference = 'Continue'
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile -UseBasicParsing
    }
    catch {
        Write-Error "Failed to download from: $downloadUrl`n$_"
    }
    
    Write-Info "Extracting binary..."
    
    # Create temp extraction directory
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    
    try {
        # Extract archive
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($tempFile, $tempDir)
    }
    catch {
        Write-Error "Failed to extract archive: $_"
    }
    
    # Find the binary
    $binaryPath = Get-ChildItem -Path $tempDir -Filter $BinaryName -Recurse | Select-Object -First 1
    
    if (-not $binaryPath) {
        Write-Error "Binary not found in archive"
    }
    
    # Create install directory if it doesn't exist
    if (-not (Test-Path $InstallDir)) {
        Write-Info "Creating installation directory..."
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }
    
    # Install the binary
    Write-Info "Installing to $InstallDir\$BinaryName..."
    Move-Item -Path $binaryPath.FullName -Destination "$InstallDir\$BinaryName" -Force
    
    # Cleanup
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

function Add-ToPath {
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    
    if ($userPath -notlike "*$InstallDir*") {
        Write-Info "Adding installation directory to PATH..."
        
        $newPath = if ($userPath -eq $null -or $userPath -eq "") {
            $InstallDir
        } else {
            "$userPath;$InstallDir"
        }
        
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        
        # Update current session
        $env:Path = "$env:Path;$InstallDir"
        
        Write-Warn "PATH updated. You may need to restart your terminal for changes to take effect."
    }
}

function Test-Admin {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Main installation flow
function Main {
    Write-Host "===================================" -ForegroundColor Cyan
    Write-Host "  Freshdesk CLI Installer" -ForegroundColor Cyan
    Write-Host "===================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Detect platform
    $platform = Get-Platform
    Write-Info "Detected platform: $platform"
    
    # Get latest version
    $version = Get-LatestVersion
    Write-Info "Latest version: $version"
    
    # Check if already installed
    $existingBinary = "$InstallDir\$BinaryName"
    if (Test-Path $existingBinary) {
        try {
            $currentVersion = & $existingBinary --version 2>$null | Select-String -Pattern '\d+\.\d+\.\d+' | ForEach-Object { $_.Matches[0].Value }
        } catch {
            $currentVersion = "unknown"
        }
        
        Write-Warn "Existing installation found (version: $currentVersion)"
        
        if (-not $Force) {
            $response = Read-Host "Do you want to overwrite it? [y/N]"
            if ($response -ne 'y' -and $response -ne 'Y') {
                Write-Host "Installation cancelled"
                exit 0
            }
        }
    }
    
    # Install binary
    Install-Binary -Version $version -Platform $platform
    
    # Verify installation
    try {
        $testOutput = & "$InstallDir\$BinaryName" --version 2>$null
        if ($testOutput) {
            Write-Info "✓ Successfully installed freshdesk $version"
        } else {
            throw "Verification failed"
        }
    }
    catch {
        Write-Error "Installation verification failed: $_"
    }
    
    # Add to PATH
    Add-ToPath
    
    Write-Host ""
    Write-Host "Installation complete! 🎉" -ForegroundColor Green
    Write-Host ""
    Write-Host "Run 'freshdesk --help' to get started" -ForegroundColor Cyan
    Write-Host "Run 'freshdesk update' to check for updates" -ForegroundColor Cyan
    
    # Create desktop shortcut option
    $createShortcut = Read-Host "`nCreate desktop shortcut? [y/N]"
    if ($createShortcut -eq 'y' -or $createShortcut -eq 'Y') {
        $WshShell = New-Object -comObject WScript.Shell
        $Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\Freshdesk CLI.lnk")
        $Shortcut.TargetPath = "$InstallDir\$BinaryName"
        $Shortcut.WorkingDirectory = $InstallDir
        $Shortcut.IconLocation = "$InstallDir\$BinaryName"
        $Shortcut.Save()
        Write-Info "Desktop shortcut created"
    }
}

# Run main function
Main