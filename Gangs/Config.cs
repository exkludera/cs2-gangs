using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Gangs
{
	public class GangsConfig : BasePluginConfig
	{
        [JsonPropertyName("OpenCommands")]
		public string OpenCommands { get; set; } = "css_gangs;css_gang;css_g";

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

		[JsonPropertyName("CreateCost")]
		public CreateCost CreateCost { get; set; } = new();

		[JsonPropertyName("RenameCost")]
		public RenameCost RenameCost { get; set; } = new();

		[JsonPropertyName("ExtendCost")]
		public Prices ExtendCost { get; set; } = new();

		[JsonPropertyName("MaxMembers")]
		public int MaxMembers { get; set; } = 10;

		[JsonPropertyName("ExpInc")]
		public int ExpInc { get; set; } = 100;

        [JsonPropertyName("ConfigVersion")]
        public override int Version { get; set; } = 1;
    }
	public class CreateCost
	{
        [JsonPropertyName("Value")]
		public int Value { get; set; } = 0;

        [JsonPropertyName("Days")]
		public int Days { get; set; } = 0;
	}
	public class RenameCost
	{
        [JsonPropertyName("Value")]
		public int Value { get; set; } = 0;
	}
	public class Prices
	{
		[JsonPropertyName("Prices")]
		public List<ExtendCost> Value { get; set; } = new();
	}
	public class ExtendCost
    {
		[JsonPropertyName("Day")]
		public int Day { get; set; }

		[JsonPropertyName("Value")]
		public int Value { get; set; }
	}
}