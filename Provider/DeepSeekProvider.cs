namespace chatAIVintageStoryMod.Provider;

public class DeepSeekProvider : OpenAICompatibleProvider
{
    public override string Name => "DeepSeek";

    public DeepSeekProvider(string apiKey, string endpoint, string model,
        double temperature = 0.7, int maxTokens = 500, HttpClient? http = null)
        : base(apiKey,
               !string.IsNullOrEmpty(endpoint) ? endpoint : "https://api.deepseek.com/v1/chat/completions",
               !string.IsNullOrEmpty(model) ? model : "deepseek-chat",
               temperature, maxTokens, http) { }
}
