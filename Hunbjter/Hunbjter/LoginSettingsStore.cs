using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hunbjter;

public sealed class LoginSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string settingsPath;

    public LoginSettingsStore()
    {
        settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hunbjter",
            "login-settings.json");
    }

    public LoginSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new LoginSettings();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<LoginSettings>(json) ?? new LoginSettings();
        }
        catch
        {
            return new LoginSettings();
        }
    }

    public void Save(LoginSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static string ProtectPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "";
        }

        var bytes = Encoding.UTF8.GetBytes(password);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string UnprotectPassword(string encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(encryptedPassword))
        {
            return "";
        }

        try
        {
            var encrypted = Convert.FromBase64String(encryptedPassword);
            var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}
