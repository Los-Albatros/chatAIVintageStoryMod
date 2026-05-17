using System.Text;
using System.Text.Json;
using chatAIVintageStoryMod.MCP;

namespace chatAIVintageStoryMod.Provider;

public abstract class OpenAICompatibleProvider : IAIProvider
{
    private static readonly HttpClient _sharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxTokens;
    private readonly HttpClient _http;

    public abstract string Name { get; }
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    protected OpenAICompatibleProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
    {
        _apiKey = apiKey;
        _endpoint = endpoint;
        _model = model;
        _temperature = temperature;
        _maxTokens = maxTokens;
        _http = http ?? _sharedHttp;
    }

    public async Task<string> GenerateResponseAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<MCPTool>? tools = null,
        Func<string, Dictionary<string, JsonElement>, Task<string>>? toolExecutor = null)
    {
        var messages = BuildMessages(prompt, systemPrompt);
        var responseBody = await SendHttpAsync(messages, tools);

        if (toolExecutor != null && tools != null && tools.Count > 0)
        {
            using var doc = JsonDocument.Parse(responseBody);
            var messageEl = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

            if (messageEl.TryGetProperty("tool_calls", out var toolCallsEl))
            {
                // Build assistant message for follow-up
                var assistantMsgRaw = messageEl.GetRawText();
                var followUpMessages = new List<object>(messages)
                {
                    JsonSerializer.Deserialize<object>(assistantMsgRaw)!
                };

                foreach (var call in toolCallsEl.EnumerateArray())
                {
                    var callId = call.GetProperty("id").GetString()!;
                    var funcName = call.GetProperty("function").GetProperty("name").GetString()!;
                    var argsStr = call.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                    var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsStr) ?? new Dictionary<string, JsonElement>();
                    var result = await toolExecutor(funcName, args);
                    followUpMessages.Add(new { role = "tool", tool_call_id = callId, content = result });
                }

                var finalBody = await SendHttpAsync(followUpMessages, tools: null);
                using var doc2 = JsonDocument.Parse(finalBody);
                return doc2.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "No response";
            }

            // No tool_calls in response — reuse the already-parsed doc
            return messageEl
                .GetProperty("content")
                .GetString() ?? "No response";
        }

        using var docFinal = JsonDocument.Parse(responseBody);
        return docFinal.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "No response";
    }

    private List<object> BuildMessages(string prompt, string? systemPrompt)
    {
        var messages = new List<object>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });
        messages.Add(new { role = "user", content = prompt });
        return messages;
    }

    private async Task<string> SendHttpAsync(List<object> messages, IReadOnlyList<MCPTool>? tools)
    {
        object payload;
        if (tools != null && tools.Count > 0)
        {
            payload = new
            {
                model = _model,
                messages,
                temperature = _temperature,
                max_tokens = _maxTokens,
                tools = BuildToolDefinitions(tools)
            };
        }
        else
        {
            payload = new
            {
                model = _model,
                messages,
                temperature = _temperature,
                max_tokens = _maxTokens
            };
        }

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static object[] BuildToolDefinitions(IReadOnlyList<MCPTool> tools)
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
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = new
                    {
                        type = "object",
                        properties = props,
                        required = required.ToArray()
                    }
                }
            };
        }
        return result;
    }
}
