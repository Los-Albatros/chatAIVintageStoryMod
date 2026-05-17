namespace chatAIVintageStoryMod.Provider;

public class MistralProvider : OpenAICompatibleProvider
{
    public override string Name => "Mistral";

    public MistralProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
        : base(apiKey,
               !string.IsNullOrEmpty(endpoint) ? endpoint : "https://api.mistral.ai/v1/chat/completions",
               !string.IsNullOrEmpty(model) ? model : "mistral-large-latest",
               temperature, maxTokens, http) { }
}
