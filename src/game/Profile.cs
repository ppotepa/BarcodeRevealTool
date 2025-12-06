
public class Profile
{
    public General[] Property1 { get; set; }
}

public class General
{
    public int leagueMax { get; set; }
    public int ratingMax { get; set; }
    public int totalGamesPlayed { get; set; }
    public Previousstats previousStats { get; set; }
    public Currentstats currentStats { get; set; }
    public Members members { get; set; }
}

public class Previousstats
{
    public int rating { get; set; }
    public int gamesPlayed { get; set; }
    public int rank { get; set; }
}

public class Currentstats
{
    public int rating { get; set; }
    public int gamesPlayed { get; set; }
    public int rank { get; set; }
}

public class Members
{
    public int randomGamesPlayed { get; set; }
    public Character character { get; set; }
    public Account account { get; set; }
    public Clan clan { get; set; }
    public int proId { get; set; }
    public string proNickname { get; set; }
    public string proTeam { get; set; }
    public Proplayer proPlayer { get; set; }
    public Racegames raceGames { get; set; }
}

public class Character
{
    public int realm { get; set; }
    public string name { get; set; }
    public int id { get; set; }
    public int accountId { get; set; }
    public string region { get; set; }
    public int battlenetId { get; set; }
    public string tag { get; set; }
    public object discriminator { get; set; }
}

public class Account
{
    public string battleTag { get; set; }
    public int id { get; set; }
    public string partition { get; set; }
    public object hidden { get; set; }
    public string tag { get; set; }
    public int discriminator { get; set; }
}

public class Clan
{
    public string tag { get; set; }
    public int id { get; set; }
    public string region { get; set; }
    public string name { get; set; }
    public int members { get; set; }
    public int activeMembers { get; set; }
    public int avgRating { get; set; }
    public int avgLeagueType { get; set; }
    public int games { get; set; }
}

public class Proplayer
{
    public Proplayer1 proPlayer { get; set; }
    public Proteam proTeam { get; set; }
    public object[] links { get; set; }
}

public class Proplayer1
{
    public int id { get; set; }
    public object aligulacId { get; set; }
    public string nickname { get; set; }
    public object name { get; set; }
    public object country { get; set; }
    public object birthday { get; set; }
    public object earnings { get; set; }
    public object updated { get; set; }
    public object version { get; set; }
}

public class Proteam
{
    public object name { get; set; }
    public string shortName { get; set; }
    public object id { get; set; }
    public object aligulacId { get; set; }
    public object updated { get; set; }
}

public class Racegames
{
    public int RANDOM { get; set; }
}
