namespace Ollama.TUI;

public class AppSettings
{
    public string OllamaServerUrl { get; set; } = "http://localhost:11434";
    public string Theme { get; set; } = "Default";
    public string? SelectedModel { get; set; }
}
