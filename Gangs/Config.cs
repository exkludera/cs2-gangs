using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

public class GangsConfig : BasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string Prefix { get; set; } = "{blue}[Gangs]{default}";


    [JsonPropertyName("DatabaseHost")]
    public string DatabaseHost { get; set; } = "localhost";

    [JsonPropertyName("DatabasePort")]
    public int DatabasePort { get; set; } = 3306;

    [JsonPropertyName("DatabaseUser")]
    public string DatabaseUser { get; set; } = "user";

    [JsonPropertyName("DatabasePassword")]
    public string DatabasePassword { get; set; } = "password";

    [JsonPropertyName("DatabaseName")]
    public string DatabaseName { get; set; } = "db";

    [JsonPropertyName("ServerId")]
    public int ServerId { get; set; } = 0;


    [JsonPropertyName("OpenCommands")]
    public string OpenCommands { get; set; } = "css_gangs;css_gang;css_g";


    [JsonPropertyName("MaxMembers")]
    public int MaxMembers { get; set; } = 10;

    [JsonPropertyName("ExpInc")]
    public int ExpInc { get; set; } = 100;

    [JsonPropertyName("ClanTags")]
    public bool ClanTags { get; set; } = true;

    [JsonPropertyName("OnlyTerrorists")]
    public bool OnlyTerrorists { get; set; } = false;


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