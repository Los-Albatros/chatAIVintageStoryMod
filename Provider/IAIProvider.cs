namespace chatAIVintageStoryMod.Provider;

public interface IAIProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    Task<string> GenerateResponseAsync(string prompt);
}
