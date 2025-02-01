using System.Data;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Gangs;

public class MenuHTML
{
    private static Plugin Instance = Plugin.Instance;

    static public void Open(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        if (Instance._api!.OnlyTerroristCheck(player))
        {
            Instance.PrintToChat(player, Instance.Localizer["chat<only_terrorists>"]);
            return;
        }

        var slot = player.Slot;

        var menu = new CenterHtmlMenu(Instance.Localizer["menu<title>"], Instance);

        if (Instance.userInfo[slot].DatabaseID != -1)
        {
            var gang = Instance.GangList.Find(x => x.DatabaseID == Instance.userInfo[slot].GangId);

            if (gang != null)
            {
                var days = Helper.ConvertUnixToDateTime(gang.EndDate).Subtract(DateTime.Now).Days;

                if (Instance.Config.ExtendCost.Value.Count > 0)
                    menu.Title = Instance.Localizer["menu<title_with_days>", gang.Name, days];
                else
                    menu.Title = Instance.Localizer["menu<title_with_name>", gang.Name];

                menu.AddMenuOption(Instance.Localizer["menu<statistic>"], (player, option) => {
                    OpenStatisticMenu(player, gang);
                });

                menu.AddMenuOption(Instance.Localizer["menu<skills>"], (player, option) =>
                {
                    var skillsMenu = new CenterHtmlMenu(Instance.Localizer["menu<skills>"], Instance);

                    foreach (var skill in gang.SkillList)
                    {
                        if (Instance.StoreApi != null)
                        {
                            skillsMenu.AddMenuOption(Instance.Localizer["menu<skill_info>", Instance.Localizer[skill.Name], skill.Level, skill.MaxLevel, skill.Price], (player, option) =>
                            {
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        if (Instance.StoreApi.GetPlayerCredits(player) >= skill.Price)
                                        {
                                            if (skill.Level < skill.MaxLevel)
                                            {
                                                await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                                                {
                                                    await connection.OpenAsync();
                                                    skill.Level += 1;
                                                    await connection.ExecuteAsync($"UPDATE `{Instance.Config.Database.TablePerks}` SET `{skill.Name}` = {skill.Level} WHERE `gang_id` = {gang.DatabaseID};");

                                                    Server.NextFrame(() => {
                                                        Instance.StoreApi.GivePlayerCredits(player, -skill.Price);
                                                        Instance.PrintToChat(player, Instance.Localizer["chat<skill_success_buy>", Instance.Localizer[skill.Name]]);
                                                    });
                                                }
                                            }
                                            else
                                            {
                                                Server.NextFrame(() => {
                                                    Instance.PrintToChat(player, Instance.Localizer["chat<skill_max_lvl>"]);
                                                });
                                            }
                                        }
                                        else
                                        {
                                            Server.NextFrame(() => {
                                                Instance.PrintToChat(player, Instance.Localizer["chat<not_enough_credits>"]);
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Instance.LogError("(CommandGang) Fail skill gang! | " + ex.Message);
                                        throw new Exception("Fail skill gang! | " + ex.Message);
                                    }
                                });
                            }, Instance.StoreApi.GetPlayerCredits(player) < skill.Price || skill.Level >= skill.MaxLevel);
                        }
                    }

                    MenuManager.OpenCenterHtmlMenu(Instance, player, skillsMenu);
                }, Instance.NeedExtendGang(gang));

                menu.AddMenuOption(Instance.Localizer["menu<leader_panel>"], (player, option) => {
                    OpenAdminMenu(player);
                }, Instance.userInfo[slot].Rank == 0 ? false : true);

                menu.AddMenuOption(Instance.Localizer["menu<leave>"], (player, option) =>
                {
                    var acceptMenu = new CenterHtmlMenu(Instance.Localizer["menu<leave_accept>"], Instance);
                    acceptMenu.AddMenuOption(Instance.Localizer["Yes"], (player, option) =>
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                                {
                                    await connection.OpenAsync();

                                    await connection.ExecuteAsync($@"
                                        DELETE FROM `{Instance.Config.Database.TablePlayers}`
                                        WHERE `id` = @gId;",
                                        new { gId = Instance.userInfo[player.Slot].DatabaseID });

                                    var steamID = Instance.userInfo[slot].SteamID;
                                    Instance.userInfo[slot] = new UserInfo { SteamID = steamID };

                                    Server.NextFrame(() => {
                                        Instance.PrintToChat(player, Instance.Localizer["chat<leave_success>"]);
                                        Instance.AddScoreboardTagToPlayer(player);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Instance.LogError("(CommandGang) Failed to leave gang! | " + ex.Message);
                                throw new Exception("Failed to leave gang! | " + ex.Message);
                            }
                        });
                    });
                    acceptMenu.AddMenuOption(Instance.Localizer["No"], (invited, option) => {
                        MenuManager.OpenCenterHtmlMenu(Instance, player, menu);
                    });

                    MenuManager.OpenCenterHtmlMenu(Instance, player, acceptMenu);
                }, Instance.userInfo[slot].Rank > 0 ? false : true);

                menu.AddMenuOption(Instance.Localizer["menu<top>"], (player, option) =>
                {
                    var topGangsMenu = new CenterHtmlMenu(Instance.Localizer["menu<top>"], Instance);
                    var Gangs = from gang in Instance.GangList orderby gang.Exp descending select gang;

                    foreach (var gang in Gangs)
                    {
                        topGangsMenu.AddMenuOption(Instance.Localizer["menu<top_info>", gang.Name, Instance.GetGangLevel(gang)], (player, option) =>
                        {
                            OpenStatisticMenu(player, gang);
                        });
                    }

                    topGangsMenu.ExitButton = true;

                    MenuManager.OpenCenterHtmlMenu(Instance, player, topGangsMenu);
                });
            }
        }

        else
        {
            if (Instance.Config.CreateCost.Value > 0)
            {
                if (Instance.StoreApi != null)
                {
                    menu.AddMenuOption(Instance.Localizer["menu<create_with_credits>", Instance.Config.CreateCost.Value], (player, option) =>
                    {
                        Instance.userInfo[slot].Status = 1;
                        Instance.PrintToChat(player, Instance.Localizer["chat<create_name>"]);
                    }, Instance.StoreApi.GetPlayerCredits(player) < Instance.Config.CreateCost.Value);
                }
            }
            else
            {
                menu.AddMenuOption(Instance.Localizer["menu<create>"], (player, option) =>
                {
                    Instance.userInfo[slot].Status = 1;
                    Instance.PrintToChat(player, Instance.Localizer["chat<create_name>"]);
                });
            }

            menu.AddMenuOption(Instance.Localizer["menu<top>"], (player, option) =>
            {
                var topGangsMenu = new CenterHtmlMenu(Instance.Localizer["menu<top>"], Instance);
                var Gangs = from gang in Instance.GangList orderby gang.Exp descending select gang;

                foreach (var gang in Gangs)
                {
                    topGangsMenu.AddMenuOption(Instance.Localizer["menu<top_info>", gang.Name, Instance.GetGangLevel(gang)], (player, option) =>
                    {
                        OpenStatisticMenu(player, gang);
                    });
                }

                topGangsMenu.ExitButton = true;

                MenuManager.OpenCenterHtmlMenu(Instance, player, topGangsMenu);
            });
        }

        if (menu.MenuOptions.Count == 0)
            menu.AddMenuOption(Instance.Localizer["Oops"], (player, option) => { }, true);

        MenuManager.OpenCenterHtmlMenu(Instance, player, menu);
    }
    static public void OpenAdminMenu(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot) return;

        var slot = player.Slot;

        var gang = Instance.GangList.Find(x => x.DatabaseID == Instance.userInfo[slot].GangId);

        if (gang == null)
            return;

        var menu = new CenterHtmlMenu(Instance.Localizer["menu<title_with_name>", gang.Name], Instance);

        var sizeSkill = gang.SkillList.Find(x => x.Name.Equals("size"));

        menu.AddMenuOption(Instance.Localizer["menu<invite>"], (player, option) =>
        {
            var usersMenu = new CenterHtmlMenu(Instance.Localizer["menu<players>"], Instance);

            foreach (var user in Utilities.GetPlayers())
            {
                if (user == null || !user.IsValid || user.IsBot)
                    continue;

                if (Instance.userInfo[user.Slot].DatabaseID == -1)
                {
                    usersMenu.AddMenuOption($"{user.PlayerName}", (inviter, option) =>
                    {
                        Instance.PrintToChat(inviter, Instance.Localizer["chat<invite_sent>", user.PlayerName]);
                        var acceptMenu = new CenterHtmlMenu(Instance.Localizer["menu<invite_came>", gang.Name], Instance);

                        acceptMenu.AddMenuOption(Instance.Localizer["Accept"], (invited, option) =>
                        {
                            if (invited.AuthorizedSteamID != null)
                            {
                                var l_steamId = invited.AuthorizedSteamID.SteamId64;
                                var l_playerName = invited.PlayerName;
                                var l_inviterName = inviter.PlayerName;
                                var l_inviteDate = Helper.GetNowUnixTime();
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                                        {
                                            await connection.OpenAsync();
                                            var sql = $"INSERT INTO `{Instance.Config.Database.TablePlayers}` (`gang_id`, `steam_id`, `name`, `gang_hierarchy`, `inviter_name`, `invite_date`) VALUES ({gang.DatabaseID}, '{l_steamId}', '{l_playerName}', 3, '{l_inviterName}', {l_inviteDate});";
                                            await connection.ExecuteAsync(sql);
                                            var command = connection.CreateCommand();
                                            sql = $"SELECT `id` FROM `{Instance.Config.Database.TablePlayers}` WHERE `steam_id` = '{l_steamId}'";
                                            command.CommandText = sql;
                                            var reader = await command.ExecuteReaderAsync();

                                            if (await reader.ReadAsync())
                                            {
                                                Instance.userInfo[user.Slot] = new UserInfo
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
                                                gang.MembersList.Add(Instance.userInfo[user.Slot]);
                                                Server.NextFrame(() => {
                                                    Instance.PrintToChat(invited, Instance.Localizer["chat<invite_welcome>", gang.Name]);
                                                    Instance.PrintToChat(inviter, Instance.Localizer["chat<invite_accept>", invited.PlayerName]);
                                                    Instance.AddScoreboardTagToPlayer(invited);
                                                    MenuManager.CloseActiveMenu(invited);
                                                });
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Instance.LogError("(OpenAdminMenu) Failed invite in database | " + ex.Message);
                                        throw new Exception("Failed invite in database! | " + ex.Message);
                                    }
                                });
                            }
                        });
                        acceptMenu.ExitButton = true;
                        MenuManager.OpenCenterHtmlMenu(Instance, user, acceptMenu);
                    });
                }
            }
            if (usersMenu.MenuOptions.Count > 0)
                MenuManager.OpenCenterHtmlMenu(Instance, player, usersMenu);
            else
                Instance.PrintToChat(player, Instance.Localizer["chat<no_players>"]);

        }, Instance.NeedExtendGang(gang) || (sizeSkill != null && gang.MembersList.Count >= (Instance.Config.Settings.MaxMembers + sizeSkill.Level)) || gang.MembersList.Count >= Instance.Config.Settings.MaxMembers);
        if (Instance.Config.ExtendCost.Value.Count > 0 && Instance.StoreApi != null)
        {
            menu.AddMenuOption(Instance.Localizer["menu<extend>"], (player, option) =>
            {
                var pricesMenu = new CenterHtmlMenu(Instance.Localizer["menu<extend_date>"], Instance);

                foreach (var price in Instance.Config.ExtendCost.Value)
                {
                    pricesMenu.AddMenuOption(Instance.Localizer["menu<extend_select_date>", price.Day, price.Value], (player, option) =>
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                if (Instance.StoreApi.GetPlayerCredits(player) >= price.Value)
                                {
                                    await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                                    {
                                        await connection.OpenAsync();

                                        var gang = Instance.GangList.Find(x => x.DatabaseID == Instance.userInfo[player.Slot].GangId);
                                        if (gang != null)
                                        {
                                            var addDay = Helper.ConvertUnixToDateTime(gang.EndDate).AddDays(Convert.ToInt32(price.Day));
                                            var newDate = (int)(addDay.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                                            await connection.ExecuteAsync($"UPDATE `{Instance.Config.Database.TableGroups}` SET `end_date` = {newDate} WHERE `id` = {gang.DatabaseID}");
                                            gang.EndDate = newDate;

                                            Server.NextFrame(() =>
                                            {
                                                Instance.StoreApi.GivePlayerCredits(player, -price.Value);
                                                Instance.PrintToChat(player, Instance.Localizer["chat<extend_success>"]);
                                            });
                                        }
                                    }
                                }
                                else
                                {
                                    Server.NextFrame(() => {
                                        Instance.PrintToChat(player, Instance.Localizer["chat<not_enough_credits>"]);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Instance.LogError("OpenAdminMenu) Failed extend gang! | " + ex.Message);
                                throw new Exception("Failed extend gang! | " + ex.Message);
                            }
                        });
                    }, Instance.StoreApi.GetPlayerCredits(player) < price.Value);
                }

                pricesMenu.ExitButton = true;
                MenuManager.OpenCenterHtmlMenu(Instance, player, pricesMenu);
            });
        }

        if (Instance.Config.RenameCost > 0)
        {
            if (Instance.StoreApi != null)
            {
                menu.AddMenuOption(Instance.Localizer["menu<rename_with_credits>", Instance.Config.RenameCost], (player, option) =>
                {
                    Instance.userInfo[slot].Status = 2;
                    Instance.PrintToChat(player, Instance.Localizer["chat<rename>"]);
                }, Instance.NeedExtendGang(gang) || Instance.StoreApi.GetPlayerCredits(player) < Instance.Config.RenameCost);
            }
        }
        else
        {
            menu.AddMenuOption(Instance.Localizer["menu<rename>"], (player, option) =>
            {
                Instance.userInfo[slot].Status = 2;
                Instance.PrintToChat(player, Instance.Localizer["chat<rename>"]);
            }, Instance.NeedExtendGang(gang));
        }

        menu.AddMenuOption(Instance.Localizer["menu<kick>"], (player, option) =>
        {
            var usersMenu = new CenterHtmlMenu(Instance.Localizer["menu<players>"], Instance);

            Dictionary<int, string> users = new Dictionary<int, string>();

            Task.Run(async () =>
            {
                try
                {
                    await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                    {
                        await connection.OpenAsync();
                        var command = connection.CreateCommand();
                        string sql = $"SELECT `id`, `name` FROM `{Instance.Config.Database.TablePlayers}` WHERE `id` <> {Instance.userInfo[player.Slot].DatabaseID} AND `gang_id` = {Instance.userInfo[player.Slot].GangId};";
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
                                var acceptMenu = new CenterHtmlMenu(Instance.Localizer["menu<kick_sure>", user.Value], Instance);
                                acceptMenu.AddMenuOption(Instance.Localizer["Yes"], (player, option) =>
                                {
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                                            {
                                                await connection.OpenAsync();

                                                await connection.ExecuteAsync($@"DELETE FROM `{Instance.Config.Database.TablePlayers}` WHERE `id` = @gId;",
                                                    new { gId = user.Key });

                                                Server.NextFrame(() => {
                                                    foreach (var player in Utilities.GetPlayers())
                                                    {
                                                        if (Instance.userInfo[player.Slot].DatabaseID == user.Key)
                                                        {
                                                            var steamID = Instance.userInfo[player.Slot].SteamID;
                                                            Instance.userInfo[player.Slot] = new UserInfo { SteamID = steamID };
                                                            Instance.PrintToChat(player, Instance.Localizer["chat<kick_message>"]);
                                                            Instance.AddScoreboardTagToPlayer(player);
                                                            break;
                                                        }
                                                    }
                                                    Instance.PrintToChat(player, Instance.Localizer["chat<kick_complete>", user.Value]);
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Instance.LogError("(CommandGang) Fail kick from gang! | " + ex.Message);
                                            throw new Exception("Fail kick from gang! | " + ex.Message);
                                        }
                                    });
                                });
                                acceptMenu.AddMenuOption(Instance.Localizer["No"], (invited, option) => {
                                    MenuManager.OpenCenterHtmlMenu(Instance, player, menu);
                                });
                                MenuManager.OpenCenterHtmlMenu(Instance, player, acceptMenu);
                            });
                        }
                        Server.NextFrame(() => {
                            if (usersMenu.MenuOptions.Count > 0) MenuManager.OpenCenterHtmlMenu(Instance, player, usersMenu);
                            else Instance.PrintToChat(player, Instance.Localizer["chat<no_players>"]);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Instance.LogError("(OpenAdminMenu) Failed check players to kick from gang in database | " + ex.Message);
                    throw new Exception("Failed check players to kick from gang in database! | " + ex.Message);
                }
            });
        }, Instance.NeedExtendGang(gang));

        menu.AddMenuOption(Instance.Localizer["menu<leader>"], (player, option) =>
        {
            var usersMenu = new CenterHtmlMenu(Instance.Localizer["menu<players>"], Instance);

            Dictionary<int, string> users = new Dictionary<int, string>();

            Task.Run(async () =>
            {
                try
                {
                    await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                    {
                        await connection.OpenAsync();
                        var command = connection.CreateCommand();
                        string sql = $"SELECT `id`, `name` FROM `{Instance.Config.Database.TablePlayers}` WHERE `id` <> {Instance.userInfo[player.Slot].DatabaseID} AND `gang_id` = {Instance.userInfo[player.Slot].GangId};";
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
                                var acceptMenu = new CenterHtmlMenu(Instance.Localizer["menu<leader_sure>", user.Value], Instance);
                                acceptMenu.AddMenuOption(Instance.Localizer["Yes"], (player, option) =>
                                {
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                                            {
                                                await connection.OpenAsync();

                                                await connection.ExecuteAsync($"UPDATE `{Instance.Config.Database.TablePlayers}` SET `gang_hierarchy` = 0 WHERE `id` = {user.Key}");
                                                await connection.ExecuteAsync($"UPDATE `{Instance.Config.Database.TablePlayers}` SET `gang_hierarchy` = 1 WHERE `id` = {Instance.userInfo[player.Slot].DatabaseID}");

                                                Instance.userInfo[player.Slot].Rank = 1;
                                                Server.NextFrame(() => {
                                                    foreach (var player in Utilities.GetPlayers())
                                                    {
                                                        if (Instance.userInfo[player.Slot].DatabaseID == user.Key)
                                                        {
                                                            Instance.userInfo[player.Slot].Rank = 0;
                                                            Instance.PrintToChat(player, Instance.Localizer["chat<leader_new>"]);
                                                            break;
                                                        }
                                                    }
                                                    Instance.PrintToChat(player, Instance.Localizer["chat<leader_complete>", user.Value]);
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Instance.LogError("(CommandGang) Fail transfer leader! | " + ex.Message);
                                            throw new Exception("Fail transfer leader! | " + ex.Message);
                                        }
                                    });
                                });
                                acceptMenu.AddMenuOption(Instance.Localizer["No"], (invited, option) => {
                                    MenuManager.OpenCenterHtmlMenu(Instance, player, menu);
                                });
                                MenuManager.OpenCenterHtmlMenu(Instance, player, acceptMenu);
                            });
                        }
                        Server.NextFrame(() => {
                            if (usersMenu.MenuOptions.Count > 0) MenuManager.OpenCenterHtmlMenu(Instance, player, usersMenu);
                            else Instance.PrintToChat(player, Instance.Localizer["chat<no_players>"]);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Instance.LogError("(OpenAdminMenu) Failed check players to transfer leader in database | " + ex.Message);
                    throw new Exception("Failed check players to transfer leader in database! | " + ex.Message);
                }
            });
        }, Instance.NeedExtendGang(gang));
        if (Instance.userInfo[slot].Rank == 0)
        {
            menu.AddMenuOption(Instance.Localizer["menu<disband>"], (player, option) =>
            {
                var confirmMenu = new CenterHtmlMenu(Instance.Localizer["Sure"], Instance);

                confirmMenu.AddMenuOption(Instance.Localizer["Yes"], (player, option) =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                            {
                                await connection.OpenAsync();

                                await connection.ExecuteAsync($@"DELETE FROM `{Instance.Config.Database.TablePlayers}` WHERE `gang_id` = @gId;", new { gId = gang.DatabaseID });

                                await connection.ExecuteAsync($@"DELETE FROM `{Instance.Config.Database.TablePerks}` WHERE `gang_id` = @gId;", new { gId = gang.DatabaseID });

                                await connection.ExecuteAsync($@"DELETE FROM `{Instance.Config.Database.TableGroups}` WHERE `id` = @gId;", new { gId = gang.DatabaseID });

                                Server.NextFrame(() => {
                                    Instance.PrintToChatAll(Instance.Localizer["chat<disband_announce>", player.PlayerName, gang.Name]);
                                    foreach (var user in Utilities.GetPlayers())
                                    {
                                        if (user == null || !user.IsValid || user.IsBot) continue;
                                        var slot = user.Slot;
                                        if (Instance.userInfo[slot].GangId == gang.DatabaseID)
                                        {
                                            var steamID = Instance.userInfo[slot].SteamID;
                                            Instance.userInfo[slot] = new UserInfo { SteamID = steamID };
                                        };
                                    }
                                    Instance.GangList.Remove(gang);
                                    Instance.AddScoreboardTagToPlayer(player, true);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Instance.LogError("(OpenAdminMenu) Failed disband in database! | " + ex.Message);
                            throw new Exception("Failed disband in database! | " + ex.Message);
                        }
                    });
                });

                confirmMenu.AddMenuOption(Instance.Localizer["Cancel"], (player, option) => {
                    MenuManager.OpenCenterHtmlMenu(Instance, player, menu);
                });

                MenuManager.OpenCenterHtmlMenu(Instance, player, confirmMenu);
            });
        }
        menu.ExitButton = true;
        MenuManager.OpenCenterHtmlMenu(Instance, player, menu);
    }

    static public void OpenStatisticMenu(CCSPlayerController? player, Gang gang)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        Task.Run(async () =>
        {
            try
            {
                await using (var connection = new MySqlConnection(Instance.dbConnectionString))
                {
                    await connection.OpenAsync();

                    var countmembers = await connection.QueryAsync($@"
                        SELECT COUNT(*) as Count
                        FROM `{Instance.Config.Database.TablePlayers}`
                        WHERE `gang_id` = @gangid",
                        new { gangid = gang.DatabaseID });

                    var data = (IDictionary<string, object>)countmembers.First();
                    var count = data["Count"];

                    var owner = await connection.QueryAsync($@"
                        SELECT `name`
                        FROM `{Instance.Config.Database.TablePlayers}`
                        WHERE `gang_id` = @gangid AND `gang_hierarchy` = 0",
                        new { gangid = gang.DatabaseID });

                    data = (IDictionary<string, object>)owner.First();
                    var name = data["name"];

                    Server.NextFrame(() => {
                        var statmenu = new CenterHtmlMenu(Instance.Localizer["menu<statistic_title>"], Instance);

                        int level = Instance.GetGangLevel(gang);
                        int needexp = level * Instance.Config.Settings.ExpInc + Instance.Config.Settings.ExpInc;

                        statmenu.AddMenuOption(Instance.Localizer["menu<statistic_name>", gang.Name], (player, option) => { }, true);

                        statmenu.AddMenuOption(Instance.Localizer["menu<statistic_leader>", name], (player, option) => { }, true);

                        statmenu.AddMenuOption(Instance.Localizer["menu<statistic_num_players>", count], (player, option) => { }, true);

                        statmenu.AddMenuOption(Instance.Localizer["menu<statistic_lvl>", level, gang.Exp, needexp], (player, option) => { }, true);

                        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                        dateTime = dateTime.AddSeconds(gang.CreateDate).ToLocalTime();
                        var date = dateTime.ToString("dd.MM.yyyy") + " " + dateTime.ToString("HH:mm");

                        statmenu.AddMenuOption(Instance.Localizer["menu<statistic_create_date>", date], (player, option) => { }, true);

                        MenuManager.OpenCenterHtmlMenu(Instance, player, statmenu);
                    });
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError(ex.Message);
            }
        });
    }
}
