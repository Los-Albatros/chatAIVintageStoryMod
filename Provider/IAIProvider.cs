using System.Text.Json;
using chatAIVintageStoryMod.MCP;

namespace chatAIVintageStoryMod.Provider;

public interface IAIProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    Task<string> GenerateResponseAsync(
        string prompt,
        string? systemPrompt = null,
        IReadOnlyList<MCPTool>? tools = null,
        Func<string, Dictionary<string, JsonElement>, Task<string>>? toolExecutor = null);
}
