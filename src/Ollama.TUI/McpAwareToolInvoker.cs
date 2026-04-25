using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;

namespace Ollama.TUI;

/// <summary>
/// Custom tool invoker that passes argument values directly to tools without
/// converting JsonElement values to strings. The default OllamaSharp invoker
/// calls je.ToString() which corrupts numeric (30000 → "30000") and boolean
/// (true → "True") arguments, breaking MCP tools with typed parameters.
/// </summary>
internal sealed class McpAwareToolInvoker : IToolInvoker
{
    public async Task<ToolResult> InvokeAsync(
        Message.ToolCall toolCall,
        IEnumerable<object> tools,
        CancellationToken cancellationToken)
    {
        var callableTools = tools?.OfType<Tool>().ToArray() ?? [];
        var tool = callableTools.FirstOrDefault(t =>
            (t.Function?.Name ?? string.Empty).Equals(
                toolCall?.Function?.Name ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));

        object? toolResult = null;
        var args = toolCall?.Function?.Arguments;

        if (tool is IAsyncInvokableTool ai)
            toolResult = await ai.InvokeMethodAsync(args).ConfigureAwait(false);
        else if (tool is IInvokableTool i)
            toolResult = i.InvokeMethod(args);

        return new ToolResult(Tool: tool!, ToolCall: toolCall!, Result: toolResult);
    }
}
