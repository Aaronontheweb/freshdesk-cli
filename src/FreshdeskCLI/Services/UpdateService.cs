using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreshdeskCLI.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task<bool> PerformUpdateAsync(UpdateInfo update, CancellationToken cancellationToken = default);
}

public sealed class UpdateService : IUpdateService
{
    private const string GITHUB_API_URL = "https://api.github.com/repos/Aaronontheweb/freshdesk-cli/releases/latest";
    private readonly HttpClient _httpClient;
    private readonly string _currentVersion;

    public UpdateService(HttpClient httpClient, string currentVersion)
    {
        _httpClient = httpClient;
        _currentVersion = currentVersion;
        
        // Set User-Agent header for GitHub API
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "freshdesk-cli");
        }
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(GITHUB_API_URL, cancellationToken);
            var release = JsonSerializer.Deserialize(response, UpdateJsonContext.Default.GitHubRelease);

            if (release == null)
                return null;

            // Parse version from tag (e.g., "v1.2.3" -> "1.2.3")
            var latestVersion = release.TagName.TrimStart('v');

            if (IsNewerVersion(latestVersion, _currentVersion))
            {
                var platform = GetPlatformString();
                var asset = FindPlatformAsset(release.Assets, platform);
                
                if (asset != null)
                {
                    return new UpdateInfo
                    {
                        Version = latestVersion,
                        DownloadUrl = asset.BrowserDownloadUrl,
                        FileName = asset.Name,
                        ReleaseNotes = release.Body,
                        PublishedAt = release.PublishedAt
                    };
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail
            Console.Error.WriteLine($"Update check failed: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> PerformUpdateAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        var currentExePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExePath))
        {
            Console.Error.WriteLine("Cannot determine current executable path");
            return false;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"freshdesk-update-{update.Version}");
        var tempFile = Path.Combine(tempDir, update.FileName);
        var backupPath = currentExePath + ".backup";

        try
        {
            // Create temp directory
            Directory.CreateDirectory(tempDir);

            // Download new version
            Console.WriteLine($"Downloading version {update.Version}...");
            await DownloadFileAsync(update.DownloadUrl, tempFile, cancellationToken);

            // Extract if needed
            var extractedBinary = await ExtractBinaryAsync(tempFile, tempDir);
            if (string.IsNullOrEmpty(extractedBinary))
            {
                throw new InvalidOperationException("Failed to extract binary from archive");
            }

            // Make executable on Unix
            if (!OperatingSystem.IsWindows())
            {
                var chmod = Process.Start("chmod", $"+x {extractedBinary}");
                await chmod.WaitForExitAsync(cancellationToken);
            }

            // Perform platform-specific replacement
            if (OperatingSystem.IsWindows())
            {
                return await UpdateOnWindows(currentExePath, extractedBinary, backupPath);
            }
            else
            {
                return await UpdateOnUnix(currentExePath, extractedBinary, backupPath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update failed: {ex.Message}");

            // Try to restore backup
            if (File.Exists(backupPath))
            {
                try
                {
                    File.Move(backupPath, currentExePath, true);
                    Console.WriteLine("Restored previous version from backup");
                }
                catch
                {
                    Console.Error.WriteLine("Failed to restore backup");
                }
            }

            return false;
        }
        finally
        {
            // Cleanup temp files
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }

    private async Task<bool> UpdateOnWindows(string currentPath, string newPath, string backupPath)
    {
        // Create a PowerShell script to perform the update
        var scriptPath = Path.Combine(Path.GetTempPath(), "update-freshdesk.ps1");
        var script = $@"
Start-Sleep -Seconds 2
Move-Item -Path '{currentPath}' -Destination '{backupPath}' -Force
Move-Item -Path '{newPath}' -Destination '{currentPath}' -Force
Remove-Item -Path '{backupPath}' -Force -ErrorAction SilentlyContinue
& '{currentPath}' --version
Remove-Item -Path $MyInvocation.MyCommand.Path -Force
";
        await File.WriteAllTextAsync(scriptPath, script);

        // Launch the update script
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        Console.WriteLine("Update will complete in a few seconds...");
        Console.WriteLine("Please run 'freshdesk --version' to verify the update.");
        
        // Exit current process
        Environment.Exit(0);
        return true;
    }

    private async Task<bool> UpdateOnUnix(string currentPath, string newPath, string backupPath)
    {
        // Create a shell script to perform the update
        var scriptPath = Path.Combine(Path.GetTempPath(), "update-freshdesk.sh");
        var script = $@"#!/bin/bash
sleep 2
mv '{currentPath}' '{backupPath}'
mv '{newPath}' '{currentPath}'
chmod +x '{currentPath}'
rm -f '{backupPath}'
'{currentPath}' --version
rm -f '$0'
";
        await File.WriteAllTextAsync(scriptPath, script);

        // Make script executable
        var chmod = Process.Start("chmod", $"+x {scriptPath}");
        await chmod.WaitForExitAsync();

        // Launch the update script
        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = scriptPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Console.WriteLine("Update will complete in a few seconds...");
        Console.WriteLine("Please run 'freshdesk --version' to verify the update.");
        
        // Exit current process
        Environment.Exit(0);
        return true;
    }

    private async Task DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var buffer = new byte[8192];
        var totalRead = 0L;
        var lastPercent = -1;

        using var fileStream = File.Create(filePath);
        using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        int bytesRead;
        while ((bytesRead = await httpStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (int)((totalRead * 100) / totalBytes);
                if (percent != lastPercent && percent % 10 == 0)
                {
                    Console.Write($"\rDownloading: {percent}%");
                    lastPercent = percent;
                }
            }
        }

        Console.WriteLine("\rDownload complete!    ");
    }

    private async Task<string?> ExtractBinaryAsync(string archivePath, string extractPath)
    {
        var extension = Path.GetExtension(archivePath).ToLowerInvariant();

        if (extension == ".zip")
        {
            // Extract ZIP (Windows)
            ZipFile.ExtractToDirectory(archivePath, extractPath, true);
        }
        else if (extension == ".gz" && archivePath.Contains(".tar.gz"))
        {
            // Extract tar.gz (Unix)
            var tar = Process.Start(new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{archivePath}\" -C \"{extractPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (tar != null)
            {
                await tar.WaitForExitAsync();
                if (tar.ExitCode != 0)
                {
                    throw new InvalidOperationException($"tar extraction failed with exit code {tar.ExitCode}");
                }
            }
        }
        else
        {
            // Assume it's a raw binary
            return archivePath;
        }

        // Find the binary in the extracted files
        var binaryName = OperatingSystem.IsWindows() ? "freshdesk.exe" : "freshdesk";
        var files = Directory.GetFiles(extractPath, binaryName, SearchOption.AllDirectories);
        
        return files.FirstOrDefault();
    }

    private static string GetPlatformString()
    {
        var os = "";
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x64"
        };

        if (OperatingSystem.IsWindows())
            os = "win";
        else if (OperatingSystem.IsMacOS())
            os = "osx";
        else if (OperatingSystem.IsLinux())
            os = "linux";
        else
            throw new PlatformNotSupportedException();

        return $"{os}-{arch}";
    }

    private static GitHubAsset? FindPlatformAsset(GitHubAsset[] assets, string platform)
    {
        // Look for exact platform match
        return assets.FirstOrDefault(a => 
            a.Name.Contains(platform, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(latestParts.Length, currentParts.Length); i++)
            {
                if (latestParts[i] > currentParts[i]) return true;
                if (latestParts[i] < currentParts[i]) return false;
            }

            return latestParts.Length > currentParts.Length;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UpdateInfo
{
    public required string Version { get; init; }
    public required string DownloadUrl { get; init; }
    public required string FileName { get; init; }
    public string? ReleaseNotes { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
}

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public required string TagName { get; init; }

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset PublishedAt { get; init; }

    [JsonPropertyName("assets")]
    public required GitHubAsset[] Assets { get; init; }
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public required string BrowserDownloadUrl { get; init; }

    [JsonPropertyName("size")]
    public long Size { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(GitHubAsset[]))]
internal partial class UpdateJsonContext : JsonSerializerContext
{
}