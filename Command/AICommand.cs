using System.Collections.Concurrent;
using chatAIVintageStoryMod.Config;
using chatAIVintageStoryMod.MCP;
using chatAIVintageStoryMod.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace chatAIVintageStoryMod.Command;

public class AICommand
{
    private readonly ICoreServerAPI _api;
    private readonly ConfigManager _config;
    private readonly MCPToolRegistry _registry;
    private readonly ConcurrentDictionary<string, DateTime> _rateLimitTracker = new();
    private IServerNetworkChannel _channel = null!;

    public const string PrivilegeApiKey = "chataimod.apikey";
    public const string PrivilegeUse    = "chataimod.use";

    public AICommand(ICoreServerAPI api, ConfigManager config, MCPToolRegistry registry)
    {
        _api = api;
        _config = config;
        _registry = registry;
    }

    public void Register()
    {
        _channel = _api.Network
            .RegisterChannel("chataimod")
            .RegisterMessageType<AIQueryPacket>()
            .RegisterMessageType<AIResponsePacket>()
            .RegisterMessageType<AIConfigSyncPacket>()
            .RegisterMessageType<AIConfigChangePacket>()
            .SetMessageHandler<AIQueryPacket>(OnClientQuery)
            .SetMessageHandler<AIConfigChangePacket>(OnClientConfigChange);

        _api.Event.PlayerJoin += OnPlayerJoin;

        _api.ChatCommands
            .Create("ai")
            .WithDescription(Lang("command.description"))
            .WithArgs(new StringArgParser("args", isMandatoryArg: false))
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(OnAiCommand);
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        _channel.SendPacket(BuildConfigSyncPacket(player), player);
    }

    private AIConfigSyncPacket BuildConfigSyncPacket(IServerPlayer player)
    {
        var data = _config.Data;
        string rawKey = data.Provider.ToUpper() switch
        {
            "MISTRAL"   => data.Mistral.ApiKey,
            "OPENAI"    => data.OpenAI.ApiKey,
            "ANTHROPIC" => data.Anthropic.ApiKey,
            "GROK"      => data.Grok.ApiKey,
            "DEEPSEEK"  => data.DeepSeek.ApiKey,
            _           => ""
        };
        return new AIConfigSyncPacket
        {
            Provider         = data.Provider,
            RateLimitSeconds = data.RateLimitSeconds,
            HasApiKey        = !string.IsNullOrEmpty(ConfigManager.ResolveApiKey(rawKey)),
            IsAdmin          = player.HasPrivilege(Privilege.controlserver)
        };
    }

    private void OnClientConfigChange(IServerPlayer player, AIConfigChangePacket packet)
    {
        if (!player.HasPrivilege(Privilege.controlserver))
        {
            _channel.SendPacket(new AIResponsePacket { Error = Lang("config.no_permission") }, player);
            return;
        }

        if (!string.IsNullOrEmpty(packet.Provider))
        {
            string prov = packet.Provider.ToLower();
            if (!_config.IsKnownProvider(prov))
            {
                _channel.SendPacket(new AIResponsePacket { Error = Lang("config.unknown_provider", prov) }, player);
                return;
            }
            _config.SetProvider(prov);
        }

        if (packet.RateLimitSeconds.HasValue)
        {
            _config.Data.RateLimitSeconds = packet.RateLimitSeconds.Value;
            _config.Save();
        }

        if (!string.IsNullOrEmpty(packet.ApiKey))
            _config.SetApiKey(_config.Data.Provider, packet.ApiKey);

        SyncConfigToAll();
    }

    private void OnClientQuery(IServerPlayer player, AIQueryPacket packet)
    {
        var result = ExecuteAiQuery(player, packet.Question);
        if (result.StatusMessage != null && result.Status == EnumCommandStatus.Error)
            _channel.SendPacket(new AIResponsePacket { Error = result.StatusMessage }, player);
    }

    private TextCommandResult OnAiCommand(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        string input = args[0]?.ToString()?.Trim() ?? "";

        if (input.StartsWith("config", StringComparison.OrdinalIgnoreCase))
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return HandleConfig(player, parts.Length > 1 ? parts[1..] : new string[0]);
        }

        if (string.IsNullOrEmpty(input))
            return TextCommandResult.Error(Lang("ai.usage"));

        return ExecuteAiQuery(player, input);
    }

    private TextCommandResult ExecuteAiQuery(IServerPlayer? player, string input)
    {
        if (player != null && !HasQueryPermission(player))
            return TextCommandResult.Error(Lang("ai.no_permission"));

        if (player != null && IsRateLimited(player.PlayerUID, out int secondsLeft))
            return TextCommandResult.Error(Lang("ai.rate_limited", secondsLeft));

        var service = BuildService();
        if (!service.IsReady)
            return TextCommandResult.Error(Lang("ai.provider_unavailable"));

        if (player != null)
        {
            _rateLimitTracker[player.PlayerUID] = DateTime.UtcNow;
            _api.Logger.Audit($"[chatAI] {player.PlayerName}: {input}");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var response = await service.AskAsync(input);
                player?.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang("ai.response", service.ProviderName, response),
                    EnumChatType.Notification);
            }
            catch (Exception ex)
            {
                player?.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang("ai.error", ex.Message),
                    EnumChatType.Notification);
            }
        });

        return TextCommandResult.Success(Lang("ai.thinking", service.ProviderName));
    }

    private bool HasQueryPermission(IServerPlayer player)
    {
        if (_config.Data.AllowAll) return true;
        return player.HasPrivilege(PrivilegeUse);
    }

    private bool IsRateLimited(string playerUid, out int secondsRemaining)
    {
        int limitSeconds = _config.Data.RateLimitSeconds;
        if (limitSeconds <= 0) { secondsRemaining = 0; return false; }

        if (_rateLimitTracker.TryGetValue(playerUid, out DateTime lastRequest))
        {
            var elapsed = DateTime.UtcNow - lastRequest;
            if (elapsed.TotalSeconds < limitSeconds)
            {
                secondsRemaining = (int)(limitSeconds - elapsed.TotalSeconds) + 1;
                return true;
            }
        }
        secondsRemaining = 0;
        return false;
    }

    private TextCommandResult HandleConfig(IServerPlayer? player, string[] args)
    {
        if (args.Length == 0)
            return TextCommandResult.Error(Lang("config.usage"));

        switch (args[0].ToLower())
        {
            case "show":
                if (!HasConfigPermission(player))
                    return TextCommandResult.Error(Lang("config.no_permission"));
                return TextCommandResult.Success(
                    Lang("config.show", _config.Data.Provider, _config.GetApiKeyDisplay()));

            case "provider":
                if (!HasConfigPermission(player))
                    return TextCommandResult.Error(Lang("config.no_permission"));
                if (args.Length < 2)
                    return TextCommandResult.Error(Lang("config.usage"));
                string prov = args[1].ToLower();
                if (!_config.IsKnownProvider(prov))
                    return TextCommandResult.Error(Lang("config.unknown_provider", prov));
                _config.SetProvider(prov);
                _api.BroadcastMessageToAllGroups(Lang("config.provider_set", prov), EnumChatType.Notification);
                SyncConfigToAll();
                return TextCommandResult.Success("");

            case "apikey":
                if (!HasApiKeyPermission(player))
                    return TextCommandResult.Error(Lang("config.no_permission"));
                if (args.Length < 2)
                    return TextCommandResult.Error(Lang("config.usage"));
                _config.SetApiKey(_config.Data.Provider, args[1]);
                SyncConfigToAll();
                return TextCommandResult.Success(Lang("config.apikey_set"));

            default:
                return TextCommandResult.Error(Lang("config.usage"));
        }
    }

    private void SyncConfigToAll()
    {
        foreach (var p in _api.World.AllOnlinePlayers)
            if (p is IServerPlayer sp)
                _channel.SendPacket(BuildConfigSyncPacket(sp), sp);
    }

    private bool HasConfigPermission(IServerPlayer? player)
        => player == null || player.HasPrivilege(Privilege.controlserver);

    private bool HasApiKeyPermission(IServerPlayer? player)
        => player == null || player.HasPrivilege(PrivilegeApiKey);

    private AIService BuildService()
    {
        var data = _config.Data;
        var provider = data.Provider.ToUpper() switch
        {
            "MISTRAL" => (Provider.IAIProvider)new Provider.MistralProvider(
                ConfigManager.ResolveApiKey(data.Mistral.ApiKey),
                data.Mistral.Endpoint, data.Mistral.Model,
                data.Mistral.Temperature, data.Mistral.MaxTokens),
            "OPENAI" => new Provider.OpenAIProvider(
                ConfigManager.ResolveApiKey(data.OpenAI.ApiKey),
                data.OpenAI.Endpoint, data.OpenAI.Model,
                data.OpenAI.Temperature, data.OpenAI.MaxTokens),
            "ANTHROPIC" => new Provider.AnthropicProvider(
                ConfigManager.ResolveApiKey(data.Anthropic.ApiKey),
                data.Anthropic.Endpoint, data.Anthropic.Model,
                data.Anthropic.Temperature, data.Anthropic.MaxTokens),
            "GROK" => new Provider.GrokProvider(
                ConfigManager.ResolveApiKey(data.Grok.ApiKey),
                data.Grok.Endpoint, data.Grok.Model,
                data.Grok.Temperature, data.Grok.MaxTokens),
            "DEEPSEEK" => new Provider.DeepSeekProvider(
                ConfigManager.ResolveApiKey(data.DeepSeek.ApiKey),
                data.DeepSeek.Endpoint, data.DeepSeek.Model,
                data.DeepSeek.Temperature, data.DeepSeek.MaxTokens),
            _ => new Provider.OllamaProvider(data.Ollama.Endpoint, data.Ollama.Model)
        };

        string? systemPrompt = string.IsNullOrWhiteSpace(data.SystemPrompt) ? null : data.SystemPrompt;
        return new AIService(provider, systemPrompt, _registry);
    }

    private string Lang(string key, params object[] args)
        => Vintagestory.API.Config.Lang.Get($"chataimod:{key}", args);
}
