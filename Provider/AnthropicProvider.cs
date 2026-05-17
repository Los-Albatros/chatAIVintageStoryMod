using System.Text;
using System.Text.Json;
using chatAIVintageStoryMod.MCP;

namespace chatAIVintageStoryMod.Provider;

public class AnthropicProvider : IAIProvider
{
    private static readonly HttpClient _sharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly HttpClient _http;

    public string Name => "Anthropic";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public AnthropicProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
    {
        _apiKey = apiKey;
        _endpoint = !string.IsNullOrEmpty(endpoint) ? endpoint : "https://api.anthropic.com/v1/messages";
        _model = !string.IsNullOrEmpty(model) ? model : "claude-3-5-sonnet-latest";
        _maxTokens = maxTokens;
        _http = http ?? _sharedHttp;
    }

    public async Task<string> GenerateResponseAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<MCPTool>? tools = null,
        Func<string, Dictionary<string, JsonElement>, Task<string>>? toolExecutor = null)
    {
        var messages = new List<object> { new { role = "user", content = prompt } };
        var responseBody = await SendAsync(messages, systemPrompt, tools);

        if (toolExecutor != null && tools != null && tools.Count > 0)
        {
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement.GetProperty("content");

            var toolUseBlocks = new List<JsonElement>();
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var t) && t.GetString() == "tool_use")
                    toolUseBlocks.Add(block);
            }

            if (toolUseBlocks.Count > 0)
            {
                var toolResults = new List<object>();
                foreach (var toolUseBlock in toolUseBlocks)
                {
                    var toolId = toolUseBlock.GetProperty("id").GetString()!;
                    var toolName = toolUseBlock.GetProperty("name").GetString()!;
                    var toolInput = toolUseBlock.GetProperty("input");
                    var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolInput.GetRawText())
                        ?? new Dictionary<string, JsonElement>();
                    var toolResult = await toolExecutor(toolName, args);
                    toolResults.Add(new { type = "tool_result", tool_use_id = toolId, content = toolResult });
                }

                var contentRaw = content.GetRawText();
                var followUpMessages = new List<object>
                {
                    new { role = "user", content = prompt },
                    new { role = "assistant", content = JsonSerializer.Deserialize<object>(contentRaw)! },
                    new { role = "user", content = toolResults.ToArray() }
                };
                var finalBody = await SendAsync(followUpMessages, systemPrompt, tools: null);
                using var doc2 = JsonDocument.Parse(finalBody);
                return ExtractText(doc2.RootElement);
            }

            return ExtractText(doc.RootElement);
        }

        using var docFinal = JsonDocument.Parse(responseBody);
        return ExtractText(docFinal.RootElement);
    }

    private async Task<string> SendAsync(List<object> messages, string? systemPrompt, IReadOnlyList<MCPTool>? tools)
    {
        // Build payload using anonymous objects; only include optional fields when present
        string json;
        if (tools != null && tools.Count > 0 && !string.IsNullOrEmpty(systemPrompt))
        {
            json = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = _maxTokens,
                system = systemPrompt,
                messages,
                tools = BuildToolDefs(tools)
            });
        }
        else if (tools != null && tools.Count > 0)
        {
            json = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = _maxTokens,
                messages,
                tools = BuildToolDefs(tools)
            });
        }
        else if (!string.IsNullOrEmpty(systemPrompt))
        {
            json = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = _maxTokens,
                system = systemPrompt,
                messages
            });
        }
        else
        {
            json = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = _maxTokens,
                messages
            });
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string ExtractText(JsonElement root)
    {
        var content = root.GetProperty("content");
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text")
                return block.GetProperty("text").GetString() ?? "No response";
        }
        return "No response";
    }

    private static object[] BuildToolDefs(IReadOnlyList<MCPTool> tools)
    {
        var result = new object[tools.Count];
        for (int i = 0; i < tools.Count; i++)
        {
            var t = tools[i];
            var props = new Dictionary<string, object>();
            foreach (var p in t.Parameters)
                props[p.Name] = new { type = p.Type, description = p.Description };

            var required = new List<string>();
            foreach (var p in t.Parameters)
                if (p.Required) required.Add(p.Name);

            result[i] = new
            {
                name = t.Name,
                description = t.Description,
                input_schema = new
                {
                    type = "object",
                    properties = props,
                    required = required.ToArray()
                }
            };
        }
        return result;
    }
}
