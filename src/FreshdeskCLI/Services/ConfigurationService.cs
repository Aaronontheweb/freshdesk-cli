using System.Text.Json;
using FreshdeskCLI.Models;

namespace FreshdeskCLI.Services;

public interface IConfigurationService
{
    Task<FreshdeskConfig?> LoadConfigAsync(CancellationToken cancellationToken = default);
    Task SaveConfigAsync(FreshdeskConfig config, CancellationToken cancellationToken = default);
    Task<bool> ConfigExistsAsync(CancellationToken cancellationToken = default);
    string GetConfigPath();
}

public sealed class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;

    public ConfigurationService()
    {
        // Allow override for testing
        var configPath = Environment.GetEnvironmentVariable("FRESHDESK_CONFIG_PATH");
        if (!string.IsNullOrEmpty(configPath))
        {
            _configPath = Path.Combine(configPath, "config.json");
        }
        else
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(homeDir, ".freshdesk");
            _configPath = Path.Combine(configDir, "config.json");
        }
    }

    public async Task<FreshdeskConfig?> LoadConfigAsync(CancellationToken cancellationToken = default)
    {
        // First check environment variables
        var envConfig = LoadFromEnvironment();
        if (envConfig != null)
            return envConfig;

        // Then check config file
        if (!File.Exists(_configPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            var configFile = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.ConfigFile);

            if (configFile?.Profiles == null || configFile.Profiles.Count == 0)
                return null;

            // Get default profile or first profile
            var profile = configFile.DefaultProfile != null && configFile.Profiles.ContainsKey(configFile.DefaultProfile)
                ? configFile.Profiles[configFile.DefaultProfile]
                : configFile.Profiles.First().Value;

            return profile;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveConfigAsync(FreshdeskConfig config, CancellationToken cancellationToken = default)
    {
        var configDir = Path.GetDirectoryName(_configPath);
        if (string.IsNullOrEmpty(configDir))
        {
            throw new InvalidOperationException($"Invalid configuration path: {_configPath}");
        }

        if (!Directory.Exists(configDir))
        {
            try
            {
                Directory.CreateDirectory(configDir);

                // Set permissions to user-only on Unix systems
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(configDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to create configuration directory: {configDir}", ex);
            }
        }

        // Load existing config or create new
        ConfigFile? configFile = null;
        if (File.Exists(_configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
                configFile = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.ConfigFile);
            }
            catch (JsonException)
            {
                configFile = null;
            }
        }

        configFile ??= new ConfigFile { Profiles = new Dictionary<string, FreshdeskConfig>() };

        // Update default profile
        var profileName = config.ProfileName ?? "default";
        configFile.Profiles[profileName] = config;
        configFile.DefaultProfile = profileName;

        // Save to file
        var updatedJson = JsonSerializer.Serialize(configFile, FreshdeskJsonContext.Default.ConfigFile);
        await File.WriteAllTextAsync(_configPath, updatedJson, cancellationToken);

        // Set file permissions to user-only on Unix systems
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public Task<bool> ConfigExistsAsync(CancellationToken cancellationToken = default)
    {
        // Check environment variables first
        if (LoadFromEnvironment() != null)
            return Task.FromResult(true);

        return Task.FromResult(File.Exists(_configPath));
    }

    public string GetConfigPath() => _configPath;

    private static FreshdeskConfig? LoadFromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable("FRESHDESK_API_KEY");
        var domain = Environment.GetEnvironmentVariable("FRESHDESK_DOMAIN");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(domain))
            return null;

        return new FreshdeskConfig
        {
            ApiKey = apiKey,
            Domain = domain,
            ProfileName = "environment"
        };
    }
}