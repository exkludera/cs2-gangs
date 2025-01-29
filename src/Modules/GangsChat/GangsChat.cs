using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using GangsAPI;

public class Config : BasePluginConfig
{
    public int MaxLevel { get; set; } = 1;
    public int Price { get; set; } = 250;
    public int Value { get; set; } = 1;
    public string Command { get; set; } = "css_gc";
}

public class Plugin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Gangs Chat";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "exkludera";

    private string moduleName = "chat";
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
        AddCommand($"{Config.Command}", "", GangChat);

        if (hotReload)
        {
            _api = GangsApi.Capability.Get();
            if (_api == null) return;
            _api.RegisterSkill(moduleName, Config.MaxLevel, Config.Price);
        }
    }

    public override void Unload(bool hotReload)
    {
        RemoveCommand($"css_{Config.Command}", GangChat);

        _api?.UnRegisterSkill(moduleName);
    }

    void GangChat(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || _api == null) return;
        if (!player.IsValid || player.IsBot) return;
        if (_api.OnlyTerroristCheck(player)) return;

        var level = _api.GetSkillLevel(player, moduleName);
        var value = level * Config.Value;
        if (value <= 0) return;

        var message = info.ArgString;

        if (!string.IsNullOrEmpty(message))
        {
            int gangid = _api.GetGangId(player);
            string gangname = _api.GetGangName(gangid);
            List<ulong> steamids = _api.GetGangMembers(gangid);

            foreach (var steamid in steamids)
            {
                var target = Utilities.GetPlayerFromSteamId(steamid);

                if (target == null || !target.IsValid)
                    continue;

                target.PrintToChat($"{ChatColors.Grey}[{gangname}] {player.PlayerName}{ChatColors.White}: {message}");
            }

            return;
        }

        return;
    }
}