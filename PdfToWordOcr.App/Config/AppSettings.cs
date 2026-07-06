using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PdfToWordOcr.App.Config;

public sealed record AppDefaults(string Model, int Dpi, string Font, string Language)
{
    public static AppDefaults Default { get; } = new("claude-sonnet-5", 150, "Nirmala UI", "English");
}

public static class AppSettings
{
    private const string SettingsFileName = "appsettings.json";

    private static readonly string KeyFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PdfToWordOcr",
        "key.dat");

    public static string? TryGetApiKey()
    {
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
