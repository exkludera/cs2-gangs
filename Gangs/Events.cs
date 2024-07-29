using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Data;
using static Dapper.SqlMapper;

namespace Gangs;

public partial class Gangs
{
    private void UnregisterEvents()
    {
        RemoveCommandListener("say", OnCommandSay, HookMode.Pre);
        RemoveCommandListener("say_team", OnCommandSay, HookMode.Pre);

        RemoveListener<Listeners.OnMapStart>(OnMapStart);
        RemoveListener<Listeners.OnMapEnd>(OnMapEnd);
        RemoveListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
    }

    private void RegisterEvents()
    {
        AddCommandListener("say", OnCommandSay, HookMode.Pre);
        AddCommandListener("say_team", OnCommandSay, HookMode.Pre);

        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);

        RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
        {
            AddScoreboardTagToPlayer(@event.Userid!);

            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var player = @event.Attacker;

            if (player == null || !player.IsValid || player.IsBot)
                return HookResult.Continue;

            AddScoreboardTagToPlayer(@event.Userid!);

            var slot = player.Slot;

            var gang = GangList.Find(x => x.DatabaseID == userInfo[slot].GangId);

            if (gang == null)
                return HookResult.Continue;

            if (NeedExtendGang(gang))
                return HookResult.Continue;

            gang.Exp += 1;

            return HookResult.Continue;
        });
    }

    public void OnClientAuthorized(int playerSlot, SteamID steamID)
    {
        CCSPlayerController? player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player == null || !player.IsValid || player.IsBot)
            return;

        if (player.AuthorizedSteamID == null)
        {
            AddTimer(3.0f, () =>
            {
                OnClientAuthorized(playerSlot, steamID);
            });
            return;
        }

        string nickname = player.PlayerName;
        string steamid = steamID.SteamId2;

        Task.Run(async () =>
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    var command = connection.CreateCommand();
                    string sql = $"SELECT player_table.* FROM `{Config.Database.TablePlayers}` AS `player_table` INNER JOIN `{Config.Database.TableGroups}` AS `gang_table` ON player_table.gang_id = gang_table.id WHERE player_table.steam_id = '{steamid}';";
                    command.CommandText = sql;
                    var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        userInfo[playerSlot] = new UserInfo
                        {
                            SteamID = reader.GetString(2),
                            Status = 0,
                            DatabaseID = reader.GetInt32(0),
                            GangId = reader.GetInt32(1),
                            Rank = reader.GetInt32(4),
                            InviterName = reader.GetString(5),
                            InviteDate = reader.GetInt32(6)
                        };
                        if (!String.Equals(reader.GetString(3), nickname))
                        {
                            await reader.CloseAsync();
                            sql = $"UPDATE `{Config.Database.TablePlayers}` SET `name` = '{nickname}' WHERE `id` = '{userInfo[playerSlot].DatabaseID}'";
                            command.CommandText = sql;
                            await command.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            await reader.CloseAsync();
                        }
                    }
                    else
                    {
                        await reader.CloseAsync();
                        userInfo[playerSlot] = new UserInfo { SteamID = steamid };
                    }
                };
            }
            catch (Exception ex)
            {
                LogError("(OnClientAuthorized) Failed get info in database | " + ex.Message);
                throw new Exception("Failed get info in database! | " + ex.Message);
            }
        });
    }

    [GameEventHandler]
    public HookResult OnClientDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player is null
            || string.IsNullOrEmpty(player.IpAddress)
            || player.IpAddress.Contains("127.0.0.1")
            || player.IsBot
            || player.IsHLTV
            || !player.UserId.HasValue
        )
            return HookResult.Continue;

        Array.Clear(userInfo, player.Slot, 1);

        return HookResult.Continue;
    }

    public HookResult OnCommandSay(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var slot = player.Slot;

        if (userInfo[slot].Status == 1 || userInfo[slot].Status == 2)
        {
            var message = info.ArgString;
            message = message.Trim('"');

            if (String.Equals(info.ArgString, "cancel"))
            {
                userInfo[slot].Status = 0;
                return HookResult.Handled;
            }

            if (message.Length > 16)
            {
                player.PrintToChat(Localizer["chat<rename_long>"]);
                return HookResult.Handled;
            }

            else if (message.Length < 3)
            {
                player.PrintToChat(Localizer["chat<rename_short>"]);
                return HookResult.Handled;
            }

            else if (message.Length == 0)
                return HookResult.Handled;

            var playerName = player.PlayerName;
            var createDate = Helper.GetNowUnixTime();
            var addDay = Helper.ConvertUnixToDateTime(createDate).AddDays(Convert.ToInt32(Config.CreateCost.Days));
            var endDate = (int)(addDay.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            Task.Run(async () =>
            {
                try
                {
                    await using (var connection = new MySqlConnection(dbConnectionString))
                    {
                        await connection.OpenAsync();

                        var command = connection.CreateCommand();
                        string sql = $"SELECT `name` FROM `{Config.Database.TableGroups}` WHERE `name` = '{message}'";
                        command.CommandText = sql;
                        var reader = await command.ExecuteReaderAsync();
                        if (await reader.ReadAsync() == false)
                        {
                            reader.Close();
                            if (userInfo[slot].Status == 1)
                            {
                                sql = $"INSERT INTO `{Config.Database.TableGroups}` (`name`, `create_date`, `end_date`) VALUES ('{message}', {createDate}, {endDate});";
                                await connection.ExecuteAsync(sql);

                                sql = $"SELECT `id` FROM `{Config.Database.TableGroups}` WHERE `name` = '{message}'";
                                command.CommandText = sql;
                                var reader2 = await command.ExecuteReaderAsync();

                                if (await reader2.ReadAsync())
                                {
                                    var gangId = reader2.GetInt32(0);
                                    reader2.Close();
                                    GangList.Add(new Gang(
                                        message,
                                        createDate,
                                        endDate,
                                        new(),
                                        new(),
                                        0,
                                        gangId));

                                    sql = $"INSERT INTO `{Config.Database.TablePerks}` (`gang_id`) VALUES ({gangId});";
                                    await connection.ExecuteAsync(sql);

                                    sql = $"INSERT INTO `{Config.Database.TablePlayers}` (`gang_id`, `steam_id`, `name`, `gang_hierarchy`, `inviter_name`, `invite_date`) VALUES ({gangId}, '{userInfo[slot].SteamID}', '{playerName}', 0, '{playerName}', {createDate});";
                                    await connection.ExecuteAsync(sql);

                                    sql = $"SELECT `id` FROM `{Config.Database.TablePlayers}` WHERE `gang_id` = {gangId} AND `steam_id` = '{userInfo[slot].SteamID}'";
                                    command.CommandText = sql;
                                    var reader3 = await command.ExecuteReaderAsync();

                                    if (await reader3.ReadAsync())
                                    {
                                        var steamID = userInfo[slot].SteamID;
                                        userInfo[slot] = new UserInfo
                                        {
                                            SteamID = steamID,
                                            Status = 0,
                                            DatabaseID = reader3.GetInt32(0),
                                            GangId = gangId,
                                            Rank = 0,
                                            InviterName = playerName,
                                            InviteDate = createDate
                                        };
                                        var gang = GangList.Find(x => x.DatabaseID == userInfo[slot].GangId);
                                        if (gang != null) gang.MembersList.Add(userInfo[slot]);
                                        if (Config.CreateCost.Value > 0)
                                        {
                                            if (StoreApi != null) StoreApi.GivePlayerCredits(player, -Config.CreateCost.Value);
                                        }
                                        Server.NextFrame(() =>
                                        {
                                            if (gang != null) _api!.OnGangsCreated(player, gang.DatabaseID);
                                            PrintToChatAll(Localizer["chat<create_success>", playerName, message]);
                                        });
                                    }
                                    reader3.Close();
                                }
                            }
                            else if (userInfo[slot].Status == 2)
                            {
                                sql = $"UPDATE `{Config.Database.TableGroups}` SET `name` = '{message}' WHERE `id` = {userInfo[player.Slot].GangId}";
                                await connection.ExecuteAsync(sql);

                                var gang = GangList.Find(x => x.DatabaseID == userInfo[player.Slot].GangId);

                                if (gang != null) gang.Name = message;
                                userInfo[player.Slot].Status = 0;

                                if (Config.RenameCost > 0)
                                {
                                    if (StoreApi != null) StoreApi.GivePlayerCredits(player, -Config.RenameCost);
                                }
                                Server.NextFrame(() =>
                                {
                                    PrintToChatAll(Localizer["chat<rename_success>", playerName, message]);
                                });
                            }
                        }
                        else
                        {
                            Server.NextFrame(() => PrintToChat(player, Localizer["chat<rename_exist>"]));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError("(OnCommandSay) Failed create in database | " + ex.Message);
                    throw new Exception("Failed create in database! | " + ex.Message);
                }
            });
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    private void OnMapStart(string mapName)
    {
        Task.Run(async () =>
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    var command = connection.CreateCommand();

                    string sql = $"SELECT `name`, `create_date`, `end_date`, `exp`, `id` FROM `{Config.Database.TableGroups}`;";
                    command.CommandText = sql;
                    var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        if (!GangList.Any(g => g.DatabaseID == reader.GetInt32(4)))
                        {
                            GangList.Add(new Gang(
                                reader.GetString(0),
                                reader.GetInt32(1),
                                reader.GetInt32(2),
                                new(),
                                new(),
                                reader.GetInt32(3),
                                reader.GetInt32(4)
                            ));
                        }
                    }
                    reader.Close();

                    sql = $"SELECT * FROM `{Config.Database.TablePerks}`;";
                    command.CommandText = sql;
                    reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var cols = reader.GetColumnSchema();
                        DataTable dt = new DataTable();
                        foreach (var item in cols)
                        {
                            if (!item.ColumnName.Equals("id") && !item.ColumnName.Equals("gang_id") && item.DataType != null)
                                dt.Columns.Add(item.ColumnName, item.DataType);
                        }

                        var gang = GangList.Find(x => x.DatabaseID == (int)reader["gang_id"]);
                        if (gang != null)
                        {
                            foreach (DataColumn item in dt.Columns)
                            {
                                var skill = gang.SkillList.Find(x => x.Name.Equals(item.ColumnName));
                                if (skill != null) skill.Level = (int)reader[item.ColumnName];
                            }
                        }

                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                LogError("(OnMapStart) Failed load map in database | " + ex.Message);
                throw new Exception("Failed load map in database! | " + ex.Message);
            }
        });
    }

    private void OnMapEnd()
    {
        foreach (var gang in GangList)
        {
            Task.Run(async () =>
            {
                try
                {
                    await using (var connection = new MySqlConnection(dbConnectionString))
                    {
                        await connection.OpenAsync();
                        await connection.QueryAsync($@"
                                UPDATE `{Config.Database.TableGroups}`
                                SET `exp` = @exp
                                WHERE `id` = @id",
                            new { exp = gang.Exp, id = gang.DatabaseID });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{ex.Message}");
                    throw;
                }
                /*finally
                {
                    Logger.LogInformation("[Gangs] (OnMapEnd) Clearing GangList");
                    GangList.Clear();
                }*/
            });
        }
    }
}