using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
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
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(this.OnTakeDamage, HookMode.Pre);

        if (hotReload)
        {
            _api = GangsApi.Capability.Get();
            if (_api == null) return;
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        }
    }

    public override void Unload(bool hotReload)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(this.OnTakeDamage, HookMode.Pre);

        _api?.UnRegisterSkill(moduleName);
    }

    public HookResult OnTakeDamage(DynamicHook hook)
    {
        var damageInfo = hook.GetParam<CTakeDamageInfo>(1);

        var attacker = damageInfo.Attacker.Value;
        if (attacker == null || _api == null) return HookResult.Continue;

        var pawnController = new CCSPlayerPawn(attacker.Handle).Controller.Value;
        if (pawnController == null) return HookResult.Continue;

        var player = new CCSPlayerController(pawnController.Handle);
        if (!player.IsValid) return HookResult.Continue;

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

                damageInfo.Damage += value;

                return HookResult.Continue;
            }
        
        return HookResult.Continue;
    }
}