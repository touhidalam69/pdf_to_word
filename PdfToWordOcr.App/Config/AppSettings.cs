using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.App.Config;

public sealed record AppDefaults(string Model, int Dpi, string Font, string Language)
{
    public static AppDefaults Default { get; } = new("claude-sonnet-5", 150, "Nirmala UI", "English");
}

/// <summary>Shape of the untracked, gitignored appsettings.local.json override file.</summary>
public sealed record LocalSettings(string? ApiKey);

/// <summary>
/// Per-user preferences stored in %APPDATA%\PdfToWordOcr\settings.json.
/// Never holds the API key — that stays in the DPAPI file or the gitignored
/// local override. Null template = use the built-in default.
/// </summary>
public sealed class UserSettings
{
    public string? WordPromptTemplate { get; set; }
    public string? MarkdownPromptTemplate { get; set; }
}

public static class AppSettings
{
    private const string SettingsFileName = "appsettings.json";
    private const string LocalSettingsFileName = "appsettings.local.json";

    private static readonly string KeyFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PdfToWordOcr",
        "key.dat");

    private static readonly string UserSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PdfToWordOcr",
        "settings.json");

    public static UserSettings LoadUserSettings()
    {
        if (!File.Exists(UserSettingsPath))
        {
            return new UserSettings();
        }

        try
        {
            var json = File.ReadAllText(UserSettingsPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<UserSettings>(json, options) ?? new UserSettings();
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
    }

    public static void SaveUserSettings(UserSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath)!);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        File.WriteAllText(UserSettingsPath, JsonSerializer.Serialize(settings, options));
    }

    /// <summary>The user's template override for the given format, or null for the built-in default.</summary>
    public static string? GetPromptTemplate(OutputFormat format)
    {
        var settings = LoadUserSettings();
        var template = format == OutputFormat.Markdown
            ? settings.MarkdownPromptTemplate
            : settings.WordPromptTemplate;
        return string.IsNullOrWhiteSpace(template) ? null : template;
    }

    public static string? TryGetApiKey()
    {
        var localKey = TryReadLocalApiKey();
        if (!string.IsNullOrWhiteSpace(localKey))
        {
            return localKey;
        }

        var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        return !string.IsNullOrWhiteSpace(envKey) ? envKey : TryReadEncryptedKey();
    }

    public static void SaveApiKey(string apiKey)
    {
        var directory = Path.GetDirectoryName(KeyFilePath)!;
        Directory.CreateDirectory(directory);

        var plainBytes = Encoding.UTF8.GetBytes(apiKey);
        var encryptedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(KeyFilePath, encryptedBytes);
    }

    public static AppDefaults LoadDefaults()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        if (!File.Exists(path))
        {
            return AppDefaults.Default;
        }

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<AppDefaults>(json, options) ?? AppDefaults.Default;
        }
        catch (JsonException)
        {
            return AppDefaults.Default;
        }
    }

    private static string? TryReadLocalApiKey()
    {
        var path = Path.Combine(AppContext.BaseDirectory, LocalSettingsFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<LocalSettings>(json, options)?.ApiKey;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadEncryptedKey()
    {
        if (!File.Exists(KeyFilePath))
        {
            return null;
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(KeyFilePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
