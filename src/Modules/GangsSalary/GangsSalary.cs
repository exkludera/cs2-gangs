using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using GangsAPI;
using StoreApi;

public class Config : BasePluginConfig
{
    public int MaxLevel { get; set; } = 20;
    public int Price { get; set; } = 1;
    public int Value { get; set; } = 1;
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
        if (hotReload)
        {
            _api = GangsApi.Capability.Get();
            if (_api == null) return;
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        }
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterSkill(moduleName);
    }
    
    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null)
            return HookResult.Continue;

        GiveSalary(player);

        return HookResult.Continue;
    }

    public void GiveSalary(CCSPlayerController player)
    {
        if (player == null || _api == null) return;
        if (!player.IsValid || player.IsBot) return;
        if (player.Connected != PlayerConnectedState.PlayerConnected) return;

        if (_api.OnlyTerroristCheck(player))
            return;

        var level = _api.GetSkillLevel(player, moduleName);

        int salary = level * Config.Value;

        AddTimer(0.1f, () => {
            StoreApi!.GivePlayerCredits(player, salary);
            player.PrintToChat($"[salary] You have received ${salary} from your gang");
        });
    }
}