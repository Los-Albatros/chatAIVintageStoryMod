namespace chatAIVintageStoryMod.Config;

public class ModConfigData
{
    public string Provider { get; set; } = "OLLAMA";
    public int RateLimitSeconds { get; set; } = 30;
    public bool AllowAll { get; set; } = true;
    public string SystemPrompt { get; set; } = "";
    public List<MCPServerConfigEntry> MCPServers { get; set; } = new();

    public OllamaConfig Ollama { get; set; } = new();
    public ProviderConfig Mistral { get; set; } = new();
    public ProviderConfig OpenAI { get; set; } = new();
    public ProviderConfig Anthropic { get; set; } = new();
    public ProviderConfig Grok { get; set; } = new();
    public ProviderConfig DeepSeek { get; set; } = new();

    public class OllamaConfig
    {
        public string Endpoint { get; set; } = "http://localhost:11434/api/generate";
        public string Model { get; set; } = "mistral:7b";
    }

    public class ProviderConfig
    {
        public string ApiKey { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public string Model { get; set; } = "";
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 500;
    }

    public class MCPServerConfigEntry
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
    }
}
