using OllamaSharp;

namespace Ollama.TUI;

internal static class DateTimeFunctions
{
    /// <summary>Gets the current local date and time.</summary>
    [OllamaTool]
    public static string GetCurrentDateTime() => DateTime.Now.ToString("F");
}
