using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

public class Database
{
    [JsonPropertyName("Host")]
    public string Host { get; set; } = "localhost";

    [JsonPropertyName("Port")]
    public int Port { get; set; } = 3306;

    [JsonPropertyName("User")]
    public string User { get; set; } = "user";

    [JsonPropertyName("Password")]
    public string Password { get; set; } = "password";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "db";

    [JsonPropertyName("TableGroups")]
    public string TableGroups { get; set; } = "gang_group";

    [JsonPropertyName("TablePerks")]
    public string TablePerks { get; set; } = "gang_perk";

    [JsonPropertyName("TablePlayers")]
    public string TablePlayers { get; set; } = "gang_player";
}

public class Settings
{
    [JsonPropertyName("MenuCommands")]
    public string MenuCommands { get; set; } = "css_gangs;css_gang";

    [JsonPropertyName("MaxMembers")]
    public int MaxMembers { get; set; } = 10;

    [JsonPropertyName("ExpInc")]
    public int ExpInc { get; set; } = 100;

    [JsonPropertyName("ClanTags")]
    public bool ClanTags { get; set; } = true;

    [JsonPropertyName("OnlyTerrorists")]
    public bool OnlyTerrorists { get; set; } = false;
}


public class CreateCost
{
    [JsonPropertyName("Value")]
    public int Value { get; set; } = 0;

    [JsonPropertyName("Days")]
    public int Days { get; set; } = 0;
}

public class ExtendCost
{
    [JsonPropertyName("Day")]
    public int Day { get; set; }

    [JsonPropertyName("Value")]
    public int Value { get; set; }
}

public class Prices
{
    [JsonPropertyName("Prices")]
    public List<ExtendCost> Value { get; set; } = new();
}

public class GangsConfig : BasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string Prefix { get; set; } = "{blue}[Gangs]{default}";

    [JsonPropertyName("Database")] public Database Database { get; set; } = new Database();

    [JsonPropertyName("Settings")] public Settings Settings { get; set; } = new Settings();

    [JsonPropertyName("CreateCost")]
    public CreateCost CreateCost { get; set; } = new CreateCost
    {
        Value = 1000,
        Days = 14
    };

    [JsonPropertyName("RenameCost")]
    public int RenameCost { get; set; } = 250;

    [JsonPropertyName("ExtendCost")]
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