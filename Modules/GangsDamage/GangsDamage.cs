using System.Reflection;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using GangsAPI;

public class Plugin : BasePlugin, IPluginConfig<DamageConfig>
{
    public override string ModuleName => "Gangs Damage";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "exkludera";

    private string moduleName = "damage";
    private GangsApi? _api;
    public DamageConfig Config {get; set; } = new();
    
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
        if (hotReload)
        {
            _api = GangsApi.Capability.Get();
            if (_api == null) return;
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        }

        RegisterEventHandler<EventPlayerHurt>(EventPlayerHurt, HookMode.Pre);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterSkill(moduleName);

        DeregisterEventHandler<EventPlayerHurt>(EventPlayerHurt, HookMode.Pre);
    }

    public void OnConfigParsed(DamageConfig config)
    {
		Config = config;
        Helper.UpdateConfig(config);
    }

    HookResult EventPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var player = @event.Attacker;

        if (player == null || _api == null) return HookResult.Continue;
        if (!player.IsValid || player.IsBot) return HookResult.Continue;
        if (player.Connected != PlayerConnectedState.PlayerConnected) return HookResult.Continue;

        if (_api.OnlyTerroristCheck(player))
            return HookResult.Continue;

        var level = _api.GetSkillLevel(player, moduleName);

        var damageValue = level * Config.Value;
        if (damageValue <= 0) return HookResult.Continue;

        CCSWeaponBase? ccsWeaponBase = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value?.As<CCSWeaponBase>();
        if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
        {

            CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;
            if (weaponData == null || weaponData.GearSlot != gear_slot_t.GEAR_SLOT_KNIFE)
                return HookResult.Continue;

            @event.Userid!.PlayerPawn.Value!.Health -=damageValue;
        }

        return HookResult.Continue;
    }
}
public class DamageConfig : BasePluginConfig
{
    public int MaxLevel { get; set; } = 10;
    public int Price { get; set; } = 250;
    public int Value { get; set; } = 2;
}
internal class Helper
{
    private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "";
    private static readonly string CfgPath = $"{Server.GameDirectory}/csgo/addons/counterstrikesharp/configs/plugins/{AssemblyName}/{AssemblyName}.json";
    public static void UpdateConfig<T>(T config) where T : BasePluginConfig, new()
    {
        // serialize the updated config back to json
        var updatedJsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true, DictionaryKeyPolicy = JsonNamingPolicy.CamelCase, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(CfgPath, updatedJsonContent);
    }
}