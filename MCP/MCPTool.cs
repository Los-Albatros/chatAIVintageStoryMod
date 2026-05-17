using System.Text.Json;

namespace chatAIVintageStoryMod.MCP;

public class MCPTool
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<MCPParameter> Parameters { get; init; } = [];
    public Func<Dictionary<string, JsonElement>, Task<string>> Handler { get; init; } = _ => Task.FromResult("ok");
}

public class MCPParameter
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "string"; // "string" | "number" | "boolean"
    public string Description { get; init; } = "";
    public bool Required { get; init; }
}
