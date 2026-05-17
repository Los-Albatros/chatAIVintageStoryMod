namespace chatAIVintageStoryMod.Provider;

public class OpenAIProvider : OpenAICompatibleProvider
{
    public override string Name => "OpenAI";

    public OpenAIProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
        : base(apiKey,
               endpoint.Length > 0 ? endpoint : "https://api.openai.com/v1/chat/completions",
               model.Length > 0 ? model : "gpt-4o",
               temperature, maxTokens, http) { }
}
