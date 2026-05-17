using System.Text;
using System.Text.Json;

namespace chatAIVintageStoryMod.MCP;

public class ExternalMCPClient
{
    private static readonly HttpClient _sharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _baseUrl;
    private readonly string _serverName;
    private readonly HttpClient _http;
    private int _requestId;

    public string ServerName => _serverName;

    public ExternalMCPClient(string baseUrl, string serverName, HttpClient? http = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _serverName = serverName;
        _http = http ?? _sharedHttp;
    }

    public async Task<IReadOnlyList<MCPTool>> DiscoverToolsAsync()
    {
        try
        {
            var result = await RpcAsync("tools/list", null);
            if (result == null) return new List<MCPTool>();

            if (!result.Value.TryGetProperty("tools", out var toolsEl)) return new List<MCPTool>();

            var tools = new List<MCPTool>();
            foreach (var t in toolsEl.EnumerateArray())
            {
                var tool = ParseTool(t);
                if (tool != null) tools.Add(tool);
            }
            return tools;
        }
        catch
        {
            return new List<MCPTool>();
        }
    }

    public async Task<string> ExecuteToolAsync(string name, Dictionary<string, JsonElement> args)
    {
        try
        {
            var result = await RpcAsync("tools/call", new { name, arguments = args });
            if (result == null) return "No result";

            if (result.Value.TryGetProperty("content", out var content))
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var type) && type.GetString() == "text")
                        return block.GetProperty("text").GetString() ?? "No result";
                }
            }
            return result.Value.GetRawText();
        }
        catch (Exception ex)
        {
            return $"MCP tool error: {ex.Message}";
        }
    }

    private async Task<JsonElement?> RpcAsync(string method, object? @params)
    {
        var id = System.Threading.Interlocked.Increment(ref _requestId);
        string json;
        if (@params == null)
            json = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method });
        else
            json = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params });

        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("error", out _)) return null;
        if (!doc.RootElement.TryGetProperty("result", out var result)) return null;

        return JsonSerializer.Deserialize<JsonElement>(result.GetRawText());
    }

    private MCPTool? ParseTool(JsonElement t)
    {
        if (!t.TryGetProperty("name", out var nameEl)) return null;
        var name = nameEl.GetString();
        if (string.IsNullOrEmpty(name)) return null;

        var desc = t.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
        var parameters = new List<MCPParameter>();

        if (t.TryGetProperty("inputSchema", out var schema) &&
            schema.TryGetProperty("properties", out var props))
        {
            var requiredSet = new HashSet<string>(StringComparer.Ordinal);
            if (schema.TryGetProperty("required", out var req))
                foreach (var r in req.EnumerateArray())
                {
                    var rName = r.GetString();
                    if (rName != null) requiredSet.Add(rName);
                }

            foreach (var prop in props.EnumerateObject())
            {
                parameters.Add(new MCPParameter
                {
                    Name = prop.Name,
                    Type = prop.Value.TryGetProperty("type", out var pt) ? pt.GetString() ?? "string" : "string",
                    Description = prop.Value.TryGetProperty("description", out var pd) ? pd.GetString() ?? "" : "",
                    Required = requiredSet.Contains(prop.Name)
                });
            }
        }

        var capturedName = name;
        return new MCPTool
        {
            Name = capturedName,
            Description = desc,
            Parameters = parameters,
            Handler = args => ExecuteToolAsync(capturedName, args)
        };
    }
}
