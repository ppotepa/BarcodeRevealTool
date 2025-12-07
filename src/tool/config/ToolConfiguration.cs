
using Newtonsoft.Json;

public class ToolConfiguration
{
    [JsonProperty("user")]
    public User? User { get; set; }

    [JsonProperty("refreshInterval")]
    public int? RefreshInterval { get; set; }

    [JsonProperty("exposeApi")]
    public bool? ExposeApi { get; set; }
}

public class User
{
    [JsonProperty("battleTag")]
    public string? BattleTag { get; set; }
}
