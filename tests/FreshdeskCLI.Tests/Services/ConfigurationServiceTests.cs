using System.Text.Json;
using FreshdeskCLI.Models;
using FreshdeskCLI.Services;

namespace FreshdeskCLI.Tests.Services;

public class ConfigurationServiceTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly ConfigurationService _service;

    public ConfigurationServiceTests()
    {
        // Create a temp directory for test configs
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"freshdesk-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testConfigPath);

        // Override the config path for testing
        Environment.SetEnvironmentVariable("FRESHDESK_CONFIG_PATH", _testConfigPath);
        _service = new ConfigurationService();
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testConfigPath))
        {
            Directory.Delete(_testConfigPath, true);
        }
        Environment.SetEnvironmentVariable("FRESHDESK_CONFIG_PATH", null);
    }

    [Fact]
    public async Task LoadConfigAsync_ReturnsNull_WhenNoConfigExists()
    {
        // Act
        var config = await _service.LoadConfigAsync();

        // Assert
        Assert.Null(config);
    }

    [Fact]
    public async Task SaveConfigAsync_CreatesConfigFile()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "testdomain",
            ApiKey = "test-api-key",
            ProfileName = "default"
        };

        // Act
        await _service.SaveConfigAsync(config);

        // Assert
        var configFile = Path.Combine(_testConfigPath, "config.json");
        Assert.True(File.Exists(configFile));

        var savedConfig = await _service.LoadConfigAsync();
        Assert.NotNull(savedConfig);
        Assert.Equal("testdomain", savedConfig.Domain);
        Assert.Equal("test-api-key", savedConfig.ApiKey);
    }

    [Fact]
    public async Task LoadConfigAsync_ReadsFromEnvironmentVariables()
    {
        // Arrange
        // Use a fresh service instance with a clean directory
        var cleanPath = Path.Combine(Path.GetTempPath(), $"freshdesk-test-clean-{Guid.NewGuid()}");
        Directory.CreateDirectory(cleanPath);
        Environment.SetEnvironmentVariable("FRESHDESK_CONFIG_PATH", cleanPath);

        Environment.SetEnvironmentVariable("FRESHDESK_DOMAIN", "env-domain");
        Environment.SetEnvironmentVariable("FRESHDESK_API_KEY", "env-api-key");

        try
        {
            var service = new ConfigurationService();

            // Act
            var config = await service.LoadConfigAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal("env-domain", config.Domain);
            Assert.Equal("env-api-key", config.ApiKey);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FRESHDESK_DOMAIN", null);
            Environment.SetEnvironmentVariable("FRESHDESK_API_KEY", null);
            Environment.SetEnvironmentVariable("FRESHDESK_CONFIG_PATH", _testConfigPath);
            if (Directory.Exists(cleanPath))
                Directory.Delete(cleanPath, true);
        }
    }

    [Fact]
    public async Task LoadConfigAsync_EnvironmentVariablesOverrideFile()
    {
        // Arrange
        var fileConfig = new FreshdeskConfig
        {
            Domain = "file-domain",
            ApiKey = "file-api-key",
            ProfileName = "default"
        };
        await _service.SaveConfigAsync(fileConfig);

        // Both env vars must be set for environment config to be used
        Environment.SetEnvironmentVariable("FRESHDESK_DOMAIN", "env-domain");
        Environment.SetEnvironmentVariable("FRESHDESK_API_KEY", "env-api-key");

        try
        {
            // Act
            var config = await _service.LoadConfigAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal("env-domain", config.Domain); // From env
            Assert.Equal("env-api-key", config.ApiKey); // From env
        }
        finally
        {
            Environment.SetEnvironmentVariable("FRESHDESK_DOMAIN", null);
            Environment.SetEnvironmentVariable("FRESHDESK_API_KEY", null);
        }
    }

    [Fact]
    public void FreshdeskConfig_IsValid_ReturnsTrueForValidConfig()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "testdomain",
            ApiKey = "test-api-key"
        };

        // Act & Assert
        Assert.True(config.IsValid);
        Assert.Equal("https://testdomain.freshdesk.com", config.BaseUrl);
    }

    [Fact]
    public void FreshdeskConfig_IsValid_ReturnsFalseForInvalidConfig()
    {
        // Arrange
        var configNoDomain = new FreshdeskConfig { ApiKey = "test-api-key" };
        var configNoApiKey = new FreshdeskConfig { Domain = "testdomain" };
        var configEmpty = new FreshdeskConfig();

        // Act & Assert
        Assert.False(configNoDomain.IsValid);
        Assert.False(configNoApiKey.IsValid);
        Assert.False(configEmpty.IsValid);
    }

    [Fact]
    public async Task SaveConfigAsync_CreatesConfigDirectory()
    {
        // Arrange
        var testPath = Path.Combine(_testConfigPath, "subdir");
        Environment.SetEnvironmentVariable("FRESHDESK_CONFIG_PATH", testPath);
        var service = new ConfigurationService();

        var config = new FreshdeskConfig
        {
            Domain = "testdomain",
            ApiKey = "test-api-key",
            ProfileName = "default"
        };

        // Act
        await service.SaveConfigAsync(config);

        // Assert
        Assert.True(Directory.Exists(testPath));
        var configFile = Path.Combine(testPath, "config.json");
        Assert.True(File.Exists(configFile));
    }
}