namespace CfbSim.Core.Data;

/// <summary>
/// Real FBS teams and conferences (≈2025 alignment) with rough prestige (1–100).
///
/// NOTE: conference membership is volatile due to ongoing realignment. The Power-4
/// (SEC/Big Ten/ACC/Big 12) here is solid; the Pac-12 rebuild and several Group-of-5
/// moves (MWC↔Pac-12, AAC/C-USA/MAC/Sun Belt churn) are the most likely to need
/// edits. This is plain data — fix any membership/prestige here without touching code.
/// Prestige is a subjective first pass and a tuning surface.
/// </summary>
public static class FbsLeagueData
{
    public sealed record TeamData(string Name, string Abbr, int Prestige);
    public sealed record ConferenceData(string Name, string Abbr, bool Power, TeamData[] Teams);

    public static IReadOnlyList<ConferenceData> Conferences { get; } = new[]
    {
        new ConferenceData("Southeastern Conference", "SEC", true, new[]
        {
            new TeamData("Alabama", "ALA", 92),
            new TeamData("Arkansas", "ARK", 68),
            new TeamData("Auburn", "AUB", 80),
            new TeamData("Florida", "FLA", 78),
            new TeamData("Georgia", "UGA", 94),
            new TeamData("Kentucky", "UK", 64),
            new TeamData("LSU", "LSU", 85),
            new TeamData("Mississippi State", "MSST", 60),
            new TeamData("Missouri", "MIZ", 74),
            new TeamData("Oklahoma", "OU", 84),
            new TeamData("Ole Miss", "MISS", 76),
            new TeamData("South Carolina", "SC", 70),
            new TeamData("Tennessee", "TENN", 82),
            new TeamData("Texas", "TEX", 88),
            new TeamData("Texas A&M", "TAMU", 80),
            new TeamData("Vanderbilt", "VAN", 52),
        }),
        new ConferenceData("Big Ten Conference", "B1G", true, new[]
        {
            new TeamData("Illinois", "ILL", 64),
            new TeamData("Indiana", "IND", 62),
            new TeamData("Iowa", "IOWA", 74),
            new TeamData("Maryland", "MD", 62),
            new TeamData("Michigan", "MICH", 88),
            new TeamData("Michigan State", "MSU", 66),
            new TeamData("Minnesota", "MINN", 66),
            new TeamData("Nebraska", "NEB", 68),
            new TeamData("Northwestern", "NW", 56),
            new TeamData("Ohio State", "OSU", 94),
            new TeamData("Oregon", "ORE", 86),
            new TeamData("Penn State", "PSU", 84),
            new TeamData("Purdue", "PUR", 56),
            new TeamData("Rutgers", "RUT", 58),
            new TeamData("UCLA", "UCLA", 66),
            new TeamData("USC", "USC", 80),
            new TeamData("Washington", "WASH", 76),
            new TeamData("Wisconsin", "WIS", 72),
        }),
        new ConferenceData("Atlantic Coast Conference", "ACC", true, new[]
        {
            new TeamData("Boston College", "BC", 56),
            new TeamData("California", "CAL", 58),
            new TeamData("Clemson", "CLEM", 86),
            new TeamData("Duke", "DUKE", 60),
            new TeamData("Florida State", "FSU", 80),
            new TeamData("Georgia Tech", "GT", 64),
            new TeamData("Louisville", "LOU", 72),
            new TeamData("Miami", "MIA", 80),
            new TeamData("NC State", "NCST", 66),
            new TeamData("North Carolina", "UNC", 64),
            new TeamData("Pittsburgh", "PITT", 66),
            new TeamData("SMU", "SMU", 70),
            new TeamData("Stanford", "STAN", 58),
            new TeamData("Syracuse", "SYR", 60),
            new TeamData("Virginia", "UVA", 54),
            new TeamData("Virginia Tech", "VT", 64),
            new TeamData("Wake Forest", "WAKE", 56),
        }),
        new ConferenceData("Big 12 Conference", "B12", true, new[]
        {
            new TeamData("Arizona", "ARIZ", 64),
            new TeamData("Arizona State", "ASU", 70),
            new TeamData("Baylor", "BAY", 68),
            new TeamData("BYU", "BYU", 72),
            new TeamData("Cincinnati", "CIN", 64),
            new TeamData("Colorado", "COLO", 70),
            new TeamData("Houston", "HOU", 60),
            new TeamData("Iowa State", "ISU", 72),
            new TeamData("Kansas", "KU", 62),
            new TeamData("Kansas State", "KSU", 74),
            new TeamData("Oklahoma State", "OKST", 72),
            new TeamData("TCU", "TCU", 72),
            new TeamData("Texas Tech", "TTU", 72),
            new TeamData("UCF", "UCF", 64),
            new TeamData("Utah", "UTAH", 76),
            new TeamData("West Virginia", "WVU", 62),
        }),
        new ConferenceData("American Athletic Conference", "AAC", false, new[]
        {
            new TeamData("Army", "ARMY", 64),
            new TeamData("Charlotte", "CLT", 40),
            new TeamData("East Carolina", "ECU", 52),
            new TeamData("Florida Atlantic", "FAU", 46),
            new TeamData("Memphis", "MEM", 64),
            new TeamData("Navy", "NAVY", 56),
            new TeamData("North Texas", "UNT", 50),
            new TeamData("Rice", "RICE", 42),
            new TeamData("South Florida", "USF", 54),
            new TeamData("Temple", "TEM", 40),
            new TeamData("Tulane", "TULN", 64),
            new TeamData("Tulsa", "TLSA", 42),
            new TeamData("UAB", "UAB", 46),
            new TeamData("UTSA", "UTSA", 56),
        }),
        new ConferenceData("Mountain West Conference", "MWC", false, new[]
        {
            new TeamData("Air Force", "AF", 58),
            new TeamData("Boise State", "BSU", 70),
            new TeamData("Colorado State", "CSU", 56),
            new TeamData("Fresno State", "FRES", 58),
            new TeamData("Hawaii", "HAW", 44),
            new TeamData("Nevada", "NEV", 42),
            new TeamData("New Mexico", "UNM", 44),
            new TeamData("San Diego State", "SDSU", 54),
            new TeamData("San Jose State", "SJSU", 50),
            new TeamData("UNLV", "UNLV", 56),
            new TeamData("Utah State", "USU", 50),
            new TeamData("Wyoming", "WYO", 52),
        }),
        new ConferenceData("Pac-12 Conference", "PAC", false, new[]
        {
            new TeamData("Oregon State", "ORST", 60),
            new TeamData("Washington State", "WSU", 60),
        }),
        new ConferenceData("Conference USA", "CUSA", false, new[]
        {
            new TeamData("Delaware", "DEL", 40),
            new TeamData("Jacksonville State", "JVST", 46),
            new TeamData("Kennesaw State", "KENN", 34),
            new TeamData("Liberty", "LIB", 62),
            new TeamData("Louisiana Tech", "LT", 44),
            new TeamData("Middle Tennessee", "MTSU", 42),
            new TeamData("Missouri State", "MOST", 36),
            new TeamData("New Mexico State", "NMSU", 44),
            new TeamData("Sam Houston", "SHSU", 42),
            new TeamData("UTEP", "UTEP", 38),
            new TeamData("Western Kentucky", "WKU", 50),
        }),
        new ConferenceData("Mid-American Conference", "MAC", false, new[]
        {
            new TeamData("Akron", "AKR", 34),
            new TeamData("Ball State", "BALL", 40),
            new TeamData("Bowling Green", "BGSU", 46),
            new TeamData("Buffalo", "BUFF", 44),
            new TeamData("Central Michigan", "CMU", 46),
            new TeamData("Eastern Michigan", "EMU", 42),
            new TeamData("Kent State", "KENT", 34),
            new TeamData("Miami (OH)", "M-OH", 50),
            new TeamData("Northern Illinois", "NIU", 50),
            new TeamData("Ohio", "OHIO", 50),
            new TeamData("Toledo", "TOL", 56),
            new TeamData("UMass", "MASS", 36),
            new TeamData("Western Michigan", "WMU", 46),
        }),
        new ConferenceData("Sun Belt Conference", "SBC", false, new[]
        {
            new TeamData("Appalachian State", "APP", 58),
            new TeamData("Arkansas State", "ARST", 46),
            new TeamData("Coastal Carolina", "CCU", 52),
            new TeamData("Georgia Southern", "GASO", 50),
            new TeamData("Georgia State", "GAST", 44),
            new TeamData("James Madison", "JMU", 60),
            new TeamData("Louisiana", "UL", 58),
            new TeamData("UL Monroe", "ULM", 38),
            new TeamData("Marshall", "MRSH", 52),
            new TeamData("Old Dominion", "ODU", 44),
            new TeamData("South Alabama", "USA", 50),
            new TeamData("Southern Miss", "USM", 44),
            new TeamData("Texas State", "TXST", 50),
            new TeamData("Troy", "TROY", 54),
        }),
    };

    /// <summary>FBS independents (conference id 0).</summary>
    public static IReadOnlyList<TeamData> Independents { get; } = new[]
    {
        new TeamData("Notre Dame", "ND", 88),
        new TeamData("UConn", "CONN", 46),
    };
}
