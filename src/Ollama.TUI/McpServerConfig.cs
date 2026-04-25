namespace Ollama.TUI;

public enum McpTransportType
{
    Stdio = 0,
    Http = 1,
}

public class McpServerConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public McpTransportType Transport { get; set; } = McpTransportType.Stdio;
    public bool Enabled { get; set; } = true;

    // Stdio transport settings
    public string? Command { get; set; }
    public string? Arguments { get; set; }

    /// <summary>Semicolon-separated KEY=VALUE pairs, e.g. "FOO=bar;BAZ=qux".</summary>
    public string? EnvironmentVariables { get; set; }

    // Http transport settings
    public string? Url { get; set; }

    /// <summary>Semicolon-separated Name:Value pairs, e.g. "Authorization:Bearer token".</summary>
    public string? Headers { get; set; }
}
