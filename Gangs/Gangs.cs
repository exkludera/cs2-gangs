using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using GangsAPI;
using CounterStrikeSharp.API.Core.Capabilities;
using StoreApi;
using CounterStrikeSharp.API.Core.Translations;

namespace Gangs;

public partial class Gangs : BasePlugin, IPluginConfig<GangsConfig>
{
    public override string ModuleName => "Gangs";
    public override string ModuleVersion => "0.1.3";
    public override string ModuleAuthor => "Faust, continued by exkludera";

    public GangsConfig Config { get; set; } = new();

    internal string dbConnectionString = string.Empty;

    public UserInfo[] userInfo = new UserInfo[65];
    public List<Gang> GangList = new();
    public List<Skill> SkillList = new();

    public Api.ApiGangs? _api;
    private readonly PluginCapability<GangsApi> _pluginCapability = new("Gangs_Core:API");

    public IStoreApi? StoreApi { get; set; }

    public override void Load(bool hotReload)
    {
        _api = new Api.ApiGangs(this); 
        Capabilities.RegisterPluginCapability(_pluginCapability, () => _api);
        Server.NextWorldUpdate(() => _api.OnCoreReady());

        RegisterEvents();

        if(hotReload)
        {
            OnMapStart(string.Empty);
            
			foreach(var player in Utilities.GetPlayers())
			{
				if(player.AuthorizedSteamID != null)
					OnClientAuthorized(player.Slot, player.AuthorizedSteamID);
			}
        }

        string[] commands = Config.OpenCommands.Split(';');
        foreach(var command in commands)
        {
            AddCommand(command, "Open main gang menu", (player, _) => CommandGang(player));
        }
    }

    public override void Unload(bool hotReload)
    {
        UnregisterEvents();
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            StoreApi = IStoreApi.Capability.Get();
        }
        catch (Exception ex)
        {
            Logger.LogError("{OnAllPluginsLoaded} Fail load StoreApi! | " + ex.Message);
            throw new Exception("[Gangs] Fail load StoreApi! | " + ex.Message);
        }
    }

    public void OnConfigParsed(GangsConfig config)
    {
		if (config.DatabaseHost.Length < 1 || config.DatabaseName.Length < 1 || config.DatabaseUser.Length < 1)
			throw new Exception("[CS2-Gangs] You need to setup Database info in config!");

        GangList.Clear();

        MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = config.DatabaseHost,
			Database = config.DatabaseName,
			UserID = config.DatabaseUser,
			Password = config.DatabasePassword,
			Port = (uint)config.DatabasePort
		};

        dbConnectionString = builder.ConnectionString;

		Task.Run(async () =>
		{
			try
			{
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    connection.Open();
                    string sql = @"CREATE TABLE IF NOT EXISTS `gang_group` (
                        `id` int(20) NOT NULL AUTO_INCREMENT,
                        `name` varchar(32) NOT NULL,
                        `exp` int(32) NOT NULL DEFAULT 0,
                        `server_id` int(16) NOT NULL DEFAULT 0,
                        `create_date` int(32) NOT NULL,
                        `end_date` int(32) NOT NULL,
                        PRIMARY KEY (id)
                    ) DEFAULT CHARSET=utf8 AUTO_INCREMENT=1;";
                    await connection.ExecuteAsync(sql);

                    sql = @"CREATE TABLE IF NOT EXISTS `gang_player` (
					    `id` int(20) NOT NULL AUTO_INCREMENT,
                        `gang_id` int(20) NOT NULL,
                        `steam_id` varchar(32) NOT NULL,
                        `name` varchar(32) NOT NULL,
                        `gang_hierarchy` int(16) NOT NULL,
                        `inviter_name` varchar(32) NULL DEFAULT NULL,
                        `invite_date` int(32) NOT NULL,
                        FOREIGN KEY (gang_id)  REFERENCES gang_group (id),
                        PRIMARY KEY (id)
                    ) DEFAULT CHARSET=utf8 AUTO_INCREMENT=1;";
                    await connection.ExecuteAsync(sql);

                    sql = @"CREATE TABLE IF NOT EXISTS `gang_perk` (
                        `id` int(20) NOT NULL AUTO_INCREMENT,
                        `gang_id` int(20) NOT NULL,
                        FOREIGN KEY (gang_id)  REFERENCES gang_group (id),
                        PRIMARY KEY (id)
                    ) DEFAULT CHARSET=utf8 AUTO_INCREMENT=1;";
                    await connection.ExecuteAsync(sql);
                }
			}
			catch (Exception ex)
			{
                Logger.LogError("{OnConfigParsed} Unable to connect to database! | " + ex.Message);
                throw new Exception("[Gangs] Unable to connect to Database! | " + ex.Message);
			}
		});

        Config = config;
        Config.Prefix = StringExtensions.ReplaceColorTags(Config.Prefix);
        Helper.UpdateConfig(config);
    }
}
