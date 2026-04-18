using System.Text.Json;

using XenoAtom.Terminal.UI.Styling;

namespace Ollama.TUI;

internal static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ollama-tui",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
        File.WriteAllText(SettingsPath, json);
    }

    public static Theme ToTheme(string name) => name switch
    {
        "Light" => Theme.DefaultLight,
        "Terminal" => Theme.Terminal,
        _ => Theme.Default,
    };
}
