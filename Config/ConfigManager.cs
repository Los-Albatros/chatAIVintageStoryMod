using Vintagestory.API.Server;

namespace chatAIVintageStoryMod.Config;

public class ConfigManager
{
    private static readonly string[] KnownProviders =
        { "ollama", "mistral", "openai", "anthropic", "grok", "deepseek" };

    private readonly ICoreServerAPI _api;
    private ModConfigData _data = new();

    public ConfigManager(ICoreServerAPI api) { _api = api; }

    public ModConfigData Data => _data;

    public void Load()
    {
        _data = _api.LoadModConfig<ModConfigData>("chataimod.json") ?? new ModConfigData();
    }

    public void Save() => _api.StoreModConfig(_data, "chataimod.json");

    public bool IsKnownProvider(string name) =>
        KnownProviders.Contains(name.ToLower());

    public void SetProvider(string provider)
    {
        _data.Provider = provider.ToUpper();
        Save();
    }

    public void SetApiKey(string provider, string key)
    {
        switch (provider.ToUpper())
        {
            case "MISTRAL":   _data.Mistral.ApiKey = key;   break;
            case "OPENAI":    _data.OpenAI.ApiKey = key;    break;
            case "ANTHROPIC": _data.Anthropic.ApiKey = key; break;
            case "GROK":      _data.Grok.ApiKey = key;      break;
            case "DEEPSEEK":  _data.DeepSeek.ApiKey = key;  break;
        }
        Save();
    }

    public string GetApiKeyDisplay()
    {
        string raw = GetRawApiKey(_data.Provider);
        return DisplayApiKey(raw);
    }

    private string GetRawApiKey(string provider) => provider.ToUpper() switch
    {
        "MISTRAL"   => _data.Mistral.ApiKey,
        "OPENAI"    => _data.OpenAI.ApiKey,
        "ANTHROPIC" => _data.Anthropic.ApiKey,
        "GROK"      => _data.Grok.ApiKey,
        "DEEPSEEK"  => _data.DeepSeek.ApiKey,
        _           => ""
    };

    public static string ResolveApiKey(string raw)
    {
        if (raw.StartsWith("${") && raw.EndsWith("}"))
        {
            string varName = raw[2..^1];
            if (varName.Length == 0) return "";
            return Environment.GetEnvironmentVariable(varName) ?? "";
        }
        return raw;
    }

    public static string DisplayApiKey(string raw)
    {
        if (raw.StartsWith("${") && raw.EndsWith("}"))
        {
            string varName = raw[2..^1];
            if (varName.Length == 0) return "[not set]";
            return Environment.GetEnvironmentVariable(varName) != null ? "[ENV]" : "[not set]";
        }
        if (string.IsNullOrEmpty(raw)) return "[not set]";
        return "***";
    }
}
