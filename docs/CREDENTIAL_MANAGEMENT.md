# Credential Management Guide

## Overview
Secure credential storage and management for the Freshdesk CLI, following industry best practices.

## Storage Hierarchy

Credentials are checked in the following order (highest to lowest priority):

1. **Environment Variables** - Runtime override
2. **Config File** - Persistent storage
3. **Future: OS Keyring** - Secure storage (optional enhancement)

## Environment Variables

### Supported Variables
```bash
# Required
export FRESHDESK_DOMAIN="acme.freshdesk.com"
export FRESHDESK_API_KEY="sk_live_xxxxxxxxxxxx"

# Optional
export FRESHDESK_DOWNLOAD_PATH="~/Downloads/freshdesk"
export FRESHDESK_OUTPUT_FORMAT="json"
export FRESHDESK_MAX_CONCURRENT_DOWNLOADS="3"
```

### Usage Examples
```bash
# One-time use
FRESHDESK_DOMAIN=acme.freshdesk.com FRESHDESK_API_KEY=sk_live_xxx freshdesk tickets list

# Session persistence
export FRESHDESK_DOMAIN="acme.freshdesk.com"
export FRESHDESK_API_KEY="sk_live_xxxxxxxxxxxx"
freshdesk tickets list
freshdesk tickets get 123

# Docker usage
docker run -e FRESHDESK_DOMAIN=acme.freshdesk.com \
           -e FRESHDESK_API_KEY=sk_live_xxx \
           freshdesk-cli tickets list
```

### CI/CD Integration
```yaml
# GitHub Actions
- name: Run Freshdesk CLI
  env:
    FRESHDESK_DOMAIN: ${{ secrets.FRESHDESK_DOMAIN }}
    FRESHDESK_API_KEY: ${{ secrets.FRESHDESK_API_KEY }}
  run: |
    freshdesk tickets list --status open
```

## File-Based Storage

### Config File Location
- **Linux/macOS**: `~/.freshdesk/config.json`
- **Windows**: `%USERPROFILE%\.freshdesk\config.json`

### File Structure
```json
{
  "domain": "acme.freshdesk.com",
  "api_key": "sk_live_xxxxxxxxxxxx",
  "default_download_path": "./attachments",
  "max_concurrent_downloads": 3,
  "auto_retry": true,
  "retry_count": 3,
  "output_format": "table"
}
```

### File Permissions

#### Linux/macOS
```bash
# Automatically set by CLI
chmod 600 ~/.freshdesk/config.json

# Verify permissions
ls -la ~/.freshdesk/config.json
# -rw------- 1 user user 156 Jan 15 10:30 config.json
```

#### Windows
```powershell
# Remove inheritance and set current user only
$path = "$env:USERPROFILE\.freshdesk\config.json"
$acl = Get-Acl $path
$acl.SetAccessRuleProtection($true, $false)
$permission = $env:USERNAME, "FullControl", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $path $acl
```

## Setting Credentials

### Interactive Setup
```bash
# Basic setup
freshdesk auth login acme

# CLI will prompt:
# API Key: [hidden input]

# Non-interactive setup
freshdesk auth login acme --api-key sk_live_xxxxxxxxxxxx
```

### Validation Process
1. Normalize domain (add .freshdesk.com if needed)
2. Validate API key format (minimum length, character set)
3. Test API connection with minimal request
4. Save configuration if successful

### Setup Wizard (Future Enhancement)
```bash
freshdesk setup

# Interactive prompts:
# Welcome to Freshdesk CLI Setup
# 
# Freshdesk subdomain (e.g., 'acme' for acme.freshdesk.com): acme
# API Key (hidden): ****
# Default download directory [./attachments]: ~/Downloads/freshdesk
# Output format (table/json/yaml) [table]: json
# 
# Testing connection... ✓
# Configuration saved successfully!
```

## Security Best Practices

### 1. API Key Generation
```markdown
To generate an API key:
1. Log in to your Freshdesk account
2. Click on your profile picture → Profile Settings
3. Navigate to the API Key section
4. Copy your API key (keep it secure!)

Best practices:
- Generate separate keys for different environments
- Rotate keys regularly (every 90 days)
- Never commit keys to version control
- Use read-only keys when possible
```

### 2. Secure Input Handling
```csharp
public static string ReadSecureString(string prompt)
{
    Console.Write(prompt);
    var password = new StringBuilder();
    
    ConsoleKeyInfo key;
    do
    {
        key = Console.ReadKey(intercept: true);
        
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Remove(password.Length - 1, 1);
            Console.Write("\b \b");
        }
        else if (key.Key != ConsoleKey.Enter && !char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write("*");
        }
    } while (key.Key != ConsoleKey.Enter);
    
    Console.WriteLine();
    return password.ToString();
}
```

### 3. Memory Security
```csharp
public sealed class SecureApiKey : IDisposable
{
    private readonly SecureString _secureString;
    private bool _disposed;
    
    public SecureApiKey(string apiKey)
    {
        _secureString = new SecureString();
        foreach (char c in apiKey)
        {
            _secureString.AppendChar(c);
        }
        _secureString.MakeReadOnly();
        
        // Clear original string from memory
        apiKey = new string('\0', apiKey.Length);
    }
    
    public string GetDecrypted()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SecureApiKey));
        
        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(_secureString);
            return Marshal.PtrToStringUni(ptr)!;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _secureString?.Dispose();
            _disposed = true;
        }
    }
}
```

### 4. Audit Logging
```csharp
public class AuditLogger
{
    public void LogCredentialAccess(string action, bool success)
    {
        var logEntry = new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = action,
            Success = success,
            User = Environment.UserName,
            Machine = Environment.MachineName,
            Process = Process.GetCurrentProcess().ProcessName
        };
        
        // Log to secure location (not including actual credentials)
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".freshdesk",
            "audit.log");
        
        File.AppendAllText(logPath, JsonSerializer.Serialize(logEntry) + "\n");
    }
}
```

## Credential Rotation

### Manual Rotation
```bash
# 1. Generate new API key in Freshdesk UI
# 2. Update credentials
freshdesk auth login acme --api-key sk_live_new_key_xxx

# 3. Verify new credentials work
freshdesk auth status
freshdesk tickets list --page 1 --per-page 1

# 4. Revoke old API key in Freshdesk UI
```

### Automated Rotation Script
```bash
#!/bin/bash
# rotate-credentials.sh

# Check if credentials are older than 90 days
CONFIG_FILE="$HOME/.freshdesk/config.json"
if [ -f "$CONFIG_FILE" ]; then
    LAST_MODIFIED=$(stat -f %m "$CONFIG_FILE" 2>/dev/null || stat -c %Y "$CONFIG_FILE" 2>/dev/null)
    CURRENT_TIME=$(date +%s)
    AGE_DAYS=$(( ($CURRENT_TIME - $LAST_MODIFIED) / 86400 ))
    
    if [ $AGE_DAYS -gt 90 ]; then
        echo "WARNING: Credentials are $AGE_DAYS days old"
        echo "Please rotate your API key:"
        echo "1. Generate new key at https://YOUR_DOMAIN.freshdesk.com/profile"
        echo "2. Run: freshdesk auth login YOUR_DOMAIN"
    fi
fi
```

## Multi-Account Support (Future Enhancement)

### Profile-Based Configuration
```bash
# Set up multiple profiles
freshdesk auth login acme --profile production
freshdesk auth login acme-dev --profile development

# Use specific profile
freshdesk tickets list --profile production
FRESHDESK_PROFILE=development freshdesk tickets list

# Config structure
~/.freshdesk/
├── config.json          # Default profile
├── profiles/
│   ├── production.json
│   └── development.json
```

### Profile Switching
```csharp
public class ProfileManager
{
    private readonly string _profilesDirectory;
    
    public ProfileManager()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".freshdesk");
        
        _profilesDirectory = Path.Combine(configDir, "profiles");
    }
    
    public ConfigFile LoadProfile(string? profileName = null)
    {
        // Check for profile override
        profileName ??= Environment.GetEnvironmentVariable("FRESHDESK_PROFILE");
        
        if (string.IsNullOrEmpty(profileName))
        {
            // Load default config
            return LoadDefaultConfig();
        }
        
        var profilePath = Path.Combine(_profilesDirectory, $"{profileName}.json");
        if (!File.Exists(profilePath))
        {
            throw new ProfileNotFoundException(profileName);
        }
        
        return LoadConfigFromFile(profilePath);
    }
}
```

## OS Keyring Integration (Future Enhancement)

### Platform-Specific Implementation
```csharp
public interface ISecureStorage
{
    Task<string?> GetSecretAsync(string key);
    Task SetSecretAsync(string key, string value);
    Task DeleteSecretAsync(string key);
}

// macOS Keychain
public class MacOSKeychain : ISecureStorage
{
    public async Task<string?> GetSecretAsync(string key)
    {
        var result = await RunCommand("security", 
            $"find-generic-password -s 'freshdesk-cli' -a '{key}' -w");
        return result.Success ? result.Output : null;
    }
    
    public async Task SetSecretAsync(string key, string value)
    {
        await RunCommand("security",
            $"add-generic-password -s 'freshdesk-cli' -a '{key}' -w '{value}' -U");
    }
}

// Windows Credential Manager
public class WindowsCredentialManager : ISecureStorage
{
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool CredWrite(ref CREDENTIAL credential, uint flags);
    
    public async Task SetSecretAsync(string key, string value)
    {
        var credential = new CREDENTIAL
        {
            TargetName = $"freshdesk-cli:{key}",
            CredentialBlob = Encoding.UTF8.GetBytes(value),
            CredentialBlobSize = value.Length * 2,
            Type = CRED_TYPE.GENERIC,
            Persist = CRED_PERSIST.LOCAL_MACHINE
        };
        
        if (!CredWrite(ref credential, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}

// Linux Secret Service
public class LinuxSecretService : ISecureStorage
{
    public async Task<string?> GetSecretAsync(string key)
    {
        // Use libsecret via P/Invoke or external process
        var result = await RunCommand("secret-tool",
            $"lookup application freshdesk-cli key {key}");
        return result.Success ? result.Output : null;
    }
}
```

### Usage with Fallback
```csharp
public class HybridConfigManager
{
    private readonly ISecureStorage? _secureStorage;
    private readonly ConfigManager _fileManager;
    
    public HybridConfigManager()
    {
        _fileManager = new ConfigManager();
        _secureStorage = CreateSecureStorage();
    }
    
    public async Task<ConfigFile?> LoadAsync()
    {
        // 1. Check environment variables
        var envConfig = LoadFromEnvironment();
        if (envConfig != null)
            return envConfig;
        
        // 2. Try secure storage
        if (_secureStorage != null)
        {
            var apiKey = await _secureStorage.GetSecretAsync("api_key");
            var domain = await _secureStorage.GetSecretAsync("domain");
            
            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(domain))
            {
                return new ConfigFile { ApiKey = apiKey, Domain = domain };
            }
        }
        
        // 3. Fall back to file
        return _fileManager.Load();
    }
    
    private ISecureStorage? CreateSecureStorage()
    {
        if (OperatingSystem.IsMacOS())
            return new MacOSKeychain();
        if (OperatingSystem.IsWindows())
            return new WindowsCredentialManager();
        if (OperatingSystem.IsLinux())
            return new LinuxSecretService();
        
        return null;
    }
}
```

## Troubleshooting

### Common Issues

#### 1. Permission Denied
```bash
Error: Permission denied accessing config file

# Fix permissions
chmod 600 ~/.freshdesk/config.json
```

#### 2. Invalid API Key
```bash
Error: Authentication failed. Check your API key.

# Verify API key format
# Should be 20+ characters, alphanumeric

# Re-authenticate
freshdesk auth logout
freshdesk auth login acme
```

#### 3. Config File Corruption
```bash
Error: Invalid config file format

# Backup and recreate
mv ~/.freshdesk/config.json ~/.freshdesk/config.json.bak
freshdesk auth login acme
```

#### 4. Environment Variable Not Working
```bash
# Check variable is exported
echo $FRESHDESK_API_KEY

# Ensure no trailing spaces
export FRESHDESK_API_KEY="$(echo $FRESHDESK_API_KEY | tr -d ' ')"
```

## Security Checklist

- [ ] API keys are never logged or displayed in plain text
- [ ] Config file has restricted permissions (600)
- [ ] Credentials are validated before saving
- [ ] Memory containing secrets is cleared after use
- [ ] Error messages don't reveal sensitive information
- [ ] Audit logging tracks credential access
- [ ] Documentation includes rotation procedures
- [ ] CI/CD uses secure secret management
- [ ] Default config location is in user home directory
- [ ] Credentials are masked in all output