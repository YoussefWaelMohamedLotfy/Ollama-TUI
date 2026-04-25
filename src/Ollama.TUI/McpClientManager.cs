using ModelContextProtocol.Client;

using OllamaTool = OllamaSharp.Models.Chat.Tool;

namespace Ollama.TUI;

internal sealed class McpClientManager : IAsyncDisposable
{
    private readonly List<McpClient> _clients = [];
    private volatile List<OllamaTool> _mcpTools = [];

    public IReadOnlyList<OllamaTool> McpTools => _mcpTools;

    public async Task RefreshAsync(List<McpServerConfig> servers)
    {
        foreach (var c in _clients)
            await c.DisposeAsync();
        _clients.Clear();

        var newTools = new List<OllamaTool>();

        foreach (var server in servers)
        {
            if (!server.Enabled) continue;

            try
            {
                IClientTransport transport = server.Transport == McpTransportType.Stdio
                    ? CreateStdioTransport(server)
                    : CreateHttpTransport(server);

                var client = await McpClient.CreateAsync(transport);
                var sdkTools = await client.ListToolsAsync();

                foreach (var sdkTool in sdkTools)
                    newTools.Add(McpProxyTool.Create(sdkTool, client));

                _clients.Add(client);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP] Failed to connect '{server.Name}': {ex.Message}");
            }
        }

        _mcpTools = newTools;
    }

    private static StdioClientTransport CreateStdioTransport(McpServerConfig server)
    {
        var args = server.Arguments?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var envVars = ParseEnvVars(server.EnvironmentVariables);

        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = server.Command ?? "",
            Arguments = args,
            EnvironmentVariables = envVars,
            Name = server.Name,
        });
    }

    private static HttpClientTransport CreateHttpTransport(McpServerConfig server)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = new Uri(string.IsNullOrWhiteSpace(server.Url) ? "http://localhost" : server.Url),
        };

        var headers = ParseHeaders(server.Headers);
        if (headers is null)
            return new HttpClientTransport(options);

        var httpClient = new HttpClient();
        foreach (var (name, value) in headers)
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(name, value);

        return new HttpClientTransport(options, httpClient, ownsHttpClient: true);
    }

    /// <summary>Parses "KEY=VALUE;KEY2=VALUE2" environment variable pairs.</summary>
    private static Dictionary<string, string?>? ParseEnvVars(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx < 1) continue;
            result[part[..idx].Trim()] = part[(idx + 1)..].Trim();
        }
        return result.Count == 0 ? null : result;
    }

    /// <summary>Parses "Name:Value;Name2:Value2" HTTP headers.</summary>
    private static Dictionary<string, string>? ParseHeaders(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf(':');
            if (idx < 1) continue;
            result[part[..idx].Trim()] = part[(idx + 1)..].Trim();
        }
        return result.Count == 0 ? null : result;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _clients)
            await c.DisposeAsync();
        _clients.Clear();
    }
}
