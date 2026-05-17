using System.Text.Json;
using chatAIVintageStoryMod.MCP;
using chatAIVintageStoryMod.Provider;

namespace chatAIVintageStoryMod;

public class AIService
{
    private readonly IAIProvider _provider;
    private readonly string? _systemPrompt;
    private readonly MCPToolRegistry? _registry;

    public AIService(IAIProvider provider, string? systemPrompt = null, MCPToolRegistry? registry = null)
    {
        _provider = provider;
        _systemPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt;
        _registry = registry;
    }

    public bool IsReady => _provider.IsAvailable;
    public string ProviderName => _provider.Name;

    public Task<string> AskAsync(string question)
    {
        var tools = _registry?.Count > 0 ? _registry.GetAll() : null;
        Func<string, Dictionary<string, JsonElement>, Task<string>>? executor =
            _registry?.Count > 0 ? _registry.ExecuteAsync : null;
        return _provider.GenerateResponseAsync(question, _systemPrompt, tools, executor);
    }
}
