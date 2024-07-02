using System.Data;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using GangsAPI;
using CounterStrikeSharp.API.Core.Capabilities;
using StoreApi;
using System.Text.RegularExpressions;

namespace Gangs;
public partial class Gangs : BasePlugin, IPluginConfig<GangsConfig>
{
    public override string ModuleName => "Gangs";
    public override string ModuleVersion => "0.1.2";
    public override string ModuleAuthor => "Faust, modified by exkludera";

    public GangsConfig Config {get; set; } = new();

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

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var player = @event.Attacker;

            if(player == null || !player.IsValid || player.IsBot) 
                return HookResult.Continue;

            AddScoreboardTagToPlayer(@event.Userid!);

            var slot = player.Slot;

            var gang = GangList.Find(x => x.DatabaseID == userInfo[slot].GangId);

            if(gang == null)
                return HookResult.Continue;

            if(NeedExtendGang(gang))
                return HookResult.Continue;
            
            gang.Exp += 1;

            return HookResult.Continue;
        });

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
		Helper.UpdateConfig(config);
    }

    #region Menus
    public void CommandGang(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;
        
        var slot = player.Slot;

        var menu = new ChatMenu(Localizer["menu<title>"]);

        if (userInfo[slot].DatabaseID != -1)
        {
            var gang = GangList.Find(x => x.DatabaseID == userInfo[slot].GangId);

            if (gang != null)
            {
                var days = Helper.ConvertUnixToDateTime(gang.EndDate).Subtract(DateTime.Now).Days;

                if (Config.ExtendCost.Value.Count > 0)
                    menu.Title = Localizer["menu<title_with_days>", gang.Name, days];
                else
                    menu.Title = Localizer["menu<title_with_name>", gang.Name];

                menu.AddMenuOption(Localizer["menu<statistic>"], (player, option) => {
                    OpenStatisticMenu(player, gang);
                });

                menu.AddMenuOption(Localizer["menu<skills>"], (player, option) =>
                {
                    var skillsMenu = new ChatMenu(Localizer["menu<skills>"]);

                    foreach (var skill in gang.SkillList)
                    {
                        if (StoreApi != null)
                        {
                            skillsMenu.AddMenuOption(Localizer["menu<skill_info>", Localizer[skill.Name], skill.Level, skill.MaxLevel, skill.Price], (player, option) =>
                            {
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        if (skill.Level < skill.MaxLevel)
                                        {
                                            await using (var connection = new MySqlConnection(dbConnectionString))
                                            {
                                                await connection.OpenAsync();
                                                skill.Level += 1;
                                                await connection.ExecuteAsync($"UPDATE `gang_perk` SET `{skill.Name}` = {skill.Level} WHERE `gang_id` = {gang.DatabaseID};");

                                                Server.NextFrame(() => {
                                                    StoreApi.GivePlayerCredits(player, -skill.Price);
                                                    player.PrintToChat($" {Localizer["Prefix"]} { Localizer["chat<skill_success_buy>", Localizer[skill.Name]]}");
                                                });
                                            }
                                        }
                                        else
                                        {
                                            Server.NextFrame(() => {
                                                player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<skill_max_lvl>"]}");
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("{CommandGang} Fail skill gang! | " + ex.Message);
                                        throw new Exception("[Gangs] Fail skill gang! | " + ex.Message);
                                    }
                                });
                            }, StoreApi.GetPlayerCredits(player) < skill.Price || skill.Level >= skill.MaxLevel);
                        }
                    }

                    MenuManager.OpenChatMenu(player, skillsMenu);
                }, NeedExtendGang(gang));

                menu.AddMenuOption(Localizer["menu<admin_panel>"], (player, option) => {
                    OpenAdminMenu(player);
                }, userInfo[slot].Rank == 0 ? false : true);

                menu.AddMenuOption(Localizer["menu<leave>"], (player, option) =>
                {
                    var acceptMenu = new ChatMenu(Localizer["menu<leave_accept>"]);
                    acceptMenu.AddMenuOption(Localizer["Yes"], (player, option) =>
                    {
                        Task.Run(async () =>
                        {
                            try
                            { 
                                await using (var connection = new MySqlConnection(dbConnectionString))
                                {
                                    await connection.OpenAsync();

                                    await connection.ExecuteAsync(@"
                                        DELETE FROM `gang_player`
                                        WHERE `id` = @gId;", 
                                        new{ gId = userInfo[player.Slot].DatabaseID });

                                    var steamID = userInfo[slot].SteamID;
                                    userInfo[slot] = new UserInfo{ SteamID = steamID };

                                    Server.NextFrame(() => {
                                        player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<leave_success>"]}");
                                        AddScoreboardTagToPlayer(player);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("{CommandGang} Fail leave gang! | " + ex.Message);
                                throw new Exception("[Gangs] Fail leave gang! | " + ex.Message);
                            }
                        });
                    });
                    acceptMenu.AddMenuOption(Localizer["No"], (invited, option) => {
                        MenuManager.OpenChatMenu(player, menu);
                    });

                    MenuManager.OpenChatMenu(player, acceptMenu);    
                }, userInfo[slot].Rank > 0 ? false : true);

                menu.AddMenuOption(Localizer["menu<top>"], (player, option) =>
                {
                    var topGangsMenu = new ChatMenu(Localizer["menu<top>"]);
                    var Gangs = from gang in GangList orderby gang.Exp select gang;

                    foreach (var gang in Gangs)
                    {
                        topGangsMenu.AddMenuOption(Localizer["menu<top_info>", gang.Name, GetGangLevel(gang)], (player, option) =>
                        {
                            OpenStatisticMenu(player, gang);
                        });
                    }

                    topGangsMenu.ExitButton = true;

                    MenuManager.OpenChatMenu(player, topGangsMenu);
                });
            }
        }

        else
        {
            if(Config.CreateCost.Value > 0)
            {
                if(StoreApi != null)
                {
                    menu.AddMenuOption(Localizer["menu<create_with_credits>", Config.CreateCost.Value], (player, option) =>
                    {
                        userInfo[slot].Status = 1;
                        player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<create_name>"]}");
                    }, StoreApi.GetPlayerCredits(player) < Config.CreateCost.Value);
                }
            }
            else
            {
                menu.AddMenuOption(Localizer["menu<create>"], (player, option) =>
                {
                    userInfo[slot].Status = 1;
                    player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<create_name>"]}");
                });
            }
        }

        if(menu.MenuOptions.Count == 0)
            menu.AddMenuOption(Localizer["Oops"], (player, option) => { }, true);

        MenuManager.OpenChatMenu(player, menu);
    }
    public void OpenAdminMenu(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot) return;
        
        var slot = player.Slot;
        
        var gang = GangList.Find(x => x.DatabaseID == userInfo[slot].GangId);

        if(gang == null)
            return;

        var menu = new ChatMenu(Localizer["menu<title_with_name>", gang.Name]);
        var sizeSkill = gang.SkillList.Find(x=>x.Name.Equals("size"));

        menu.AddMenuOption(Localizer["menu<invite>"], (player, option) =>
        {
            var usersMenu = new ChatMenu(Localizer["menu<players>"]);
            
            foreach (var user in Utilities.GetPlayers())
            {
                if (user == null || !user.IsValid || user.IsBot)
                    continue;
                
                if(userInfo[user.Slot].DatabaseID == -1)
                {
                    usersMenu.AddMenuOption($"{user.PlayerName}", (inviter, option) =>
                    {
                        inviter.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<invite_sent>", user.PlayerName]}");
                        var acceptMenu = new ChatMenu(Localizer["menu<invite_came>", gang.Name]);

                        acceptMenu.AddMenuOption(Localizer["Accept"], (invited, option) =>
                        {
                            if(invited.AuthorizedSteamID != null)
                            {
                                var l_steamId = invited.AuthorizedSteamID.SteamId2;
                                var l_playerName = invited.PlayerName;
                                var l_inviterName = inviter.PlayerName;
                                var l_inviteDate = Helper.GetNowUnixTime();
                                Task.Run(async () =>
                                {
                                    try
                                    { 
                                        await using (var connection = new MySqlConnection(dbConnectionString))
                                        {
                                            await connection.OpenAsync();
                                            var sql = $"INSERT INTO `gang_player` (`gang_id`, `steam_id`, `name`, `gang_hierarchy`, `inviter_name`, `invite_date`) VALUES ({gang.DatabaseID}, '{l_steamId}', '{l_playerName}', 3, '{l_inviterName}', {l_inviteDate});";
                                            await connection.ExecuteAsync(sql);
                                            var command = connection.CreateCommand();
                                            sql = $"SELECT `id` FROM `gang_player` WHERE `steam_id` = '{l_steamId}'";
                                            command.CommandText = sql;
                                            var reader = await command.ExecuteReaderAsync();

                                            if (await reader.ReadAsync())
                                            {
                                                userInfo[user.Slot] = new UserInfo
                                                {
                                                    SteamID = l_steamId,
                                                    Status = 0,
                                                    DatabaseID = reader.GetInt32(0),
                                                    GangId = gang.DatabaseID,
                                                    Rank = 3,
                                                    InviterName = l_inviterName,
                                                    InviteDate = l_inviteDate
                                                };
                                                reader.Close();
                                                gang.MembersList.Add(userInfo[user.Slot]);
                                                Server.NextFrame(() => {
                                                    invited.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<invite_welcome>", gang.Name]}");
                                                    inviter.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<invite_accept>", invited.PlayerName]}");
                                                    AddScoreboardTagToPlayer(invited);
                                                });
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("{OpenAdminMenu} Failed invite in database | " + ex.Message);
                                        throw new Exception("[Gangs] Failed invite in database! | " + ex.Message);
                                    }
                                });
                            }
                        });
                        acceptMenu.ExitButton = true;
                        MenuManager.OpenChatMenu(user, acceptMenu);
                    });
                }
            }
            if(usersMenu.MenuOptions.Count > 0)
                MenuManager.OpenChatMenu(player, usersMenu);
            else
                player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<no_players>"]}");

        }, NeedExtendGang(gang) || (sizeSkill != null && gang.MembersList.Count >= (Config.MaxMembers+sizeSkill.Level)) || gang.MembersList.Count >= Config.MaxMembers);
        if (Config.ExtendCost.Value.Count > 0 && StoreApi != null)
        {
            menu.AddMenuOption(Localizer["menu<extend>"], (player, option) =>
            {
                var pricesMenu = new ChatMenu(Localizer["menu<extend_date>"]);

                foreach (var price in Config.ExtendCost.Value)
                {
                    pricesMenu.AddMenuOption(Localizer["menu<extend_select_date>", price.Day, price.Value], (player, option) =>
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await using (var connection = new MySqlConnection(dbConnectionString))
                                {
                                    await connection.OpenAsync();
                                    
                                    var gang = GangList.Find(x=>x.DatabaseID==userInfo[player.Slot].GangId);
                                    if(gang!= null)
                                    {
                                        var addDay = Helper.ConvertUnixToDateTime(gang.EndDate).AddDays(Convert.ToInt32(price.Day));
                                        var newDate = (int)(addDay.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                                        await connection.ExecuteAsync($"UPDATE `gang_group` SET `end_date` = {newDate} WHERE `id` = {gang.DatabaseID}");
                                        gang.EndDate = newDate;

                                        Server.NextFrame(() => {
                                            StoreApi.GivePlayerCredits(player, -price.Value);
                                            player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<extend_success>"]}");
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("{OpenAdminMenu} Failed extend gang! | " + ex.Message);
                                throw new Exception("[Gangs] Failed extend gang! | " + ex.Message);
                            }
                        });
                    }, StoreApi.GetPlayerCredits(player) < price.Value);
                }

                pricesMenu.ExitButton = true;
                MenuManager.OpenChatMenu(player, pricesMenu);
            });
        }

        if(Config.RenameCost.Value > 0)
        {
            if(StoreApi != null)
            {
                menu.AddMenuOption(Localizer["menu<rename_with_credits>", Config.RenameCost.Value], (player, option) =>
                {
                    userInfo[slot].Status = 2;
                    player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<rename_print>"]}");
                }, NeedExtendGang(gang) || StoreApi.GetPlayerCredits(player) < Config.RenameCost.Value);
            }
        }
        else
        {
            menu.AddMenuOption(Localizer["menu<rename>"], (player, option) =>
            {
                userInfo[slot].Status = 2;
                player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<rename_print>"]}");
            }, NeedExtendGang(gang));
        }

        menu.AddMenuOption(Localizer["menu<leader>"], (player, option) =>
        {
            var usersMenu = new ChatMenu(Localizer["menu<players>"]);
            
            Dictionary<int, string> users = new Dictionary<int, string>();

            Task.Run(async () =>
            {
                try
                {
                    await using (var connection = new MySqlConnection(dbConnectionString))
                    {
                        await connection.OpenAsync();
                        var command = connection.CreateCommand();
                        string sql = $"SELECT `id`, `name` FROM `gang_player` WHERE `id` <> {userInfo[player.Slot].DatabaseID} AND `gang_id` = {userInfo[player.Slot].GangId};";
                        command.CommandText = sql;
                        var reader = await command.ExecuteReaderAsync();

                        while(await reader.ReadAsync()) {
                            users.Add(reader.GetInt32(0), reader.GetString(1));
                        }

                        reader.Close();

                        foreach(var user in users)
                        {
                            usersMenu.AddMenuOption($"{user.Value}", (player, option) =>
                            {
                                var acceptMenu = new ChatMenu(Localizer["menu<leader_sure>", user.Value]);
                                acceptMenu.AddMenuOption(Localizer["Yes"], (player, option) =>
                                {
                                    Task.Run(async () =>
                                    {
                                        try
                                        { 
                                            await using (var connection = new MySqlConnection(dbConnectionString))
                                            {
                                                await connection.OpenAsync();

                                                await connection.ExecuteAsync($"UPDATE `gang_player` SET `gang_hierarchy` = 0 WHERE `id` = {user.Key}");
                                                await connection.ExecuteAsync($"UPDATE `gang_player` SET `gang_hierarchy` = 1 WHERE `id` = {userInfo[player.Slot].DatabaseID}");

                                                userInfo[player.Slot].Rank = 1;
                                                Server.NextFrame(() => {
                                                    foreach(var player in Utilities.GetPlayers())
                                                    {
                                                        if(userInfo[player.Slot].DatabaseID == user.Key)
                                                        {
                                                            userInfo[player.Slot].Rank = 0;
                                                            player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<leader_new>"]}");
                                                            break;
                                                        }
                                                    }
                                                    player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<leader_complete>", user.Value]}");
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.LogError("{CommandGang} Fail transfer leader! | " + ex.Message);
                                            throw new Exception("[Gangs] Fail transfer leader! | " + ex.Message);
                                        }
                                    });
                                });
                                acceptMenu.AddMenuOption(Localizer["No"], (invited, option) => {
                                    MenuManager.OpenChatMenu(player, menu);
                                });
                                MenuManager.OpenChatMenu(player, acceptMenu);
                            });        
                        }
                        Server.NextFrame(() => {
                            if(usersMenu.MenuOptions.Count > 0) MenuManager.OpenChatMenu(player, usersMenu);
                            else player.PrintToChat($" {Localizer["Prefix"]} {Localizer["chat<no_players>"]}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("{OpenAdminMenu} Failed check players to transfer leader in database | " + ex.Message);
                    throw new Exception("[Gangs] Failed check players to transfer leader in database! | " + ex.Message);
                }
            });
        }, NeedExtendGang(gang));
        if(userInfo[slot].Rank == 0)
        {    
            menu.AddMenuOption(Localizer["menu<disband>"], (player, option) =>
            {
                var confirmMenu = new ChatMenu(Localizer["Sure"]);

                confirmMenu.AddMenuOption(Localizer["Yes"], (player, option) =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await using (var connection = new MySqlConnection(dbConnectionString))
                            {
                                await connection.OpenAsync();

                                await connection.ExecuteAsync(@"DELETE FROM `gang_player` WHERE `gang_id` = @gId;", new{ gId = gang.DatabaseID });

                                await connection.ExecuteAsync(@"DELETE FROM `gang_perk` WHERE `gang_id` = @gId;", new{ gId = gang.DatabaseID });
                                        
                                await connection.ExecuteAsync(@"DELETE FROM `gang_group` WHERE `id` = @gId AND `server_id` = @sId;", new{ gId = gang.DatabaseID, sId = Config.ServerId });

                                Server.NextFrame(() => {
                                    Server.PrintToChatAll($" {Localizer["Prefix"]} {Localizer["chat<disband_announce>", player.PlayerName, gang.Name]}");
                                    foreach (var user in Utilities.GetPlayers())
                                    {
                                        if (user == null || !user.IsValid || user.IsBot) continue;
                                        var slot = user.Slot;
                                        if(userInfo[slot].GangId == gang.DatabaseID)
                                        {
                                            var steamID = userInfo[slot].SteamID;
                                            userInfo[slot] = new UserInfo{ SteamID = steamID };
                                        };
                                    }
                                    GangList.Remove(gang);
                                    AddScoreboardTagToPlayer(player);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("{OpenAdminMenu} Failed dissolve in database! | " + ex.Message);
                            throw new Exception("[Gangs] Failed dissolve in database! | " + ex.Message);
                        }
                    });
                });

                confirmMenu.AddMenuOption(Localizer["Cancel"], (player, option) => {
                    MenuManager.OpenChatMenu(player, menu);
                });  

                MenuManager.OpenChatMenu(player, confirmMenu);
            });
        }
        menu.ExitButton = true;
        MenuManager.OpenChatMenu(player, menu);
    }

    public void OpenStatisticMenu(CCSPlayerController? player, Gang gang)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;
        
        Task.Run(async () =>
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    var countmembers = await connection.QueryAsync(@"
                        SELECT COUNT(*) as Count
                        FROM `gang_player`
                        WHERE `gang_id` = @gangid", 
                        new { gangid = gang.DatabaseID });

                        var data = (IDictionary<string,object>)countmembers.First();
                        var count = data["Count"];

                    var owner = await connection.QueryAsync(@"
                        SELECT `name`
                        FROM `gang_player`
                        WHERE `gang_id` = @gangid AND `gang_hierarchy` = 0",
                        new { gangid = gang.DatabaseID });

                        data = (IDictionary<string,object>)owner.First();
                        var name = data["name"];

                    Server.NextFrame(() => {
                        var statmenu = new ChatMenu(Localizer["menu<statistic_title>"]);

                        int level = GetGangLevel(gang);
                        int needexp = level * Config.ExpInc + Config.ExpInc;

                        statmenu.AddMenuOption(Localizer["menu<statistic_name>", gang.Name], (player, option) => { }, true);

                        statmenu.AddMenuOption(Localizer["menu<statistic_leader>", name], (player, option) => { }, true);

                        statmenu.AddMenuOption(Localizer["menu<statistic_num_players>", count], (player, option) => { }, true);

                        statmenu.AddMenuOption(Localizer["menu<statistic_lvl>", level, gang.Exp, needexp], (player, option) => { }, true);

                        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                        dateTime = dateTime.AddSeconds(gang.CreateDate).ToLocalTime();
                        var date = dateTime.ToString("dd.MM.yyyy") + " " + dateTime.ToString("HH:mm");

                        statmenu.AddMenuOption(Localizer["menu<statistic_create_date>", date], (player, option) => { }, true);

                        MenuManager.OpenChatMenu(player, statmenu);
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        });
    }
    #endregion

    public async Task EnsureColumnExists(string skillName)
    {
        try
        {
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();
                string checkColumnSql = $"SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'gang_perk' AND column_name = '{skillName}';";
                var checkColumnCommand = connection.CreateCommand();
                checkColumnCommand.CommandText = checkColumnSql;
                int columnExists = Convert.ToInt32(await checkColumnCommand.ExecuteScalarAsync());

                if (columnExists == 0)
                {
                    string addColumnSql = $"ALTER TABLE `gang_perk` ADD COLUMN `{skillName}` int(32) NOT NULL DEFAULT 0;";
                    var addColumnCommand = connection.CreateCommand();
                    addColumnCommand.CommandText = addColumnSql;
                    await addColumnCommand.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to ensure column existence | " + ex.Message);
            throw new Exception("[Gangs] Failed to ensure column existence! | " + ex.Message);
        }
    }

    public async Task AddSkillInDB(string skillName, int maxLevel, int price)
    {
        await EnsureColumnExists(skillName);

        try
        {
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();

                foreach (var gang in GangList)
                {
                    try
                    {
                        string sql = $"SELECT `{skillName}` FROM `gang_perk` WHERE `gang_id` = {gang.DatabaseID};";
                        var command = connection.CreateCommand();
                        command.CommandText = sql;
                        var reader = await command.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            gang.SkillList.Add(new Skill(skillName, reader.GetInt32(0), maxLevel, price));
                        }
                        reader.Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to add skill in database 2! | " + ex.Message);
                        throw new Exception("[Gangs] Failed to add skill in database 2! | " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to add skill in database | " + ex.Message);
            throw new Exception("[Gangs] Failed to add skill in database! | " + ex.Message);
        }
    }

    public async Task AddSkillInDB(int gangID, string skillName, int maxLevel, int price)
    {
        await EnsureColumnExists(skillName);

        try
        {
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();

                try
                {
                    var gang = GangList.Find(x => x.DatabaseID == gangID);
                    if (gang != null)
                    {
                        string sql = $"SELECT `{skillName}` FROM `gang_perk` WHERE `gang_id` = {gang.DatabaseID};";
                        var command = connection.CreateCommand();
                        command.CommandText = sql;
                        var reader = await command.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            gang.SkillList.Add(new Skill(skillName, reader.GetInt32(0), maxLevel, price));
                        }
                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to add skill in database 2! | " + ex.Message);
                    throw new Exception("[Gangs] Failed to add skill in database 2! | " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to add skill in database | " + ex.Message);
            throw new Exception("[Gangs] Failed to add skill in database! | " + ex.Message);
        }
    }

    public void AddScoreboardTagToPlayer(CCSPlayerController player)
    {
        if (!Config.ClanTags)
            return;

        var gang = GangList.Find(x => x.DatabaseID == userInfo[player.Slot].GangId);
        try
        {
            AddTimer(3.0f, () =>
            {
                if (string.IsNullOrEmpty(gang?.name))
                    return;

                if (player == null || !player.IsValid || player.IsBot)
                    return;

                string originalPlayerName = player.PlayerName;

                string stripedClanTag = RemovePlayerTags(player.Clan ?? "");

                player.Clan = $"{stripedClanTag} [{gang.name}]";

                player.PlayerName = originalPlayerName + " ";

                AddTimer(0.1f, () =>
                {
                    if (player.IsValid)
                    {
                        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
                        Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
                    }
                });

                AddTimer(0.2f, () =>
                {
                    if (player.IsValid) player.PlayerName = originalPlayerName;
                });

                AddTimer(0.3f, () =>
                {
                    if (player.IsValid) Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
                });
            });
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in AddScoreboardTagToPlayer | " + ex.Message);
        }
    }

    public string RemovePlayerTags(string input)
    {
        List<string> playerTagsToRemove = new List<string>();

        foreach (var gang in GangList)
        {
            if (!string.IsNullOrEmpty(gang.Name))
            {
                playerTagsToRemove.Add($"[{gang.Name}]");
            }
        }

        if (!string.IsNullOrEmpty(input))
        {
            foreach (var strToRemove in playerTagsToRemove)
            {
                if (input.Contains(strToRemove))
                {
                    input = Regex.Replace(input, Regex.Escape(strToRemove), string.Empty, RegexOptions.IgnoreCase).Trim();
                }
            }
        }

        return input;
    }

    public int GetMembersCount(int gang_id)
    {
        int iCount = 0;
        Task.Run(async () =>
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    var countmembers = await connection.QueryAsync(@"
                        SELECT COUNT(*) as Count FROM `gang_player` WHERE `gang_id` = @gangid", 
                        new { gangid = gang_id });
                        var data = (IDictionary<string,object>)countmembers.First();
                        var count = data["Count"];
                    
                    iCount = (int)count;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{GetMembersCount} Failed get value in database | " + ex.Message);
                throw new Exception("[Gangs] Failed get value in database! | " + ex.Message);
            }
        });
        return iCount;
    }
    public int GetGangLevel(Gang gang)
    {
        if (gang == null) 
            return -1;

        return gang.Exp / Config.ExpInc;
    }
    public bool NeedExtendGang(Gang gang)
    {
        if (gang == null)
            return true;

        if (Config.CreateCost.Days == 0)
            return false;

        return gang.EndDate < Helper.GetNowUnixTime();
    }
}
