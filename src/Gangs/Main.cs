using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Core.Capabilities;
using MySqlConnector;
using Dapper;
using GangsAPI;
using StoreApi;
using TagsApi;

namespace Gangs;

public partial class Plugin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Gangs";
    public override string ModuleVersion => "0.1.7";
    public override string ModuleAuthor => "Faust, continued by exkludera";
    public static Plugin Instance { get; set; } = new();

    internal string dbConnectionString = string.Empty;
    public Dictionary<int, UserInfo> userInfo = new();
    public List<Gang> GangList = new();
    public List<Skill> SkillList = new();

    public Api.ApiGangs? _api;
    private readonly PluginCapability<GangsApi> _pluginCapability = new("Gangs_Core:API");

    public IStoreApi? StoreApi { get; set; }
    public ITagApi? TagApi { get; set; }

    public override void Load(bool hotReload)
    {
        Instance = this;

        _api = new Api.ApiGangs(this); 
        Capabilities.RegisterPluginCapability(_pluginCapability, () => _api);
        Server.NextWorldUpdate(() => _api.OnCoreReady());

        RegisterEvents();

        string[] commands = Config.Settings.MenuCommands.Split(';');
        foreach(var command in commands)
        {
            AddCommand(command, "Open gang menu", Menu.Command_OpenMenus!);
        }

        if (hotReload)
        {
            OnMapStart(string.Empty);

            foreach (var player in Utilities.GetPlayers())
            {
                if (player.AuthorizedSteamID != null)
                    OnClientAuthorized(player.Slot, player.AuthorizedSteamID);
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        UnregisterEvents();

        string[] commands = Config.Settings.MenuCommands.Split(';');
        foreach (var command in commands)
        {
            RemoveCommand(command, Menu.Command_OpenMenus!);
        }
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            StoreApi = IStoreApi.Capability.Get();
            TagApi = ITagApi.Capability.Get();
        }
        catch (Exception ex)
        {
            LogError("(OnAllPluginsLoaded) Failed load APIs | " + ex.Message);
            throw new Exception("Failed load APIs | " + ex.Message);
        }
    }

    public Config Config { get; set; } = new Config();
    public void OnConfigParsed(Config config)
    {        
        if (config.Database.Host.Length < 1 || config.Database.Name.Length < 1 || config.Database.User.Length < 1)
			throw new Exception("You need to setup Database info in config!");

        GangList.Clear();

        MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = config.Database.Host,
			Database = config.Database.Name,
			UserID = config.Database.User,
			Password = config.Database.Password,
			Port = (uint)config.Database.Port
		};

        dbConnectionString = builder.ConnectionString;

		Task.Run(async () =>
		{
			try
			{
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    connection.Open();
                    string sql = $@"CREATE TABLE IF NOT EXISTS `{Config.Database.TableGroups}` (
                        `id` int(20) NOT NULL AUTO_INCREMENT,
                        `name` varchar(32) NOT NULL,
                        `exp` int(32) NOT NULL DEFAULT 0,
                        `create_date` int(32) NOT NULL,
                        `end_date` int(32) NOT NULL,
                        PRIMARY KEY (id)
                    ) DEFAULT CHARSET=utf8mb4 AUTO_INCREMENT=1;";
                    await connection.ExecuteAsync(sql);

                    sql = $@"CREATE TABLE IF NOT EXISTS `{Config.Database.TablePlayers}` (
					    `id` int(20) NOT NULL AUTO_INCREMENT,
                        `gang_id` int(20) NOT NULL,
                        `steam_id` bigint(17) NOT NULL,
                        `name` varchar(32) NOT NULL,
                        `gang_hierarchy` int(16) NOT NULL,
                        `inviter_name` varchar(32) NULL DEFAULT NULL,
                        `invite_date` int(32) NOT NULL,
                        FOREIGN KEY (gang_id)  REFERENCES {Config.Database.TableGroups} (id),
                        PRIMARY KEY (id)
                    ) DEFAULT CHARSET=utf8mb4 AUTO_INCREMENT=1;";
                    await connection.ExecuteAsync(sql);

                    sql = $@"CREATE TABLE IF NOT EXISTS `{Config.Database.TablePerks}` (
                        `id` int(20) NOT NULL AUTO_INCREMENT,
                        `gang_id` int(20) NOT NULL,
                        FOREIGN KEY (gang_id)  REFERENCES {Config.Database.TableGroups} (id),
                        PRIMARY KEY (id)
                    ) DEFAULT CHARSET=utf8mb4 AUTO_INCREMENT=1;";
                    await connection.ExecuteAsync(sql);
                }
			}
			catch (Exception ex)
			{
                LogError("(OnConfigParsed) Unable to connect to database! | " + ex.Message);
                throw new Exception("Unable to connect to Database! | " + ex.Message);
			}
		});

        Config = config;
        Config.Prefix = StringExtensions.ReplaceColorTags(Config.Prefix);
    }
}
