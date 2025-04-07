﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Dapper;
using TagsApi;
using CounterStrikeSharp.API.Modules.Commands.Targeting;

namespace Gangs;

public partial class Plugin
{
    public void LogError(string message)
    {
        Logger.LogError($"[Gangs] {message}");
    }

    public void PrintToChat(CCSPlayerController player, string message)
    {
        player.PrintToChat(Config.Prefix + message);
    }

    public void PrintToChatAll(string message)
    {
        Server.PrintToChatAll(Config.Prefix + message);
    }

    public async Task EnsureColumnExists(string skillName)
    {
        try
        {
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();
                string checkColumnSql = $"SELECT COUNT(*) FROM information_schema.columns WHERE TABLE_SCHEMA = '{Config.Database.Name}' AND table_name = '{Config.Database.TablePerks}' AND column_name = '{skillName}';";
                var checkColumnCommand = connection.CreateCommand();
                checkColumnCommand.CommandText = checkColumnSql;
                int columnExists = Convert.ToInt32(await checkColumnCommand.ExecuteScalarAsync());

                if (columnExists == 0)
                {
                    string addColumnSql = $"ALTER TABLE `{Config.Database.TablePerks}` ADD COLUMN `{skillName}` int(32) NOT NULL DEFAULT 0;";
                    var addColumnCommand = connection.CreateCommand();
                    addColumnCommand.CommandText = addColumnSql;
                    await addColumnCommand.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            LogError("(EnsureColumnExists) Failed to ensure column existence | " + ex.Message);
            throw new Exception("Failed to ensure column existence! | " + ex.Message);
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
                        string sql = $"SELECT `{skillName}` FROM `{Config.Database.TablePerks}` WHERE `gang_id` = {gang.DatabaseID};";
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
                        LogError("(AddSkillInDB) Failed to add skill in database 2! | " + ex.Message);
                        throw new Exception("Failed to add skill in database 2! | " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError("(AddSkillInDB) Failed to add skill in database | " + ex.Message);
            throw new Exception("Failed to add skill in database! | " + ex.Message);
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
                        string sql = $"SELECT `{skillName}` FROM `{Config.Database.TablePerks}` WHERE `gang_id` = {gang.DatabaseID};";
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
                    LogError("(AddSkillInDB) Failed to add skill in database 2! | " + ex.Message);
                    throw new Exception("Failed to add skill in database 2! | " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            LogError("(AddSkillInDB) Failed to add skill in database | " + ex.Message);
            throw new Exception("Failed to add skill in database! | " + ex.Message);
        }
    }

    public void AddScoreboardTagToPlayer(CCSPlayerController player, bool all = false)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        if (!Config.Settings.ClanTags)
            return;

        if (TagApi == null)
            return;

        var gang = GangList.Find(x => x.DatabaseID == userInfo[player.Slot].GangId);

        string gangTag = string.IsNullOrEmpty(gang?.Name) ? "" : $" [{gang.Name}]";

        if (all)
        {
            var members = userInfo.Values.Where(x => x.DatabaseID == userInfo[player.Slot].GangId);

            foreach (var member in members)
            {
                var target = Utilities.GetPlayerFromSteamId(member.SteamID);

                if (target == null)
                    continue;

                SetClanTag(target, gangTag);
            }
        }
        else SetClanTag(player, gangTag);
    }

    private void SetClanTag(CCSPlayerController player, string tag)
    {
        if (TagApi == null)
            return;

        TagApi.ResetAttribute(player, Tags.TagType.ScoreTag);

        Server.NextFrame(() => {
            string oldTag = TagApi.GetAttribute(player, Tags.TagType.ScoreTag) ?? "";

            TagApi.SetAttribute(player, Tags.TagType.ScoreTag, oldTag + tag);
        });
    }

    public async Task<int> GetMembersCount(int gang_id)
    {
        try
        {
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();

                var countmembers = await connection.QueryAsync($@"
                SELECT COUNT(*) as Count FROM `{Config.Database.TablePlayers}` WHERE `gang_id` = @gangid",
                    new { gangid = gang_id });

                var data = (IDictionary<string, object>)countmembers.First();
                var count = Convert.ToInt32(data["Count"]);

                return count;
            }
        }
        catch (Exception ex)
        {
            LogError("(GetMembersCount) Failed get value in database | " + ex.Message);
            throw new Exception("Failed get value in database! | " + ex.Message);
        }
    }

    public int GetGangLevel(Gang gang)
    {
        if (gang == null)
            return -1;

        return gang.Exp / Config.Settings.ExpInc;
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