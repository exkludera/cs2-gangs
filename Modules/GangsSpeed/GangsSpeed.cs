using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using GangsAPI;

namespace GangsSpeed;

public class GangsSpeed : BasePlugin, IPluginConfig<SpeedConfig>
{
    public override string ModuleName => "Gangs Speed";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "Faust & verneri";

    private string moduleName = "speed";
    private GangsApi? _api;
    public SpeedConfig Config {get; set; } = new();
    
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
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterSkill(moduleName);
    }

    public void OnConfigParsed(SpeedConfig config)
    {
		Config = config;
        Helper.UpdateConfig(config);
    }
    
    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid!.Handle == IntPtr.Zero || @event.Userid.UserId == null)
            return HookResult.Continue;
        
        var player = @event.Userid;

        if (player == null || _api == null) return HookResult.Continue;
        if(!player.IsValid || player.IsBot) return HookResult.Continue;
        if(player.Connected != PlayerConnectedState.PlayerConnected) return HookResult.Continue;

        if (_api.OnlyTerroristCheck(player))
            return HookResult.Continue;

        var level = _api.GetSkillLevel(player, moduleName);

        var playerPawn = player.PlayerPawn.Value;

        var SpeedValue = level * Config.Value;

        if (SpeedValue <= 0 || playerPawn == null) return HookResult.Continue;
        AddTimer(0.1f, ()=>{
            playerPawn.VelocityModifier = 1.0f + SpeedValue;
            Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
        });
        return HookResult.Continue;
    }
}
public class SpeedConfig : BasePluginConfig
{
    [JsonPropertyName("MaxLevel")]
    public int MaxLevel { get; set; } = 10;
    [JsonPropertyName("Price")]
    public int Price { get; set; } = 250;
    [JsonPropertyName("Value")]
    public float Value { get; set; } = 0.015f;
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