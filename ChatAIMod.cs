using chatAIVintageStoryMod.Command;
using chatAIVintageStoryMod.Config;
using chatAIVintageStoryMod.MCP;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace chatAIVintageStoryMod;

public class ChatAIMod : ModSystem
{
    private ConfigManager? _configManager;

    // Public API for submods: call api.ModLoader.GetModSystem<ChatAIMod>().RegisterTool(...)
    public MCPToolRegistry ToolRegistry { get; } = new();

    public override bool ShouldLoad(EnumAppSide forSide) => true;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _configManager = new ConfigManager(api);
        _configManager.Load();
        _configManager.Save();

        api.Permissions.RegisterPrivilege(AICommand.PrivilegeApiKey, "chatAI API key management", false);
        api.Permissions.RegisterPrivilege(AICommand.PrivilegeUse,    "chatAI usage permission",   false);

        _ = RegisterExternalMCPServersAsync(api);

        var command = new AICommand(api, _configManager, ToolRegistry);
        command.Register();

        Mod.Logger.Notification("chatAI mod loaded. Provider: " + _configManager.Data.Provider);
    }

    public void RegisterTool(MCPTool tool) => ToolRegistry.Register(tool);

    private async Task RegisterExternalMCPServersAsync(ICoreServerAPI api)
    {
        var servers = _configManager!.Data.MCPServers;
        if (servers.Count == 0) return;

        foreach (var serverCfg in servers)
        {
            if (string.IsNullOrEmpty(serverCfg.Url)) continue;
            try
            {
                var client = new ExternalMCPClient(serverCfg.Url, serverCfg.Name);
                var tools = await client.DiscoverToolsAsync();
                foreach (var tool in tools)
                {
                    ToolRegistry.Register(tool);
                }
                Mod.Logger.Notification($"[chatAI] Registered {tools.Count} tools from MCP server '{serverCfg.Name}'");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[chatAI] Failed to connect to MCP server '{serverCfg.Name}' ({serverCfg.Url}): {ex.Message}");
            }
        }
    }
}
