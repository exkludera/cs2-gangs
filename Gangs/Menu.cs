using System.Data;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Gangs;

public partial class Gangs : BasePlugin, IPluginConfig<GangsConfig>
{
    public void CommandGang(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        if (_api!.OnlyTerroristCheck(player))
        {
            PrintToChat(player, Localizer["chat<only_terrorists>"]);
            return;
        }

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
                                                await connection.ExecuteAsync($"UPDATE `{Config.Database.TablePerks}` SET `{skill.Name}` = {skill.Level} WHERE `gang_id` = {gang.DatabaseID};");

                                                Server.NextFrame(() => {
                                                    StoreApi.GivePlayerCredits(player, -skill.Price);
                                                    PrintToChat(player, Localizer["chat<skill_success_buy>", Localizer[skill.Name]]);
                                                });
                                            }
                                        }
                                        else
                                        {
                                            Server.NextFrame(() => {
                                                PrintToChat(player, Localizer["chat<skill_max_lvl>"]);
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogError("(CommandGang) Fail skill gang! | " + ex.Message);
                                        throw new Exception("Fail skill gang! | " + ex.Message);
                                    }
                                });
                            }, StoreApi.GetPlayerCredits(player) < skill.Price || skill.Level >= skill.MaxLevel);
                        }
                    }

                    MenuManager.OpenChatMenu(player, skillsMenu);
                }, NeedExtendGang(gang));

                menu.AddMenuOption(Localizer["menu<leader_panel>"], (player, option) => {
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

                                    await connection.ExecuteAsync($@"
                                        DELETE FROM `{Config.Database.TablePlayers}`
                                        WHERE `id` = @gId;",
                                        new { gId = userInfo[player.Slot].DatabaseID });

                                    var steamID = userInfo[slot].SteamID;
                                    userInfo[slot] = new UserInfo { SteamID = steamID };

                                    Server.NextFrame(() => {
                                        PrintToChat(player, Localizer["chat<leave_success>"]);
                                        AddScoreboardTagToPlayer(player);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("(CommandGang) Failed to leave gang! | " + ex.Message);
                                throw new Exception("Failed to leave gang! | " + ex.Message);
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
            if (Config.CreateCost.Value > 0)
            {
                if (StoreApi != null)
                {
                    menu.AddMenuOption(Localizer["menu<create_with_credits>", Config.CreateCost.Value], (player, option) =>
                    {
                        userInfo[slot].Status = 1;
                        PrintToChat(player, Localizer["chat<create_name>"]);
                    }, StoreApi.GetPlayerCredits(player) < Config.CreateCost.Value);
                }
            }
            else
            {
                menu.AddMenuOption(Localizer["menu<create>"], (player, option) =>
                {
                    userInfo[slot].Status = 1;
                    PrintToChat(player, Localizer["chat<create_name>"]);
                });
            }
        }

        if (menu.MenuOptions.Count == 0)
            menu.AddMenuOption(Localizer["Oops"], (player, option) => { }, true);

        MenuManager.OpenChatMenu(player, menu);
    }
    public void OpenAdminMenu(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot) return;

        var slot = player.Slot;

        var gang = GangList.Find(x => x.DatabaseID == userInfo[slot].GangId);

        if (gang == null)
            return;

        var menu = new ChatMenu(Localizer["menu<title_with_name>", gang.Name]);

        var sizeSkill = gang.SkillList.Find(x => x.Name.Equals("size"));

        menu.AddMenuOption(Localizer["menu<invite>"], (player, option) =>
        {
            var usersMenu = new ChatMenu(Localizer["menu<players>"]);

            foreach (var user in Utilities.GetPlayers())
            {
                if (user == null || !user.IsValid || user.IsBot)
                    continue;

                if (userInfo[user.Slot].DatabaseID == -1)
                {
                    usersMenu.AddMenuOption($"{user.PlayerName}", (inviter, option) =>
                    {
                        PrintToChat(inviter, Localizer["chat<invite_sent>", user.PlayerName]);
                        var acceptMenu = new ChatMenu(Localizer["menu<invite_came>", gang.Name]);

                        acceptMenu.AddMenuOption(Localizer["Accept"], (invited, option) =>
                        {
                            if (invited.AuthorizedSteamID != null)
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
                                            var sql = $"INSERT INTO `{Config.Database.TablePlayers}` (`gang_id`, `steam_id`, `name`, `gang_hierarchy`, `inviter_name`, `invite_date`) VALUES ({gang.DatabaseID}, '{l_steamId}', '{l_playerName}', 3, '{l_inviterName}', {l_inviteDate});";
                                            await connection.ExecuteAsync(sql);
                                            var command = connection.CreateCommand();
                                            sql = $"SELECT `id` FROM `{Config.Database.TablePlayers}` WHERE `steam_id` = '{l_steamId}'";
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
                                                    PrintToChat(invited, Localizer["chat<invite_welcome>", gang.Name]);
                                                    PrintToChat(inviter, Localizer["chat<invite_accept>", invited.PlayerName]);
                                                    AddScoreboardTagToPlayer(invited);
                                                });
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogError("(OpenAdminMenu) Failed invite in database | " + ex.Message);
                                        throw new Exception("Failed invite in database! | " + ex.Message);
                                    }
                                });
                            }
                        });
                        acceptMenu.ExitButton = true;
                        MenuManager.OpenChatMenu(user, acceptMenu);
                    });
                }
            }
            if (usersMenu.MenuOptions.Count > 0)
                MenuManager.OpenChatMenu(player, usersMenu);
            else
                PrintToChat(player, Localizer["chat<no_players>"]);

        }, NeedExtendGang(gang) || (sizeSkill != null && gang.MembersList.Count >= (Config.Settings.MaxMembers + sizeSkill.Level)) || gang.MembersList.Count >= Config.Settings.MaxMembers);
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

                                    var gang = GangList.Find(x => x.DatabaseID == userInfo[player.Slot].GangId);
                                    if (gang != null)
                                    {
                                        var addDay = Helper.ConvertUnixToDateTime(gang.EndDate).AddDays(Convert.ToInt32(price.Day));
                                        var newDate = (int)(addDay.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                                        await connection.ExecuteAsync($"UPDATE `{Config.Database.TableGroups}` SET `end_date` = {newDate} WHERE `id` = {gang.DatabaseID}");
                                        gang.EndDate = newDate;

                                        Server.NextFrame(() => {
                                            StoreApi.GivePlayerCredits(player, -price.Value);
                                            PrintToChat(player, Localizer["chat<extend_success>"]);
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("OpenAdminMenu) Failed extend gang! | " + ex.Message);
                                throw new Exception("Failed extend gang! | " + ex.Message);
                            }
                        });
                    }, StoreApi.GetPlayerCredits(player) < price.Value);
                }

                pricesMenu.ExitButton = true;
                MenuManager.OpenChatMenu(player, pricesMenu);
            });
        }

        if (Config.RenameCost > 0)
        {
            if (StoreApi != null)
            {
                menu.AddMenuOption(Localizer["menu<rename_with_credits>", Config.RenameCost], (player, option) =>
                {
                    userInfo[slot].Status = 2;
                    PrintToChat(player, Localizer["chat<rename_print>"]);
                }, NeedExtendGang(gang) || StoreApi.GetPlayerCredits(player) < Config.RenameCost);
            }
        }
        else
        {
            menu.AddMenuOption(Localizer["menu<rename>"], (player, option) =>
            {
                userInfo[slot].Status = 2;
                PrintToChat(player, Localizer["chat<rename>"]);
            }, NeedExtendGang(gang));
        }

        menu.AddMenuOption(Localizer["menu<kick>"], (player, option) =>
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
                        string sql = $"SELECT `id`, `name` FROM `{Config.Database.TablePlayers}` WHERE `id` <> {userInfo[player.Slot].DatabaseID} AND `gang_id` = {userInfo[player.Slot].GangId};";
                        command.CommandText = sql;
                        var reader = await command.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            users.Add(reader.GetInt32(0), reader.GetString(1));
                        }

                        reader.Close();

                        foreach (var user in users)
                        {
                            usersMenu.AddMenuOption($"{user.Value}", (player, option) =>
                            {
                                var acceptMenu = new ChatMenu(Localizer["menu<kick_sure>", user.Value]);
                                acceptMenu.AddMenuOption(Localizer["Yes"], (player, option) =>
                                {
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await using (var connection = new MySqlConnection(dbConnectionString))
                                            {
                                                await connection.OpenAsync();

                                                await connection.ExecuteAsync($@"DELETE FROM `{Config.Database.TablePlayers}` WHERE `id` = @gId;",
                                                    new { gId = user.Key });

                                                Server.NextFrame(() => {
                                                    foreach (var player in Utilities.GetPlayers())
                                                    {
                                                        if (userInfo[player.Slot].DatabaseID == user.Key)
                                                        {
                                                            var steamID = userInfo[player.Slot].SteamID;
                                                            userInfo[player.Slot] = new UserInfo { SteamID = steamID };
                                                            PrintToChat(player, Localizer["chat<kick_message>"]);
                                                            AddScoreboardTagToPlayer(player);
                                                            break;
                                                        }
                                                    }
                                                    PrintToChat(player, Localizer["chat<kick_complete>", user.Value]);
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogError("(CommandGang) Fail kick from gang! | " + ex.Message);
                                            throw new Exception("Fail kick from gang! | " + ex.Message);
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
                            if (usersMenu.MenuOptions.Count > 0) MenuManager.OpenChatMenu(player, usersMenu);
                            else PrintToChat(player, Localizer["chat<no_players>"]);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogError("(OpenAdminMenu) Failed check players to kick from gang in database | " + ex.Message);
                    throw new Exception("Failed check players to kick from gang in database! | " + ex.Message);
                }
            });
        }, NeedExtendGang(gang));

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
                        string sql = $"SELECT `id`, `name` FROM `{Config.Database.TablePlayers}` WHERE `id` <> {userInfo[player.Slot].DatabaseID} AND `gang_id` = {userInfo[player.Slot].GangId};";
                        command.CommandText = sql;
                        var reader = await command.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            users.Add(reader.GetInt32(0), reader.GetString(1));
                        }

                        reader.Close();

                        foreach (var user in users)
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

                                                await connection.ExecuteAsync($"UPDATE `{Config.Database.TablePlayers}` SET `gang_hierarchy` = 0 WHERE `id` = {user.Key}");
                                                await connection.ExecuteAsync($"UPDATE `{Config.Database.TablePlayers}` SET `gang_hierarchy` = 1 WHERE `id` = {userInfo[player.Slot].DatabaseID}");

                                                userInfo[player.Slot].Rank = 1;
                                                Server.NextFrame(() => {
                                                    foreach (var player in Utilities.GetPlayers())
                                                    {
                                                        if (userInfo[player.Slot].DatabaseID == user.Key)
                                                        {
                                                            userInfo[player.Slot].Rank = 0;
                                                            PrintToChat(player, Localizer["chat<leader_new>"]);
                                                            break;
                                                        }
                                                    }
                                                    PrintToChat(player, Localizer["chat<leader_complete>", user.Value]);
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogError("(CommandGang) Fail transfer leader! | " + ex.Message);
                                            throw new Exception("Fail transfer leader! | " + ex.Message);
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
                            if (usersMenu.MenuOptions.Count > 0) MenuManager.OpenChatMenu(player, usersMenu);
                            else PrintToChat(player, Localizer["chat<no_players>"]);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogError("(OpenAdminMenu) Failed check players to transfer leader in database | " + ex.Message);
                    throw new Exception("Failed check players to transfer leader in database! | " + ex.Message);
                }
            });
        }, NeedExtendGang(gang));
        if (userInfo[slot].Rank == 0)
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

                                await connection.ExecuteAsync($@"DELETE FROM `{Config.Database.TablePlayers}` WHERE `gang_id` = @gId;", new { gId = gang.DatabaseID });

                                await connection.ExecuteAsync($@"DELETE FROM `{Config.Database.TablePerks}` WHERE `gang_id` = @gId;", new { gId = gang.DatabaseID });

                                await connection.ExecuteAsync($@"DELETE FROM `{Config.Database.TableGroups}` WHERE `id` = @gId AND `server_id` = @sId;", new { gId = gang.DatabaseID });

                                Server.NextFrame(() => {
                                    PrintToChatAll(Localizer["chat<disband_announce>", player.PlayerName, gang.Name]);
                                    foreach (var user in Utilities.GetPlayers())
                                    {
                                        if (user == null || !user.IsValid || user.IsBot) continue;
                                        var slot = user.Slot;
                                        if (userInfo[slot].GangId == gang.DatabaseID)
                                        {
                                            var steamID = userInfo[slot].SteamID;
                                            userInfo[slot] = new UserInfo { SteamID = steamID };
                                        };
                                    }
                                    GangList.Remove(gang);
                                    AddScoreboardTagToPlayer(player);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError("(OpenAdminMenu) Failed disband in database! | " + ex.Message);
                            throw new Exception("Failed disband in database! | " + ex.Message);
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

                    var countmembers = await connection.QueryAsync($@"
                        SELECT COUNT(*) as Count
                        FROM `{Config.Database.TablePlayers}`
                        WHERE `gang_id` = @gangid",
                        new { gangid = gang.DatabaseID });

                    var data = (IDictionary<string, object>)countmembers.First();
                    var count = data["Count"];

                    var owner = await connection.QueryAsync($@"
                        SELECT `name`
                        FROM `{Config.Database.TablePlayers}`
                        WHERE `gang_id` = @gangid AND `gang_hierarchy` = 0",
                        new { gangid = gang.DatabaseID });

                    data = (IDictionary<string, object>)owner.First();
                    var name = data["name"];

                    Server.NextFrame(() => {
                        var statmenu = new ChatMenu(Localizer["menu<statistic_title>"]);

                        int level = GetGangLevel(gang);
                        int needexp = level * Config.Settings.ExpInc + Config.Settings.ExpInc;

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
}
