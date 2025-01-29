using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using GangsAPI;
using StoreApi;

public class Config : BasePluginConfig
{
    public int MaxLevel { get; set; } = 10;
    public int Price { get; set; } = 250;
    public int Value { get; set; } = 5;
}

public class Plugin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Gangs Salary";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "verneri";

    private string moduleName = "salary";

    private GangsApi? _api;
    public IStoreApi? StoreApi { get; set; }

    public Config Config {get; set; } = new();
    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = GangsApi.Capability.Get();
        if (_api == null) return;

        StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");

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
    
    public HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || _api == null) return HookResult.Continue;
        if (!player.IsValid || player.IsBot) return HookResult.Continue;
        if (_api.OnlyTerroristCheck(player)) return HookResult.Continue;

        var level = _api.GetSkillLevel(player, moduleName);
        int value = level * Config.Value;
        if (value <= 0) return HookResult.Continue;

        if (StoreApi == null) return HookResult.Continue;

        int gangid = _api.GetGangId(player);
        string gangname = _api.GetGangName(gangid);

        Server.NextFrame(() => {
            StoreApi.GivePlayerCredits(player, value);
            player.PrintToChat($"{Localizer["chat<prefix>"]} {Localizer["chat<salary>", value, gangname]}");
        });

        return HookResult.Continue;
    }
}