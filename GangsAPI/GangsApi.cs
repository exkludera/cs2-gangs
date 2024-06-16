using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace GangsAPI;

public interface GangsApi
{
	public static PluginCapability<GangsApi> Capability { get; } = new("Gangs_Core:API");
	string dbConnectionString { get; }
    public Task RegisterSkill(string skillName, int maxLevel, int price);
    public Task RegisterSkill(int gangID, string skillName, int maxLevel, int price);
    public bool UnRegisterSkill(string skillName);
    public int GetSkillLevel(CCSPlayerController player, string skillName);
    event Action? CoreReady;
    event Action<CCSPlayerController, int>? GangsCreated;
    event Action<CCSPlayerController, int>? ClientJoinGang;
}