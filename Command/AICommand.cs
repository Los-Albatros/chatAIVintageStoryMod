using chatAIVintageStoryMod.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace chatAIVintageStoryMod.Command;

public class AICommand
{
    private readonly ICoreServerAPI _api;
    private readonly ConfigManager _config;

    public const string PrivilegeApiKey = "chataimod.apikey";

    public AICommand(ICoreServerAPI api, ConfigManager config)
    {
        _api = api;
        _config = config;
    }

    public void Register()
    {
        _api.ChatCommands
            .Create("ai")
            .WithDescription(Lang("command.description"))
            .WithArgs(new StringArgParser("args", isMandatoryArg: false))
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(OnAiCommand);
    }

    private TextCommandResult OnAiCommand(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        string input = args[0]?.ToString()?.Trim() ?? "";

        if (input.StartsWith("config", StringComparison.OrdinalIgnoreCase))
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return HandleConfig(player, parts.Length > 1 ? parts[1..] : []);
        }

        if (string.IsNullOrEmpty(input))
            return TextCommandResult.Error(Lang("ai.usage"));

        var service = BuildService();
        if (!service.IsReady)
            return TextCommandResult.Error(Lang("ai.provider_unavailable"));

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
                if (!new[] { "ollama", "mistral", "openai" }.Contains(prov))
                    return TextCommandResult.Error(Lang("config.unknown_provider", prov));
                _config.SetProvider(prov);
                return TextCommandResult.Success(Lang("config.provider_set", prov));

            case "apikey":
                if (!HasApiKeyPermission(player))
                    return TextCommandResult.Error(Lang("config.no_permission"));
                if (args.Length < 2)
                    return TextCommandResult.Error(Lang("config.usage"));
                _config.SetApiKey(_config.Data.Provider, args[1]);
                return TextCommandResult.Success(Lang("config.apikey_set"));

            default:
                return TextCommandResult.Error(Lang("config.usage"));
        }
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
                data.Mistral.Endpoint.Length > 0 ? data.Mistral.Endpoint : "https://api.mistral.ai/v1/chat/completions",
                data.Mistral.Model.Length > 0 ? data.Mistral.Model : "mistral-large-latest",
                data.Mistral.Temperature, data.Mistral.MaxTokens),
            "OPENAI" => new Provider.OpenAIProvider(
                ConfigManager.ResolveApiKey(data.OpenAI.ApiKey),
                data.OpenAI.Endpoint.Length > 0 ? data.OpenAI.Endpoint : "https://api.openai.com/v1/chat/completions",
                data.OpenAI.Model.Length > 0 ? data.OpenAI.Model : "gpt-4o",
                data.OpenAI.Temperature, data.OpenAI.MaxTokens),
            _ => new Provider.OllamaProvider(data.Ollama.Endpoint, data.Ollama.Model)
        };
        return new AIService(provider);
    }

    private string Lang(string key, params object[] args) => Vintagestory.API.Config.Lang.Get($"chataimod:{key}", args);
}
