using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using GangsAPI;
using Microsoft.Extensions.Logging;

namespace GangsDamage;

public class GangsDamage : BasePlugin, IPluginConfig<DamageConfig>
{
    public override string ModuleName => "Gangs Damage";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "Faust & exkludera";

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
        if(hotReload)
        {
            _api = GangsApi.Capability.Get();
            if (_api == null) return;
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        }
        AddTimer(1.0f, DamageHook);
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterSkill(moduleName);
    }

    public void OnConfigParsed(DamageConfig config)
    {
		Config = config;
        Helper.UpdateConfig(config);
    }

    public void DamageHook()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(this.OnTakeDamage, HookMode.Pre);
            }
            //else
            //{
            //    RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);
            //}
        }
        catch (Exception ex)
        {
            if (ex.Message == "Invalid function pointer")
            {
                Logger.LogError($"[GangsDamage] (DamageHook) Error: Conflict between cs2fixes");
            }
            else
            {
                Logger.LogError($"[GangsDamage] (DamageHook) Error | {ex.Message}");
            }
        }
    }
    HookResult OnTakeDamage(DynamicHook hook)
    {
        CEntityInstance ent = hook.GetParam<CEntityInstance>(0);

        if (ent.DesignerName != "player")
            return HookResult.Continue;

        CTakeDamageInfo damageInfo = hook.GetParam<CTakeDamageInfo>(1);
        var attacker = damageInfo.Attacker.Value!.As<CBasePlayerPawn>().Controller.Value;
        var player = Utilities.GetPlayerFromIndex((int)attacker!.Index);

        if (player == null || _api == null) return HookResult.Continue;
        if (!player.IsValid || player.IsBot) return HookResult.Continue;
        if (player.Connected != PlayerConnectedState.PlayerConnected) return HookResult.Continue;

        if (_api.OnlyTerroristCheck(player))
            return HookResult.Continue;

        var level = _api.GetSkillLevel(player, moduleName);

        var damageValue = level * Config.Value;
        if (damageValue <= 0) return HookResult.Continue;

        CCSWeaponBase? ccsWeaponBase = damageInfo.Ability.Value?.As<CCSWeaponBase>();
        if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
        {
            CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;
            if (weaponData == null || weaponData.GearSlot != gear_slot_t.GEAR_SLOT_KNIFE)
                return HookResult.Continue;

            damageInfo.Damage += damageValue;
        }

        return HookResult.Continue;
    }
}
public class DamageConfig : BasePluginConfig
{
    [JsonPropertyName("MaxLevel")]
    public int MaxLevel { get; set; } = 10;
    [JsonPropertyName("Price")]
    public int Price { get; set; } = 250;
    [JsonPropertyName("Value")]
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