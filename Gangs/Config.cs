using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

public class Database
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

public class Settings
{
    public string MenuCommands { get; set; } = "css_gangs;css_gang";
    public string MenuType { get; set; } = "chat";
    public int MaxMembers { get; set; } = 10;
    public int ExpInc { get; set; } = 100;
    public bool ClanTags { get; set; } = true;
    public bool OnlyTerrorists { get; set; } = false;
}


public class CreateCost
{
    public int Value { get; set; } = 0;
    public int Days { get; set; } = 0;
}

public class ExtendCost
{
    public int Day { get; set; }
    public int Value { get; set; }
}

public class Prices
{
    [JsonPropertyName("Prices")]
    public List<ExtendCost> Value { get; set; } = new();
}

public class Config : BasePluginConfig
{
    public string Prefix { get; set; } = "{blue}[Gangs]{default}";

    public Database Database { get; set; } = new Database();

    public Settings Settings { get; set; } = new Settings();

    public CreateCost CreateCost { get; set; } = new CreateCost
    {
        Value = 1000,
        Days = 14
    };

    public int RenameCost { get; set; } = 250;
    public Prices ExtendCost { get; set; } = new Prices
    {
        Value = new List<ExtendCost>
        {
            new ExtendCost { Day = 7, Value = 100 },
            new ExtendCost { Day = 14, Value = 200 },
            new ExtendCost { Day = 30, Value = 500 },
            new ExtendCost { Day = 90, Value = 1500 }
        }
    };
}