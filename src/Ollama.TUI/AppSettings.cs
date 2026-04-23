namespace Ollama.TUI;

public class AppSettings
{
    public string OllamaServerUrl { get; set; } = "http://localhost:11434";
    public string Theme { get; set; } = "Default";
    public string? SelectedModel { get; set; }

    /// <summary>
    /// Stores enabled/disabled state for each tool by its function name.
    /// A missing entry means the tool is enabled by default.
    /// </summary>
    public Dictionary<string, bool> McpToolsEnabled { get; set; } = [];
}
