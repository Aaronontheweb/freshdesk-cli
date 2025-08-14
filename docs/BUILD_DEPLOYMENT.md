# Build and Deployment Guide

## Project Setup

### 1. Create the Project
```bash
# Create solution directory structure
mkdir -p src/FreshdeskCLI
mkdir -p tests/FreshdeskCLI.Tests
mkdir -p tests/TestData/FreshdeskResponses

# Create the console project
dotnet new console -n FreshdeskCLI -o src/FreshdeskCLI

# Add to existing .slnx solution
dotnet sln FreshDeskCli.slnx add src/FreshdeskCLI/FreshdeskCLI.csproj

# Create test project
dotnet new xunit -n FreshdeskCLI.Tests -o tests/FreshdeskCLI.Tests
dotnet sln FreshDeskCli.slnx add tests/FreshdeskCLI.Tests/FreshdeskCLI.Tests.csproj

# Add project reference
dotnet add tests/FreshdeskCLI.Tests reference src/FreshdeskCLI
```

### 2. Configure Project File for AOT

**src/FreshdeskCLI/FreshdeskCLI.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    
    <!-- AOT Configuration -->
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>false</InvariantGlobalization>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    
    <!-- Assembly Information -->
    <AssemblyName>freshdesk</AssemblyName>
    <RootNamespace>FreshdeskCLI</RootNamespace>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>CLI tool for managing Freshdesk support tickets</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    
    <!-- Build Options -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    
    <!-- Trimming Options -->
    <TrimMode>full</TrimMode>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <TrimmerSingleWarn>false</TrimmerSingleWarn>
  </PropertyGroup>

  <ItemGroup>
    <!-- Required Dependencies -->
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.*" />
    
    <!-- AOT Compatibility Analyzers -->
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.*" />
  </ItemGroup>

  <!-- AOT Warnings as Errors -->
  <PropertyGroup Condition="'$(PublishAot)' == 'true'">
    <WarningsAsErrors>$(WarningsAsErrors);IL2026;IL2057;IL2067;IL2075;IL2096;IL3050</WarningsAsErrors>
  </PropertyGroup>

</Project>
```

### 3. Add Dependencies
```bash
cd src/FreshdeskCLI

# Core dependencies
dotnet add package System.CommandLine --prerelease
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Http
dotnet add package Microsoft.Extensions.Configuration.Binder

# Test dependencies
cd ../../tests/FreshdeskCLI.Tests
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Moq
dotnet add package FluentAssertions
```

## Build Configurations

### Development Build
```bash
# Build for development
dotnet build -c Debug

# Run locally
dotnet run --project src/FreshdeskCLI -- tickets list

# Run with verbose logging
dotnet run --project src/FreshdeskCLI -- tickets list --verbose
```

### Release Build
```bash
# Build for release
dotnet build -c Release

# Run release build
dotnet run -c Release --project src/FreshdeskCLI -- tickets list
```

## AOT Publishing

### Platform-Specific Builds

#### Linux (x64)
```bash
dotnet publish src/FreshdeskCLI \
  -c Release \
  -r linux-x64 \
  --self-contained \
  -o ./publish/linux-x64

# Output: ./publish/linux-x64/freshdesk (10-15MB)
```

#### Windows (x64)
```bash
dotnet publish src/FreshdeskCLI `
  -c Release `
  -r win-x64 `
  --self-contained `
  -o ./publish/win-x64

# Output: ./publish/win-x64/freshdesk.exe (10-15MB)
```

#### macOS (x64)
```bash
dotnet publish src/FreshdeskCLI \
  -c Release \
  -r osx-x64 \
  --self-contained \
  -o ./publish/osx-x64

# Output: ./publish/osx-x64/freshdesk (10-15MB)
```

#### macOS (ARM64/Apple Silicon)
```bash
dotnet publish src/FreshdeskCLI \
  -c Release \
  -r osx-arm64 \
  --self-contained \
  -o ./publish/osx-arm64

# Output: ./publish/osx-arm64/freshdesk (10-15MB)
```

### Cross-Compilation Script
```bash
#!/bin/bash
# build-all.sh

VERSION="1.0.0"
PLATFORMS=("linux-x64" "linux-arm64" "win-x64" "osx-x64" "osx-arm64")

for platform in "${PLATFORMS[@]}"; do
  echo "Building for $platform..."
  
  dotnet publish src/FreshdeskCLI \
    -c Release \
    -r $platform \
    --self-contained \
    -o ./publish/$platform \
    -p:Version=$VERSION
  
  # Create archive
  if [[ $platform == win-* ]]; then
    cd ./publish/$platform
    zip -r ../freshdesk-$VERSION-$platform.zip .
    cd ../..
  else
    cd ./publish/$platform
    tar -czf ../freshdesk-$VERSION-$platform.tar.gz .
    cd ../..
  fi
done
```

## AOT Validation

### 1. Check for Trim Warnings
```bash
# Build with trim analysis
dotnet publish src/FreshdeskCLI \
  -c Release \
  -r linux-x64 \
  /p:TrimmerSingleWarn=false \
  /p:PublishTrimmed=true

# Should complete with no warnings
```

### 2. Verify Binary Size
```bash
# Check binary size
ls -lh ./publish/linux-x64/freshdesk

# Expected: 10-15MB
```

### 3. Test Startup Performance
```bash
# Measure startup time
time ./publish/linux-x64/freshdesk --version

# Expected: < 100ms
```

### 4. Validate JSON Serialization
```bash
# Create test script
cat > test-aot.sh << 'EOF'
#!/bin/bash
./publish/linux-x64/freshdesk auth status
./publish/linux-x64/freshdesk tickets list --output json
./publish/linux-x64/freshdesk help --format llm
EOF

chmod +x test-aot.sh
./test-aot.sh
```

## CI/CD Pipeline

### GitHub Actions Workflow
```yaml
# .github/workflows/build.yml
name: Build and Release

on:
  push:
    branches: [main, dev]
    tags: ['v*']
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
    
    - name: AOT Validation
      run: |
        dotnet publish src/FreshdeskCLI \
          -c Release \
          -r linux-x64 \
          /p:TrimmerSingleWarn=false \
          /p:PublishTrimmed=true

  build:
    needs: test
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/')
    
    strategy:
      matrix:
        runtime: [linux-x64, linux-arm64, win-x64, osx-x64, osx-arm64]
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Get version
      id: version
      run: echo "VERSION=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT
    
    - name: Publish ${{ matrix.runtime }}
      run: |
        dotnet publish src/FreshdeskCLI \
          -c Release \
          -r ${{ matrix.runtime }} \
          --self-contained \
          -o ./publish/${{ matrix.runtime }} \
          -p:Version=${{ steps.version.outputs.VERSION }}
    
    - name: Create archive
      run: |
        cd ./publish/${{ matrix.runtime }}
        if [[ "${{ matrix.runtime }}" == win-* ]]; then
          zip -r ../../freshdesk-${{ steps.version.outputs.VERSION }}-${{ matrix.runtime }}.zip .
        else
          tar -czf ../../freshdesk-${{ steps.version.outputs.VERSION }}-${{ matrix.runtime }}.tar.gz .
        fi
    
    - name: Upload artifact
      uses: actions/upload-artifact@v4
      with:
        name: freshdesk-${{ matrix.runtime }}
        path: freshdesk-*.*

  release:
    needs: build
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/')
    
    steps:
    - name: Download artifacts
      uses: actions/download-artifact@v4
      with:
        path: ./artifacts
    
    - name: Create Release
      uses: softprops/action-gh-release@v1
      with:
        files: ./artifacts/**/*
        generate_release_notes: true
        draft: false
        prerelease: false
```

## Installation Methods

### 1. Manual Installation

#### Linux/macOS
```bash
# Download binary
wget https://github.com/yourusername/freshdesk-cli/releases/latest/download/freshdesk-linux-x64.tar.gz

# Extract
tar -xzf freshdesk-linux-x64.tar.gz

# Make executable
chmod +x freshdesk

# Move to PATH
sudo mv freshdesk /usr/local/bin/

# Verify installation
freshdesk --version
```

#### Windows
```powershell
# Download binary
Invoke-WebRequest -Uri "https://github.com/yourusername/freshdesk-cli/releases/latest/download/freshdesk-win-x64.zip" -OutFile "freshdesk.zip"

# Extract
Expand-Archive -Path "freshdesk.zip" -DestinationPath "."

# Add to PATH (run as Administrator)
$path = [Environment]::GetEnvironmentVariable("PATH", "Machine")
[Environment]::SetEnvironmentVariable("PATH", "$path;C:\Program Files\freshdesk", "Machine")

# Verify installation
freshdesk --version
```

### 2. Homebrew (macOS/Linux)
```bash
# Create formula (homebrew-freshdesk/Formula/freshdesk.rb)
class Freshdesk < Formula
  desc "CLI tool for managing Freshdesk support tickets"
  homepage "https://github.com/yourusername/freshdesk-cli"
  version "1.0.0"
  
  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/yourusername/freshdesk-cli/releases/download/v1.0.0/freshdesk-1.0.0-osx-arm64.tar.gz"
      sha256 "..."
    else
      url "https://github.com/yourusername/freshdesk-cli/releases/download/v1.0.0/freshdesk-1.0.0-osx-x64.tar.gz"
      sha256 "..."
    end
  end
  
  on_linux do
    url "https://github.com/yourusername/freshdesk-cli/releases/download/v1.0.0/freshdesk-1.0.0-linux-x64.tar.gz"
    sha256 "..."
  end
  
  def install
    bin.install "freshdesk"
  end
  
  test do
    assert_match "freshdesk", shell_output("#{bin}/freshdesk --version")
  end
end

# Install
brew tap yourusername/freshdesk
brew install freshdesk
```

### 3. Package Managers

#### apt (Ubuntu/Debian)
```bash
# Create .deb package
mkdir -p freshdesk-deb/DEBIAN
mkdir -p freshdesk-deb/usr/local/bin

cat > freshdesk-deb/DEBIAN/control << EOF
Package: freshdesk-cli
Version: 1.0.0
Architecture: amd64
Maintainer: Your Name <email@example.com>
Description: CLI tool for managing Freshdesk support tickets
EOF

cp ./publish/linux-x64/freshdesk freshdesk-deb/usr/local/bin/
dpkg-deb --build freshdesk-deb freshdesk-cli_1.0.0_amd64.deb
```

#### Scoop (Windows)
```json
{
  "version": "1.0.0",
  "description": "CLI tool for managing Freshdesk support tickets",
  "homepage": "https://github.com/yourusername/freshdesk-cli",
  "license": "MIT",
  "architecture": {
    "64bit": {
      "url": "https://github.com/yourusername/freshdesk-cli/releases/download/v1.0.0/freshdesk-1.0.0-win-x64.zip",
      "hash": "..."
    }
  },
  "bin": "freshdesk.exe",
  "checkver": "github",
  "autoupdate": {
    "architecture": {
      "64bit": {
        "url": "https://github.com/yourusername/freshdesk-cli/releases/download/v$version/freshdesk-$version-win-x64.zip"
      }
    }
  }
}
```

## Docker Distribution

### Dockerfile
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY src/FreshdeskCLI/*.csproj ./FreshdeskCLI/
RUN dotnet restore FreshdeskCLI/FreshdeskCLI.csproj

# Copy source code
COPY src/FreshdeskCLI/. ./FreshdeskCLI/

# Build and publish AOT
RUN dotnet publish FreshdeskCLI/FreshdeskCLI.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -o /app/publish

# Runtime stage - minimal image
FROM ubuntu:22.04
WORKDIR /app

# Install required libraries for AOT binary
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates \
    libicu70 && \
    rm -rf /var/lib/apt/lists/*

# Copy published binary
COPY --from=build /app/publish/freshdesk /usr/local/bin/freshdesk

# Make executable
RUN chmod +x /usr/local/bin/freshdesk

# Create non-root user
RUN useradd -m -s /bin/bash freshdesk
USER freshdesk

ENTRYPOINT ["freshdesk"]
```

### Docker Hub Publishing
```bash
# Build image
docker build -t yourusername/freshdesk-cli:latest .
docker tag yourusername/freshdesk-cli:latest yourusername/freshdesk-cli:1.0.0

# Push to Docker Hub
docker push yourusername/freshdesk-cli:latest
docker push yourusername/freshdesk-cli:1.0.0

# Usage
docker run --rm -v ~/.freshdesk:/home/freshdesk/.freshdesk yourusername/freshdesk-cli tickets list
```

## Performance Monitoring

### Startup Time Measurement
```bash
#!/bin/bash
# measure-startup.sh

echo "Measuring startup time..."

# Measure 10 runs
for i in {1..10}; do
  /usr/bin/time -f "%e" ./freshdesk --version 2>&1 | tail -n 1
done | awk '{sum+=$1} END {print "Average: " sum/NR " seconds"}'
```

### Memory Usage
```bash
# Measure memory usage
/usr/bin/time -v ./freshdesk tickets list 2>&1 | grep "Maximum resident"
```

### Binary Size Analysis
```bash
# Analyze binary composition
size ./freshdesk

# Check dependencies (Linux)
ldd ./freshdesk

# Strip symbols for smaller size (optional)
strip --strip-all ./freshdesk
```

## Troubleshooting

### Common AOT Issues

#### 1. Reflection Usage
```
Warning IL2026: Using member 'Type.GetMethod(String)' which has 'RequiresUnreferencedCodeAttribute'
```
**Solution**: Replace reflection with source generators or compile-time alternatives.

#### 2. Dynamic Assembly Loading
```
System.PlatformNotSupportedException: Dynamic code generation is not supported on this platform.
```
**Solution**: Ensure all serialization uses source generators, not runtime generation.

#### 3. Trimming Issues
```
System.TypeLoadException: Could not load type 'X' from assembly 'Y'
```
**Solution**: Add assembly to TrimmerRootAssembly or use DynamicDependency attribute.

### Debug AOT Issues
```bash
# Enable detailed trim analysis
dotnet publish -c Release -r linux-x64 \
  /p:EnableTrimAnalyzer=true \
  /p:TrimmerSingleWarn=false \
  --verbosity detailed > build.log 2>&1

# Check for warnings
grep -i "warning" build.log
```