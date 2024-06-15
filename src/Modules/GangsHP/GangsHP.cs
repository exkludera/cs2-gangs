using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using GangsAPI;

namespace GangsHP;

public class GangsHP : BasePlugin, IPluginConfig<HPConfig>
{
    public override string ModuleName => "Gangs HP";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "Faust";

    private string moduleName = "hp";
    private GangsApi? _api;
    public HPConfig Config {get; set; } = new();
    
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

    public void OnConfigParsed(HPConfig config)
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
        var level = _api.GetSkillLevel(player, moduleName);

        var playerPawn = player.PlayerPawn.Value;

        var healthValue = level * Config.Value;

        if (healthValue <= 0 || playerPawn == null) return HookResult.Continue;
        AddTimer(0.1f, ()=>{
            playerPawn.Health = 100 + healthValue;
            playerPawn.MaxHealth = 100 + healthValue;
            Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
        });
        return HookResult.Continue;
    }
}
public class HPConfig : BasePluginConfig
{
    [JsonPropertyName("MaxLevel")]
    public int MaxLevel { get; set; } = 10;
    [JsonPropertyName("Price")]
    public int Price { get; set; } = 500;
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