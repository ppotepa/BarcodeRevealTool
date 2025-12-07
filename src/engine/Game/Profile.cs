using System.Text.Json.Serialization;

public class Rating
{
    [JsonPropertyName("leagueMax")]
    public int LeagueMax { get; set; }

    [JsonPropertyName("ratingMax")]
    public int RatingMax { get; set; }

    [JsonPropertyName("totalGamesPlayed")]
    public int TotalGamesPlayed { get; set; }

    [JsonPropertyName("previousStats")]
    public Previousstats? PreviousStats { get; set; }

    [JsonPropertyName("currentStats")]
    public Currentstats? CurrentStats { get; set; }

    [JsonPropertyName("members")]
    public Members? Members { get; set; }
}

public class Previousstats
{
    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("gamesPlayed")]
    public int GamesPlayed { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

public class Currentstats
{
    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("gamesPlayed")]
    public int GamesPlayed { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

public class Members
{
    [JsonPropertyName("protossGamesPlayed")]
    public int ProtossGamesPlayed { get; set; }

    [JsonPropertyName("terranGamesPlayed")]
    public int TerranGamesPlayed { get; set; }

    [JsonPropertyName("zergGamesPlayed")]
    public int ZergGamesPlayed { get; set; }

    [JsonPropertyName("character")]
    public Character? Character { get; set; }

    [JsonPropertyName("account")]
    public Account? Account { get; set; }

    [JsonPropertyName("raceGames")]
    public Racegames? RaceGames { get; set; }
}

public class Character
{
    [JsonPropertyName("realm")]
    public int Realm { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("accountId")]
    public int AccountId { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("battlenetId")]
    public int BattlenetId { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("discriminator")]
    public object? Discriminator { get; set; }
}

public class Account
{
    [JsonPropertyName("battleTag")]
    public string? BattleTag { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("partition")]
    public string? Partition { get; set; }

    [JsonPropertyName("hidden")]
    public object? Hidden { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("discriminator")]
    public int Discriminator { get; set; }
}

public class Racegames
{
    [JsonPropertyName("PROTOSS")]
    public int PROTOSS { get; set; }

    [JsonPropertyName("ZERG")]
    public int ZERG { get; set; }

    [JsonPropertyName("TERRAN")]
    public int TERRAN { get; set; }
}
