using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using MySqlConnector;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Enum;

namespace Gangs;

public static partial class Menu
{
    private static Plugin Instance = Plugin.Instance;
    private static string MenuType = Instance.Config.Settings.MenuType;

    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public static void Command_OpenMenus(CCSPlayerController player, CommandInfo info)
    {
        if (Instance._api!.OnlyTerroristCheck(player))
        {
            Instance.PrintToChat(player, Instance.Localizer["chat<only_terrorists>"]);
            return;
        }

        Open(player);
    }

    static public void Open(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        var slot = player.Slot;

        var menu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<title>"], Instance);

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

                menu.AddItem(Instance.Localizer["menu<statistic>"], (player, option) => {
                    OpenStatisticMenu(player, gang);
                });

                menu.AddItem(Instance.Localizer["menu<skills>"], (player, option) =>
                {
                    var skillsMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<skills>"], Instance);

                    foreach (var skill in gang.SkillList)
                    {
                        if (Instance.StoreApi != null)
                        {
                            skillsMenu.AddItem(Instance.Localizer["menu<skill_info>", Instance.Localizer[skill.Name], skill.Level, skill.MaxLevel, skill.Price], (player, option) =>
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
                            }, (Instance.StoreApi.GetPlayerCredits(player) < skill.Price || skill.Level >= skill.MaxLevel) ? DisableOption.DisableShowNumber : DisableOption.None);
                        }
                    }
                    skillsMenu.Display(player, 0);
                }, (Instance.NeedExtendGang(gang) ? DisableOption.DisableShowNumber : DisableOption.None));

                if (Instance.userInfo[slot].Rank == 0)
                {
                    menu.AddItem(Instance.Localizer["menu<leader_panel>"], (player, option) => {
                        OpenAdminMenu(player);
                    });
                }

                menu.AddItem(Instance.Localizer["menu<leave>"], (player, option) =>
                {
                    var acceptMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<leave_accept>"], Instance);
                    acceptMenu.AddItem(Instance.Localizer["Yes"], (player, option) =>
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
                    acceptMenu.AddItem(Instance.Localizer["No"], (invited, option) => {
                        menu.Display(player, 0);
                    });

                    acceptMenu.Display(player, 0);
                }, Instance.userInfo[slot].Rank > 0 ? DisableOption.None : DisableOption.DisableShowNumber);

                menu.AddItem(Instance.Localizer["menu<top>"], (player, option) =>
                {
                    var topGangsMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<top>"], Instance);
                    var Gangs = from gang in Instance.GangList orderby gang.Exp descending select gang;

                    foreach (var gang in Gangs)
                    {
                        topGangsMenu.AddItem(Instance.Localizer["menu<top_info>", gang.Name, Instance.GetGangLevel(gang)], (player, option) =>
                        {
                            OpenStatisticMenu(player, gang);
                        });
                    }

                    topGangsMenu.ExitButton = true;

                    topGangsMenu.Display(player, 0);
                });
            }
        }

        else
        {
            if (Instance.Config.CreateCost.Value > 0)
            {
                if (Instance.StoreApi != null)
                {
                    menu.AddItem(Instance.Localizer["menu<create_with_credits>", Instance.Config.CreateCost.Value], (player, option) =>
                    {
                        Instance.userInfo[slot].Status = 1;
                        Instance.PrintToChat(player, Instance.Localizer["chat<create_name>"]);
                    }, Instance.StoreApi.GetPlayerCredits(player) < Instance.Config.CreateCost.Value ? DisableOption.DisableShowNumber : DisableOption.None);
                }
            }
            else
            {
                menu.AddItem(Instance.Localizer["menu<create>"], (player, option) =>
                {
                    Instance.userInfo[slot].Status = 1;
                    Instance.PrintToChat(player, Instance.Localizer["chat<create_name>"]);
                });
            }

            menu.AddItem(Instance.Localizer["menu<top>"], (player, option) =>
            {
                var topGangsMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<top>"], Instance);
                var Gangs = from gang in Instance.GangList orderby gang.Exp descending select gang;

                foreach (var gang in Gangs)
                {
                    topGangsMenu.AddItem(Instance.Localizer["menu<top_info>", gang.Name, Instance.GetGangLevel(gang)], (player, option) =>
                    {
                        OpenStatisticMenu(player, gang);
                    });
                }

                topGangsMenu.ExitButton = true;

                topGangsMenu.Display(player, 0);
            });
        }

        //if (menu.ItemOptions.Count == 0)
        //    menu.AddItem(Instance.Localizer["Oops"], (player, option) => { }, true);

        menu.Display(player, 0);
    }
    static public void OpenAdminMenu(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot) return;

        var slot = player.Slot;

        var gang = Instance.GangList.Find(x => x.DatabaseID == Instance.userInfo[slot].GangId);

        if (gang == null)
            return;

        var menu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<title_with_name>", gang.Name], Instance);

        var sizeSkill = gang.SkillList.Find(x => x.Name.Equals("size"));

        menu.AddItem(Instance.Localizer["menu<invite>"], (player, option) =>
        {
            if ((sizeSkill != null && gang.MembersCount >= (Instance.Config.Settings.MaxMembers + sizeSkill.Level)) || (sizeSkill == null && gang.MembersCount >= Instance.Config.Settings.MaxMembers))
            {
                Instance.PrintToChat(player, Instance.Localizer["chat<invite_full>", gang.MembersCount, Instance.Config.Settings.MaxMembers]);
                return;
            }

            var usersMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<players>"], Instance);

            foreach (var user in Utilities.GetPlayers())
            {
                if (user == null || !user.IsValid || user.IsBot)
                    continue;

                if (Instance.userInfo[user.Slot].DatabaseID == -1)
                {
                    usersMenu.AddItem($"{user.PlayerName}", (inviter, option) =>
                    {
                        Instance.PrintToChat(inviter, Instance.Localizer["chat<invite_sent>", user.PlayerName]);
                        var acceptMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<invite_came>", gang.Name], Instance);

                        acceptMenu.AddItem(Instance.Localizer["Accept"], (invited, option) =>
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
                                                gang.MembersCount++;
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
                        acceptMenu.Display(user, 0);
                    });
                }
            }
            if (usersMenu.ItemOptions.Count > 0)
                usersMenu.Display(player, 0);

            else Instance.PrintToChat(player, Instance.Localizer["chat<no_players>"]);

        }, (Instance.NeedExtendGang(gang) ||
            (sizeSkill != null && gang.MembersCount >= (Instance.Config.Settings.MaxMembers + sizeSkill.Level)) ||
            (sizeSkill == null && gang.MembersCount >= Instance.Config.Settings.MaxMembers))
            ? DisableOption.DisableShowNumber
            : DisableOption.None);

        if (Instance.Config.ExtendCost.Value.Count > 0 && Instance.StoreApi != null)
        {
            menu.AddItem(Instance.Localizer["menu<extend>"], (player, option) =>
            {
                var pricesMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<extend_date>"], Instance);

                foreach (var price in Instance.Config.ExtendCost.Value)
                {
                    pricesMenu.AddItem(Instance.Localizer["menu<extend_select_date>", price.Day, price.Value], (player, option) =>
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
                    }, Instance.StoreApi.GetPlayerCredits(player) < price.Value ? DisableOption.DisableShowNumber : DisableOption.None);
                }

                pricesMenu.ExitButton = true;
                pricesMenu.Display(player, 0);
            });
        }

        if (Instance.Config.RenameCost > 0)
        {
            if (Instance.StoreApi != null)
            {
                menu.AddItem(Instance.Localizer["menu<rename_with_credits>", Instance.Config.RenameCost], (player, option) =>
                {
                    Instance.userInfo[slot].Status = 2;
                    Instance.PrintToChat(player, Instance.Localizer["chat<rename>"]);
                }, (Instance.NeedExtendGang(gang) || Instance.StoreApi.GetPlayerCredits(player) < Instance.Config.RenameCost) ? DisableOption.DisableShowNumber : DisableOption.None);
            }
        }
        else
        {
            menu.AddItem(Instance.Localizer["menu<rename>"], (player, option) =>
            {
                Instance.userInfo[slot].Status = 2;
                Instance.PrintToChat(player, Instance.Localizer["chat<rename>"]);
            }, Instance.NeedExtendGang(gang) ? DisableOption.DisableShowNumber : DisableOption.None);
        }

        menu.AddItem(Instance.Localizer["menu<kick>"], (player, option) =>
        {
            var usersMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<players>"], Instance);

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
                            usersMenu.AddItem($"{user.Value}", (player, option) =>
                            {
                                var acceptMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<kick_sure>", user.Value], Instance);
                                acceptMenu.AddItem(Instance.Localizer["Yes"], (player, option) =>
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
                                                    foreach (var target in Utilities.GetPlayers())
                                                    {
                                                        if (Instance.userInfo[target.Slot].DatabaseID == user.Key)
                                                        {
                                                            gang.MembersList.Remove(Instance.userInfo[target.Slot]);
                                                            gang.MembersCount--;
                                                            var steamID = Instance.userInfo[target.Slot].SteamID;
                                                            Instance.userInfo[target.Slot] = new UserInfo { SteamID = steamID };
                                                            Instance.PrintToChat(target, Instance.Localizer["chat<kick_message>"]);
                                                            Instance.AddScoreboardTagToPlayer(target);
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
                                acceptMenu.AddItem(Instance.Localizer["No"], (invited, option) => {
                                    menu.Display(player, 0);
                                });
                                acceptMenu.Display(player, 0);
                            });
                        }
                        Server.NextFrame(() => {
                            if (usersMenu.ItemOptions.Count > 0) usersMenu.Display(player, 0);
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
        }, Instance.NeedExtendGang(gang) ? DisableOption.DisableShowNumber : DisableOption.None);

        menu.AddItem(Instance.Localizer["menu<leader>"], (player, option) =>
        {
            var usersMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<players>"], Instance);

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
                            usersMenu.AddItem($"{user.Value}", (player, option) =>
                            {
                                var acceptMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<leader_sure>", user.Value], Instance);
                                acceptMenu.AddItem(Instance.Localizer["Yes"], (player, option) =>
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
                                acceptMenu.AddItem(Instance.Localizer["No"], (invited, option) => {
                                    menu.Display(player, 0);
                                });
                                acceptMenu.Display(player, 0);
                            });
                        }
                        Server.NextFrame(() => {
                            if (usersMenu.ItemOptions.Count > 0) usersMenu.Display(player, 0);
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
        }, Instance.NeedExtendGang(gang) ? DisableOption.DisableShowNumber : DisableOption.None);
        if (Instance.userInfo[slot].Rank == 0)
        {
            menu.AddItem(Instance.Localizer["menu<disband>"], (player, option) =>
            {
                var confirmMenu = MenuManager.MenuByType(MenuType, Instance.Localizer["Sure"], Instance);

                confirmMenu.AddItem(Instance.Localizer["Yes"], (player, option) =>
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

                confirmMenu.AddItem(Instance.Localizer["Cancel"], (player, option) => {
                    menu.Display(player, 0);
                });

                confirmMenu.Display(player, 0);
            });
        }
        menu.ExitButton = true;
        menu.Display(player, 0);
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
                        var statmenu = MenuManager.MenuByType(MenuType, Instance.Localizer["menu<statistic_title>"], Instance);

                        int level = Instance.GetGangLevel(gang);
                        int needexp = level * Instance.Config.Settings.ExpInc + Instance.Config.Settings.ExpInc;

                        statmenu.AddItem(Instance.Localizer["menu<statistic_name>", gang.Name], (player, option) => { }, DisableOption.DisableHideNumber);

                        statmenu.AddItem(Instance.Localizer["menu<statistic_leader>", name], (player, option) => { }, DisableOption.DisableHideNumber);

                        statmenu.AddItem(Instance.Localizer["menu<statistic_num_players>", count], (player, option) => { }, DisableOption.DisableHideNumber);

                        statmenu.AddItem(Instance.Localizer["menu<statistic_lvl>", level, gang.Exp, needexp], (player, option) => { }, DisableOption.DisableHideNumber);

                        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                        dateTime = dateTime.AddSeconds(gang.CreateDate).ToLocalTime();
                        var date = dateTime.ToString("dd.MM.yyyy") + " " + dateTime.ToString("HH:mm");

                        statmenu.AddItem(Instance.Localizer["menu<statistic_create_date>", date], (player, option) => { }, DisableOption.DisableHideNumber);

                        statmenu.Display(player, 0);
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