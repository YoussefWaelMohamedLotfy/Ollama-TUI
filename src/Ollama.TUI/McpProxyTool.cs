using System.Text.Json;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using OllamaSharp.Models.Chat;
using OllamaSharp.Tools;

using OllamaTool = OllamaSharp.Models.Chat.Tool;

namespace Ollama.TUI;

internal sealed class McpProxyTool : OllamaTool, IAsyncInvokableTool
{
    private readonly McpClient _client;
    private readonly string _toolName;

    private McpProxyTool(McpClient client, string toolName)
    {
        _client = client;
        _toolName = toolName;
    }

    public static McpProxyTool Create(McpClientTool sdkTool, McpClient client)
    {
        return new McpProxyTool(client, sdkTool.Name)
        {
            Type = "function",
            Function = new Function
            {
                Name = sdkTool.Name,
                Description = sdkTool.Description,
                Parameters = BuildParameters(sdkTool.JsonSchema),
            }
        };
    }

    public async Task<object?> InvokeMethodAsync(IDictionary<string, object?>? args)
    {
        var readOnlyArgs = args as IReadOnlyDictionary<string, object?>
            ?? args?.ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = await _client.CallToolAsync(_toolName, readOnlyArgs);

        return string.Join("\n", result.Content
            .OfType<TextContentBlock>()
            .Select(t => t.Text))
            .TrimEnd();
    }

    private static Parameters BuildParameters(JsonElement schema)
    {
        var p = new Parameters
        {
            Type = "object",
            Properties = [],
            Required = [],
        };

        if (!schema.TryGetProperty("properties", out var propsEl)) return p;

        foreach (var prop in propsEl.EnumerateObject())
        {
            var type = prop.Value.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "string" : "string";
            var desc = prop.Value.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
            p.Properties[prop.Name] = new Property { Type = type, Description = desc };
        }

        if (schema.TryGetProperty("required", out var reqEl))
        {
            p.Required = [.. reqEl.EnumerateArray()
                .Where(el => el.ValueKind == JsonValueKind.String)
                .Select(el => el.GetString()!)];
        }

        return p;
    }
}

