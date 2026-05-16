using chatAIVintageStoryMod.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace chatAIVintageStoryMod.Client;

public class AIClientSystem : ModSystem
{
    private ICoreClientAPI _api = null!;
    private IClientNetworkChannel _channel = null!;

    public AIConfigSyncPacket? ServerConfig { get; private set; }

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        _api = api;

        _channel = api.Network
            .RegisterChannel("chataimod")
            .RegisterMessageType<AIQueryPacket>()
            .RegisterMessageType<AIResponsePacket>()
            .RegisterMessageType<AIConfigSyncPacket>()
            .RegisterMessageType<AIConfigChangePacket>()
            .SetMessageHandler<AIResponsePacket>(OnResponse)
            .SetMessageHandler<AIConfigSyncPacket>(OnConfigSync);

        // RegisterCommand on client creates .ai (dot prefix) — the new ChatCommands API does not expose client-side dot commands
#pragma warning disable CS0618
        api.RegisterCommand("ai", Lang("command.description"), ".ai <question>", OnClientAiCommand);
#pragma warning restore CS0618

        TryRegisterConfigLib(api);
    }

    private void OnClientAiCommand(int groupId, CmdArgs args)
    {
        string input = args.PopAll().Trim();

        if (string.IsNullOrEmpty(input))
        {
            _api.ShowChatMessage(Lang("ai.usage"));
            return;
        }

        _channel.SendPacket(new AIQueryPacket { Question = input });
        _api.ShowChatMessage(Lang("ai.sent"));
    }

    private void OnResponse(AIResponsePacket packet)
    {
        if (packet.Error != null)
            _api.ShowChatMessage(Lang("ai.error", packet.Error));
        else
            _api.ShowChatMessage(Lang("ai.response", packet.ProviderName, packet.Response));
    }

    private void OnConfigSync(AIConfigSyncPacket packet)
    {
        ServerConfig = packet;
    }

    public void SendConfigChange(AIConfigChangePacket packet)
    {
        _channel.SendPacket(packet);
    }

    private void TryRegisterConfigLib(ICoreClientAPI api)
    {
        if (!api.ModLoader.IsModEnabled("configlib")) return;
        try
        {
            ConfigLibIntegration.Register(api, this);
        }
        catch (Exception ex)
        {
            Mod.Logger.Warning("chatAI: ConfigLib integration failed: " + ex.Message);
        }
    }

    private string Lang(string key, params object[] args)
        => Vintagestory.API.Config.Lang.Get($"chataimod:{key}", args);
}
