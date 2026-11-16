using System.Text.Json;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>
/// Persists and loads application settings as JSON in LocalAppData.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XenomOptimizer");
        Directory.CreateDirectory(appDir);
        _settingsPath = Path.Combine(appDir, "settings.json");
        Load();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }
}
