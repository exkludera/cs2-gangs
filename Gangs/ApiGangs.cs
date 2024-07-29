using CounterStrikeSharp.API.Core;
using GangsAPI;

namespace Gangs.Api
{
    public class ApiGangs : GangsApi
    {
        public event Action? CoreReady;
        public event Action<CCSPlayerController, int>? GangsCreated;
        public event Action<CCSPlayerController, int>? ClientJoinGang;
        private readonly Gangs _Gangs;

        public string dbConnectionString { get; }
        public ApiGangs(Gangs Gangs)
        {
            _Gangs = Gangs;
            dbConnectionString = Gangs.dbConnectionString;
        }

        public async Task RegisterSkill(string skillName, int maxLevel, int price)
        {
            await _Gangs.AddSkillInDB(skillName, maxLevel, price);
        }

        public async Task RegisterSkill(int gangID, string skillName, int maxLevel, int price)
        {
            await _Gangs.AddSkillInDB(gangID, skillName, maxLevel, price);
        }

        public bool UnRegisterSkill(string skillName)
        {
            foreach (var gang in _Gangs.GangList)
            {
                var skill = gang.SkillList.Find(x => x.Name.Equals(skillName));
                if (skill != null) gang.SkillList.Remove(skill);
            }
            return true;
        }

        public int GetSkillLevel(CCSPlayerController player, string skillName)
        {
            var gang = _Gangs.GangList.Find(x => x.DatabaseID == _Gangs.userInfo[player.Slot].GangId);
            if (gang == null) return -1;
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
            if (_Gangs.Config.Settings.OnlyTerrorists && player.Team != CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist)
                return true;

            return false;
        }
    }
}