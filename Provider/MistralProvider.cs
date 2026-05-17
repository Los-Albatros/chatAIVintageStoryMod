namespace chatAIVintageStoryMod.Provider;

public class MistralProvider : OpenAICompatibleProvider
{
    public override string Name => "Mistral";

    public MistralProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
        : base(apiKey,
               endpoint.Length > 0 ? endpoint : "https://api.mistral.ai/v1/chat/completions",
               model.Length > 0 ? model : "mistral-large-latest",
               temperature, maxTokens, http) { }
}
