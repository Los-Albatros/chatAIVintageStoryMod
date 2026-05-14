using chatAIVintageStoryMod.Provider;

namespace chatAIVintageStoryMod;

public class AIService
{
    private readonly IAIProvider _provider;

    public AIService(IAIProvider provider)
    {
        _provider = provider;
    }

    public bool IsReady => _provider.IsAvailable;
    public string ProviderName => _provider.Name;

    public Task<string> AskAsync(string question) => _provider.GenerateResponseAsync(question);
}
