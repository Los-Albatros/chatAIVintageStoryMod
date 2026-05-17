using System.Text;
using System.Text.Json;
using chatAIVintageStoryMod.MCP;

namespace chatAIVintageStoryMod.Provider;

public class OllamaProvider : IAIProvider
{
    private static readonly HttpClient _sharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _endpoint;
    private readonly string _model;
    private readonly HttpClient _http;

    public string Name => "Ollama";
    public bool IsAvailable => !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_model);

    public OllamaProvider(string endpoint, string model, HttpClient? http = null)
    {
        _endpoint = endpoint;
        _model = model;
        _http = http ?? _sharedHttp;
    }

    public async Task<string> GenerateResponseAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<MCPTool>? tools = null,
        Func<string, Dictionary<string, JsonElement>, Task<string>>? toolExecutor = null)
    {
        var effectiveSystem = BuildSystemPrompt(systemPrompt, tools);
        var response = await CallOllama(prompt, effectiveSystem);

        if (toolExecutor != null && tools != null && tools.Count > 0)
        {
            var toolCall = TryParseToolCall(response);
            if (toolCall != null)
            {
                var toolResult = await toolExecutor(toolCall.Value.Name, toolCall.Value.Args);
                var followUp = $"Tool '{toolCall.Value.Name}' returned: {toolResult}\n\nNow answer the original question: {prompt}";
                return await CallOllama(followUp, effectiveSystem);
            }
        }

        return response;
    }

    private async Task<string> CallOllama(string prompt, string? system)
    {
        string json;
        if (string.IsNullOrEmpty(system))
            json = JsonSerializer.Serialize(new { model = _model, prompt, stream = false });
        else
            json = JsonSerializer.Serialize(new { model = _model, prompt, system, stream = false });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(_endpoint, content);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("response", out var val)
            ? val.GetString() ?? "No response"
            : "No response";
    }

    private static string? BuildSystemPrompt(string? baseSystem, IReadOnlyList<MCPTool>? tools)
    {
        if (tools == null || tools.Count == 0) return baseSystem;

        var toolLines = new System.Text.StringBuilder();
        foreach (var t in tools)
        {
            string paramDesc = t.Parameters.Count == 0 ? "none"
                : string.Join(", ", t.Parameters.ConvertAll(p => $"{p.Name} ({p.Type}): {p.Description}"));
            toolLines.AppendLine($"- {t.Name}: {t.Description}. Parameters: {paramDesc}");
        }

        var toolInstructions = $"You have access to the following tools. To call a tool, respond ONLY with valid JSON:\n{{\"tool_call\":{{\"name\":\"<tool_name>\",\"arguments\":{{<params>}}}}}}\nAfter receiving the tool result, give your final answer normally.\n\nAvailable tools:\n{toolLines}";

        return string.IsNullOrEmpty(baseSystem)
            ? toolInstructions
            : baseSystem + "\n\n" + toolInstructions;
    }

    private static (string Name, Dictionary<string, JsonElement> Args)? TryParseToolCall(string response)
    {
        var trimmed = response.Trim();
        if (!trimmed.StartsWith("{")) return null;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (!doc.RootElement.TryGetProperty("tool_call", out var tc)) return null;
            var name = tc.GetProperty("name").GetString();
            if (string.IsNullOrEmpty(name)) return null;
            Dictionary<string, JsonElement> args;
            if (tc.TryGetProperty("arguments", out var argsEl))
                args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsEl.GetRawText()) ?? new Dictionary<string, JsonElement>();
            else
                args = new Dictionary<string, JsonElement>();
            return (name, args);
        }
        catch
        {
            return null;
        }
    }
}
