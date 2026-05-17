using System.Reflection;
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
    private static Type? _imgui;
    private static Type? _imguiFlags;

    public static void InvalidateCache() => _lastSyncHash = "";

    public static void Register(ICoreClientAPI api, AIClientSystem clientSystem)
    {
        Type? clType = FindType("ConfigLib.ConfigLibModSystem");
        if (clType == null)
        {
            api.Logger.Warning("[chatAI] ConfigLib type not found — ConfigLib integration disabled.");
            return;
        }

        var modSys = api.ModLoader.GetModSystem(clType.FullName ?? "");
        if (modSys == null)
        {
            api.Logger.Warning("[chatAI] ConfigLib mod system not loaded — integration disabled.");
            return;
        }

        var regMethod = clType.GetMethod("RegisterCustomConfig");
        if (regMethod == null)
        {
            api.Logger.Warning("[chatAI] ConfigLib.RegisterCustomConfig method not found — integration disabled.");
            return;
        }

        Type? buttonsType = FindType("ConfigLib.ControlButtons");
        if (buttonsType == null)
        {
            api.Logger.Warning("[chatAI] ConfigLib.ControlButtons type not found — integration disabled.");
            return;
        }

        _imgui = FindType("ImGuiNET.ImGui");
        _imguiFlags = FindType("ImGuiNET.ImGuiInputTextFlags");

        if (_imgui == null)
        {
            api.Logger.Warning("[chatAI] ImGuiNET.ImGui not found — ConfigLib UI will be limited.");
        }

        typeof(ConfigLibIntegration)
            .GetMethod(nameof(BindDelegate), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(buttonsType)
            .Invoke(null, new object[] { modSys, regMethod, clientSystem });

        api.Logger.Notification("[chatAI] ConfigLib integration registered.");
    }

    private static void BindDelegate<TButtons>(object modSys, MethodInfo regMethod, AIClientSystem clientSystem)
    {
        Action<string, TButtons> del = (id, buttons) =>
        {
            SyncFromServer(clientSystem.ServerConfig);
            bool save = (bool)(typeof(TButtons).GetField("Save")?.GetValue(buttons) ?? false);
            DrawAll(id, save, clientSystem);
        };
        regMethod.Invoke(modSys, new object[] { "chataimod", del });
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
            ImText("[chatAI] Waiting for server config...");
            return;
        }
        if (_imgui == null)
        {
            ImText("[chatAI] ImGui not available.");
            return;
        }
        if (config.IsAdmin) DrawAdmin(id, save, clientSystem, config);
        else DrawReadOnly(config);
    }

    private static void DrawAdmin(string id, bool save, AIClientSystem clientSystem, AIConfigSyncPacket config)
    {
        ImText("Provider");
        ImSetNextItemWidth(220f);
        ImCombo($"##{id}_prov", ref _selectedProvider, _providers, _providers.Length);
        ImSpacing();

        ImText("Rate limit (seconds per player, 0 = disabled)");
        ImSetNextItemWidth(120f);
        ImInputInt($"##{id}_rate", ref _rateLimitSecs);
        if (_rateLimitSecs < 0) _rateLimitSecs = 0;
        ImSpacing();

        ImText("API Key");
        ImTextDisabled(config.HasApiKey ? "  currently: configured" : "  currently: not set");
        ImSetNextItemWidth(350f);
        ImInputTextPassword($"##{id}_key", ref _apiKey, 256);
        ImSameLine();
        ImTextDisabled("(leave empty to keep current)");

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
        ImText($"Provider:    {config.Provider}");
        ImText($"Rate limit:  {(config.RateLimitSeconds > 0 ? $"{config.RateLimitSeconds}s per player" : "disabled")}");
        ImText($"API Key:     {(config.HasApiKey ? "configured" : "not set")}");
        ImSpacing();
        ImTextDisabled("Settings are managed by the server admin.");
    }

    // --- ImGui reflection helpers ---

    private static void ImText(string text) =>
        _imgui?.GetMethod("Text", new[] { typeof(string) })?.Invoke(null, new object[] { text });

    private static void ImTextDisabled(string text) =>
        _imgui?.GetMethod("TextDisabled", new[] { typeof(string) })?.Invoke(null, new object[] { text });

    private static void ImSetNextItemWidth(float w) =>
        _imgui?.GetMethod("SetNextItemWidth", new[] { typeof(float) })?.Invoke(null, new object[] { w });

    private static void ImSpacing() => InvokeDefaultArgs("Spacing");

    private static void ImSameLine() => InvokeDefaultArgs("SameLine");

    private static void ImCombo(string label, ref int current, string[] items, int count)
    {
        var m = _imgui?.GetMethods().FirstOrDefault(m =>
            m.Name == "Combo" &&
            m.GetParameters() is { Length: >= 4 } ps &&
            ps[0].ParameterType == typeof(string) &&
            ps[1].ParameterType == typeof(int).MakeByRefType() &&
            ps[2].ParameterType == typeof(string[]) &&
            ps[3].ParameterType == typeof(int));
        if (m == null) return;
        var parms = m.GetParameters();
        object?[] args = new object?[parms.Length];
        args[0] = label; args[1] = current; args[2] = items; args[3] = count;
        for (int i = 4; i < parms.Length; i++)
            args[i] = parms[i].HasDefaultValue ? parms[i].DefaultValue : null;
        m.Invoke(null, args);
        current = (int)(args[1] ?? current);
    }

    private static void ImInputInt(string label, ref int value)
    {
        var m = _imgui?.GetMethods().FirstOrDefault(m =>
            m.Name == "InputInt" &&
            m.GetParameters() is { Length: >= 2 } ps &&
            ps[0].ParameterType == typeof(string) &&
            ps[1].ParameterType == typeof(int).MakeByRefType());
        if (m == null) return;
        var parms = m.GetParameters();
        object?[] args = new object?[parms.Length];
        args[0] = label; args[1] = value;
        for (int i = 2; i < parms.Length; i++)
            args[i] = parms[i].HasDefaultValue ? parms[i].DefaultValue : null;
        m.Invoke(null, args);
        value = (int)(args[1] ?? value);
    }

    private static void ImInputTextPassword(string label, ref string text, uint maxLength)
    {
        MethodInfo? m = null;
        object?[]? args = null;

        if (_imguiFlags != null)
        {
            m = _imgui?.GetMethods().FirstOrDefault(m =>
                m.Name == "InputText" &&
                m.GetParameters() is { Length: >= 4 } ps &&
                ps[0].ParameterType == typeof(string) &&
                ps[1].ParameterType == typeof(string).MakeByRefType() &&
                ps[2].ParameterType == typeof(uint) &&
                ps[3].ParameterType == _imguiFlags);
            if (m != null)
            {
                var parms = m.GetParameters();
                args = new object?[parms.Length];
                args[0] = label; args[1] = text; args[2] = maxLength;
                args[3] = Enum.ToObject(_imguiFlags, 128); // Password flag
                for (int i = 4; i < parms.Length; i++)
                    args[i] = parms[i].HasDefaultValue ? parms[i].DefaultValue : null;
            }
        }

        if (m == null)
        {
            m = _imgui?.GetMethods().FirstOrDefault(m =>
                m.Name == "InputText" &&
                m.GetParameters() is { Length: >= 3 } ps &&
                ps[0].ParameterType == typeof(string) &&
                ps[1].ParameterType == typeof(string).MakeByRefType() &&
                ps[2].ParameterType == typeof(uint));
            if (m != null)
            {
                var parms = m.GetParameters();
                args = new object?[parms.Length];
                args[0] = label; args[1] = text; args[2] = maxLength;
                for (int i = 3; i < parms.Length; i++)
                    args[i] = parms[i].HasDefaultValue ? parms[i].DefaultValue : null;
            }
        }

        if (m == null || args == null) return;
        m.Invoke(null, args);
        text = (string)(args[1] ?? text);
    }

    private static void InvokeDefaultArgs(string methodName)
    {
        var m = _imgui?.GetMethods().FirstOrDefault(m => m.Name == methodName);
        if (m == null) return;
        var ps = m.GetParameters();
        object?[] args = ps.Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray<object?>();
        m.Invoke(null, args);
    }

    private static Type? FindType(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(typeName);
            if (t != null) return t;
        }
        return null;
    }
}
