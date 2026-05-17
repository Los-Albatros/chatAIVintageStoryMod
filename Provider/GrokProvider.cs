namespace chatAIVintageStoryMod.Provider;

public class GrokProvider : OpenAICompatibleProvider
{
    public override string Name => "Grok";

    public GrokProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
        : base(apiKey,
               !string.IsNullOrEmpty(endpoint) ? endpoint : "https://api.x.ai/v1/chat/completions",
               !string.IsNullOrEmpty(model) ? model : "grok-3",
               temperature, maxTokens, http) { }
}
