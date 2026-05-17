namespace chatAIVintageStoryMod.Provider;

public class OpenAIProvider : OpenAICompatibleProvider
{
    public override string Name => "OpenAI";

    public OpenAIProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
        : base(apiKey,
               !string.IsNullOrEmpty(endpoint) ? endpoint : "https://api.openai.com/v1/chat/completions",
               !string.IsNullOrEmpty(model) ? model : "gpt-4o",
               temperature, maxTokens, http) { }
}
