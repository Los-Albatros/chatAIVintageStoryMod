using ConfigLib;
using ImGuiNET;
using chatAIVintageStoryMod.Network;
using Vintagestory.API.Client;

namespace chatAIVintageStoryMod.Client;

public static class ConfigLibIntegration
{
    private static readonly string[] _providers =
        { "ollama", "mistral", "openai", "anthropic", "grok", "deepseek" };

    private static int _selectedProvider;
    private static int _rateLimitSecs;
    private static string _apiKey = "";
    private static string _lastSyncHash = "";

    public static void InvalidateCache() => _lastSyncHash = "";

    public static void Register(ICoreClientAPI api, AIClientSystem clientSystem)
    {
        try
        {
            var modSys = api.ModLoader.GetModSystem<ConfigLibModSystem>();
            if (modSys == null)
            {
                api.Logger.Warning("[chatAI] ConfigLib mod system not loaded — integration disabled.");
                return;
            }

            modSys.RegisterCustomConfig("chataimod", (id, buttons) =>
            {
                try
                {
                    SyncFromServer(clientSystem.ServerConfig);
                    DrawAll(id, buttons.Save, clientSystem);
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"[chatAI] ConfigLib draw error: {ex.GetType().Name}: {ex.Message}");
                }
            });

            api.Logger.Notification("[chatAI] ConfigLib integration registered successfully.");
        }
        catch (Exception ex)
        {
            api.Logger.Error($"[chatAI] ConfigLib registration failed: {ex}");
        }
    }

    private static void SyncFromServer(AIConfigSyncPacket? config)
    {
        if (config == null) return;
        string hash = $"{config.Provider}|{config.RateLimitSeconds}|{config.HasApiKey}|{config.IsAdmin}";
        if (hash == _lastSyncHash) return;
        _lastSyncHash = hash;
        _selectedProvider = Array.IndexOf(_providers, config.Provider.ToLower());
        if (_selectedProvider < 0) _selectedProvider = 0;
        _rateLimitSecs = config.RateLimitSeconds;
        _apiKey = "";
    }

    private static void DrawAll(string id, bool save, AIClientSystem clientSystem)
    {
        var config = clientSystem.ServerConfig;
        if (config == null)
        {
            ImGui.Text("Waiting for server config...");
            return;
        }
        if (config.IsAdmin) DrawAdmin(id, save, clientSystem, config);
        else DrawReadOnly(config);
    }

    private static void DrawAdmin(string id, bool save, AIClientSystem clientSystem, AIConfigSyncPacket config)
    {
        ImGui.Text("Provider");
        ImGui.SetNextItemWidth(220f);
        ImGui.Combo($"##{id}_prov", ref _selectedProvider, _providers, _providers.Length);
        ImGui.Spacing();

        ImGui.Text("Rate limit (seconds per player, 0 = disabled)");
        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt($"##{id}_rate", ref _rateLimitSecs);
        if (_rateLimitSecs < 0) _rateLimitSecs = 0;
        ImGui.Spacing();

        ImGui.Text("API Key");
        ImGui.TextDisabled(config.HasApiKey ? "  currently: configured" : "  currently: not set");
        ImGui.SetNextItemWidth(350f);
        ImGui.InputText($"##{id}_key", ref _apiKey, 256, ImGuiInputTextFlags.Password);
        ImGui.SameLine();
        ImGui.TextDisabled("(leave empty to keep current)");

        if (save)
        {
            clientSystem.SendConfigChange(new AIConfigChangePacket
            {
                Provider = _providers[_selectedProvider],
                RateLimitSeconds = _rateLimitSecs,
                ApiKey = _apiKey
            });
            _apiKey = "";
            _lastSyncHash = "";
        }
    }

    private static void DrawReadOnly(AIConfigSyncPacket config)
    {
        ImGui.Text($"Provider:    {config.Provider}");
        ImGui.Text($"Rate limit:  {(config.RateLimitSeconds > 0 ? $"{config.RateLimitSeconds}s per player" : "disabled")}");
        ImGui.Text($"API Key:     {(config.HasApiKey ? "configured" : "not set")}");
        ImGui.Spacing();
        ImGui.TextDisabled("Settings are managed by the server admin.");
    }
}
