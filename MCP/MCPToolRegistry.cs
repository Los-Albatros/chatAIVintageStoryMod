using System.Text.Json;

namespace chatAIVintageStoryMod.MCP;

public class MCPToolRegistry
{
    private readonly Dictionary<string, MCPTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _tools.Count;

    public void Register(MCPTool tool) => _tools[tool.Name] = tool;

    public IReadOnlyList<MCPTool> GetAll() => _tools.Values.ToList();

    public async Task<string> ExecuteAsync(string name, Dictionary<string, JsonElement> args)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return $"Unknown tool: {name}";
        try
        {
            return await tool.Handler(args);
        }
        catch (Exception ex)
        {
            return $"Tool error ({name}): {ex.Message}";
        }
    }
}
