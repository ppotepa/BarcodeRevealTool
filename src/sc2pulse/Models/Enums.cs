using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Queue
    {
        WOL_1V1,
        WOL_2V2,
        WOL_3V3,
        WOL_4V4,
        HOTS_1V1,
        HOTS_2V2,
        HOTS_3V3,
        HOTS_4V4,
        LOTV_1V1,
        LOTV_2V2,
        LOTV_3V3,
        LOTV_4V4,
        LOTV_ARCHON
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Region
    {
        US,
        EU,
        KR,
        CN,
        SEA,
        GLOBAL
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Race
    {
        TERRAN,
        PROTOSS,
        ZERG,
        RANDOM
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum League
    {
        BRONZE = 0,
        SILVER,
        GOLD,
        PLATINUM,
        DIAMOND,
        MASTER,
        GRANDMASTER
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchKind
    {
        _1V1,
        _2V2,
        _3V3,
        _4V4,
        ARCHON,
        COOP,
        CUSTOM,
        UNKNOWN
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LinkType
    {
        ALIGULAC,
        TWITCH,
        LIQUIPEDIA,
        TWITTER,
        INSTAGRAM,
        DISCORD,
        YOUTUBE,
        UNKNOWN,
        BATTLE_NET,
        REPLAY_STATS,
        BILIBILI
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TeamArrangement
    {
        ARRANGED,
        RANDOM
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LadderTier
    {
        FIRST,
        SECOND,
        THIRD
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TeamHistorySummaryMetric
    {
        GAMES,
        RATING_MIN,
        RATING_AVG,
        RATING_MAX,
        RATING_LAST,
        REGION_RANK_LAST,
        REGION_TEAM_COUNT_LAST
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TeamHistoryStaticField
    {
        ID,
        REGION,
        QUEUE_TYPE,
        TEAM_TYPE,
        LEGACY_ID,
        SEASON,
        LEGACY_UID
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TeamHistoryGroupBy
    {
        TEAM,
        LEGACY_UID
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TeamHistoryMetric
    {
        TIMESTAMP,
        RATING,
        GAMES,
        WINS,
        LEAGUE_TYPE,
        TIER_TYPE,
        DIVISION_ID,
        GLOBAL_RANK,
        REGION_RANK,
        LEAGUE_RANK,
        GLOBAL_TEAM_COUNT,
        REGION_TEAM_COUNT,
        LEAGUE_TEAM_COUNT,
        ID,
        SEASON
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ClanEventType
    {
        LEAVE,
        JOIN
    }
}
