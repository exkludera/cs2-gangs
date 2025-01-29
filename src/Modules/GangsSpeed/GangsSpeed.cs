using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using GangsAPI;

public class Config : BasePluginConfig
{
    public int MaxLevel { get; set; } = 10;
    public int Price { get; set; } = 250;
    public float Value { get; set; } = 0.015f;
}

public class Plugin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Gangs Speed";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "verneri";

    private string moduleName = "speed";
    private GangsApi? _api;

    public Config Config { get; set; } = new();
    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = GangsApi.Capability.Get();
        if (_api == null) return;

        _api.CoreReady += () => 
        {
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        };
        _api.GangsCreated += (player, GangsID) => 
        {
            _api.RegisterSkill(GangsID, moduleName, Config.MaxLevel, Config.Price);
        };
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);

        if (hotReload)
        {
            _api = GangsApi.Capability.Get();
            if (_api == null) return;
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        }
    }

    public override void Unload(bool hotReload)
    {
        DeregisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);

        _api?.UnRegisterSkill(moduleName);
    }
    
    HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || _api == null) return HookResult.Continue;
        if (!player.IsValid || player.IsBot) return HookResult.Continue;
        if (_api.OnlyTerroristCheck(player)) return HookResult.Continue;

        var level = _api.GetSkillLevel(player, moduleName);
        var value = level * Config.Value;
        if (value <= 0) return HookResult.Continue;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return HookResult.Continue;

        Server.NextFrame(() => {
            playerPawn.VelocityModifier = 1.0f + value;
            Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_flVelocityModifier");
        });

        return HookResult.Continue;
    }
}