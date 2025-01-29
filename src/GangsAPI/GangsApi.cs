using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace GangsAPI
{
    public interface GangsApi
    {
        public static PluginCapability<GangsApi> Capability { get; } = new("Gangs_Core:API");

        string dbConnectionString { get; }

        Task RegisterSkill(string skillName, int maxLevel, int price);

        Task RegisterSkill(int gangId, string skillName, int maxLevel, int price);

        bool UnRegisterSkill(string skillName);

        int GetSkillLevel(CCSPlayerController player, string skillName);

        event Action? CoreReady;

        event Action<CCSPlayerController, int>? GangsCreated;

        event Action<CCSPlayerController, int>? ClientJoinGang;

        bool OnlyTerroristCheck(CCSPlayerController player);

        List<ulong> GetGangMembers(int gangId);

        int GetGangId(CCSPlayerController player);

        string GetGangName(int gangId);
    }
}