using System.Security.Cryptography;
using System.Text;

namespace Hunbjter;

public sealed class LoginSettingsStore
{
    private readonly string settingsPath = JsonFileStore.ResolvePath("login-settings.json");

    public string? LastLoadFailure { get; private set; }

    public LoginSettings Load()
    {
        var settings = JsonFileStore.Load(settingsPath, static () => new LoginSettings(), out var failure);
        LastLoadFailure = failure;
        return settings;
    }

    public void Save(LoginSettings settings)
    {
        JsonFileStore.Save(settingsPath, settings);
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
