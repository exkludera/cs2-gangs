using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Text.RegularExpressions;
using Dapper;

namespace Gangs;

public partial class Gangs : BasePlugin, IPluginConfig<GangsConfig>
{
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
        if (player == null || !player.IsValid || player.IsBot)
            return;
            
        if (!Config.ClanTags)
            return;

        var gang = GangList.Find(x => x.DatabaseID == userInfo[player.Slot].GangId);

        try
        {
            AddTimer(3.0f, () =>
            {
                string originalPlayerName = player.PlayerName;
                string stripedClanTag = RemovePlayerTags(player.Clan ?? "");
                string clanTag = (_api!.OnlyTerroristCheck(player) || string.IsNullOrEmpty(gang?.name)) ? "" : $" [{gang.name}]";

                player.Clan = stripedClanTag + clanTag;
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
                    var data = (IDictionary<string, object>)countmembers.First();
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