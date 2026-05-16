using chatAIVintageStoryMod.Command;
using chatAIVintageStoryMod.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace chatAIVintageStoryMod;

public class ChatAIMod : ModSystem
{
    private ConfigManager? _configManager;

    public override bool ShouldLoad(EnumAppSide forSide) => true;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _configManager = new ConfigManager(api);
        _configManager.Load();
        _configManager.Save();

        api.Permissions.RegisterPrivilege(AICommand.PrivilegeApiKey, "chatAI API key management", false);

        var command = new AICommand(api, _configManager);
        command.Register();

        Mod.Logger.Notification("chatAI mod loaded. Provider: " + _configManager.Data.Provider);
    }
}
