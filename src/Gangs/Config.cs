using CounterStrikeSharp.API.Core;

namespace Gangs;

public class Config : BasePluginConfig
{
    public string Prefix { get; set; } = "{blue}[Gangs]{default}";

    public class Config_Database
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 3306;
        public string User { get; set; } = "user";
        public string Password { get; set; } = "password";
        public string Name { get; set; } = "db";
        public string TableGroups { get; set; } = "gang_group";
        public string TablePerks { get; set; } = "gang_perk";
        public string TablePlayers { get; set; } = "gang_player";
    }
    public Config_Database Database { get; set; } = new Config_Database();

    public class Config_Settings
    {
        public string MenuCommands { get; set; } = "css_gangs;css_gang";
        public string MenuType { get; set; } = "chat";
        public int MaxMembers { get; set; } = 10;
        public int ExpInc { get; set; } = 100;
        public bool ClanTags { get; set; } = true;
        public bool OnlyTerrorists { get; set; } = false;
    }

    public Config_Settings Settings { get; set; } = new Config_Settings();


    public class Config_CreateCost
    {
        public int Value { get; set; } = 0;
        public int Days { get; set; } = 0;
    }
    public Config_CreateCost CreateCost { get; set; } = new Config_CreateCost
    {
        Value = 1000,
        Days = 14
    };

    public int RenameCost { get; set; } = 250;

    public class Config_ExtendCost
    {
        public int Day { get; set; }
        public int Value { get; set; }
    }
    public class Config_Prices
    {
        public List<Config_ExtendCost> Value { get; set; } = new();
    }
    public Config_Prices ExtendCost { get; set; } = new Config_Prices
    {
        Value = new List<Config_ExtendCost>
        {
            new Config_ExtendCost { Day = 7, Value = 100 },
            new Config_ExtendCost { Day = 14, Value = 200 },
            new Config_ExtendCost { Day = 30, Value = 500 },
            new Config_ExtendCost { Day = 90, Value = 1500 }
        }
    };
}