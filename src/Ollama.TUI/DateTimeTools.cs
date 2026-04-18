using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;

namespace Ollama.TUI;

internal sealed class DateTimeTool : Tool, IInvokableTool
{
    public DateTimeTool()
    {
        Function = new Function
        {
            Name = "get_current_datetime",
            Description = "Gets the current local date and time.",
            Parameters = new Parameters
            {
                Properties = [],
                Required = []
            }
        };
    }

    public static string GetCurrentDateTime() => DateTime.Now.ToString("F");

    public object? InvokeMethod(IDictionary<string, object?>? args) => GetCurrentDateTime();
}
