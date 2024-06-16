using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using GangsAPI;

namespace GangsSize;

public class GangsSize : BasePlugin, IPluginConfig<SizeConfig>
{
    public override string ModuleName => "Gangs Size";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "Faust";

    private string moduleName = "size";
    private GangsApi? _api;
    public SizeConfig Config {get; set; } = new();
    
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

    public void OnConfigParsed(SizeConfig config)
    {
		Config = config;
        Helper.UpdateConfig(config);
    }
}
public class SizeConfig : BasePluginConfig
{
    [JsonPropertyName("MaxLevel")]
    public int MaxLevel { get; set; } = 10;
    [JsonPropertyName("Price")]
    public int Price { get; set; } = 250;
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