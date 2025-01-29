using CounterStrikeSharp.API.Core;
using GangsAPI;

public class Config : BasePluginConfig
{
    public int MaxLevel { get; set; } = 10;
    public int Price { get; set; } = 250;
    public int Value { get; set; } = 2;
}

public class Plugin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Gangs Damage";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "exkludera";

    private string moduleName = "damage";
    private GangsApi? _api;

    public Config Config {get; set; } = new();
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
        RegisterEventHandler<EventPlayerHurt>(EventPlayerHurt, HookMode.Pre);

        if (hotReload)
        {
            _api = GangsApi.Capability.Get();
            if (_api == null) return;
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        }
    }

    public override void Unload(bool hotReload)
    {
        DeregisterEventHandler<EventPlayerHurt>(EventPlayerHurt, HookMode.Pre);

        _api?.UnRegisterSkill(moduleName);
    }

    HookResult EventPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var player = @event.Attacker;

        if (player == null || _api == null) return HookResult.Continue;
        if (!player.IsValid || player.IsBot) return HookResult.Continue;
        if (_api.OnlyTerroristCheck(player)) return HookResult.Continue;

        var level = _api.GetSkillLevel(player, moduleName);
        var value = level * Config.Value;
        if (value <= 0) return HookResult.Continue;

        CCSWeaponBase? ccsWeaponBase = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value?.As<CCSWeaponBase>();
        if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
        {
            CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;
            if (weaponData == null || weaponData.GearSlot != gear_slot_t.GEAR_SLOT_KNIFE)
                return HookResult.Continue;

            @event.Userid!.PlayerPawn.Value!.Health -= value;
        }

        return HookResult.Continue;
    }
}