using CounterStrikeSharp.API.Core;
using GangsAPI;

namespace Gangs.Api
{
    public class ApiGangs : GangsApi
    {
        public event Action? CoreReady;
        public event Action<CCSPlayerController, int>? GangsCreated;
        public event Action<CCSPlayerController, int>? ClientJoinGang;
        private readonly Plugin plugin;

        public string dbConnectionString { get; }
        public ApiGangs(Plugin Gangs)
        {
            plugin = Gangs;
            dbConnectionString = Gangs.dbConnectionString;
        }

        public async Task RegisterSkill(string skillName, int maxLevel, int price)
        {
            await plugin.AddSkillInDB(skillName, maxLevel, price);
        }

        public async Task RegisterSkill(int gangID, string skillName, int maxLevel, int price)
        {
            await plugin.AddSkillInDB(gangID, skillName, maxLevel, price);
        }

        public bool UnRegisterSkill(string skillName)
        {
            foreach (var gang in plugin.GangList)
            {
                var skill = gang.SkillList.Find(x => x.Name.Equals(skillName));
                if (skill != null) gang.SkillList.Remove(skill);
            }

            return true;
        }

        public int GetSkillLevel(CCSPlayerController player, string skillName)
        {
            var gang = plugin.GangList.Find(x => x.DatabaseID == plugin.userInfo[player.Slot].GangId);

            if (gang == null)
                return -1;

            var skill = gang.SkillList.Find(x => x.Name.Equals(skillName));

            return skill?.Level ?? -1;
        }

        public void OnCoreReady()
        {
            CoreReady?.Invoke();
        }

        public void OnGangsCreated(CCSPlayerController player, int GangId)
        {
            GangsCreated?.Invoke(player, GangId);
        }

        public void OnClientJoinGang(CCSPlayerController player, int GangId)
        {
            ClientJoinGang?.Invoke(player, GangId);
        }

        public void OnClientBuySkill(CCSPlayerController player, int GangId)
        {
            ClientJoinGang?.Invoke(player, GangId);
        }

        public bool OnlyTerroristCheck(CCSPlayerController player)
        {
            return plugin.Config.Settings.OnlyTerrorists && player.Team != CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist;
        }

        public int GetGangId(CCSPlayerController player)
        {
            var userInfo = plugin.userInfo[player.Slot];

            if (userInfo == null)
                return -1;

            var gang = plugin.GangList.Find(x => x.DatabaseID == userInfo.GangId);

            if (gang == null)
                return -1;

            return gang.DatabaseID;
        }

        public string GetGangName(int gangId)
        {
            var gang = plugin.GangList.Find(x => x.DatabaseID == gangId);

            if (gang == null || string.IsNullOrEmpty(gang.Name))
                return "Unknown Gang";

            return gang.Name;
        }

        public List<ulong> GetGangMembers(int gangId)
        {
            var memberUserInfos = new List<ulong>();

            var gang = plugin.GangList.Find(x => x.DatabaseID == gangId);

            if (gang == null)
                return memberUserInfos;

            foreach (var userInfo in plugin.userInfo.Values)
            {
                if (userInfo.GangId == gangId)
                    memberUserInfos.Add(userInfo.SteamID);
            }

            return memberUserInfos;
        }

    }
}