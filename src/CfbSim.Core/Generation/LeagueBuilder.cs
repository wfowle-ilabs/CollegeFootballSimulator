using CfbSim.Core.Data;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Generation;

/// <summary>
/// Builds the full FBS <see cref="League"/> from <see cref="FbsLeagueData"/>: real
/// team and conference identities, each team's roster generated (prestige-weighted)
/// by <see cref="PlayerGenerator"/>. Deterministic for a given seed.
/// </summary>
public static class LeagueBuilder
{
    public static League Build(IRng rng, PlayerGenerator? generator = null)
    {
        generator ??= new PlayerGenerator();
        var league = new League { Name = "FBS" };
        int teamId = 0;
        int conferenceId = 0;

        foreach (FbsLeagueData.ConferenceData cd in FbsLeagueData.Conferences)
        {
            conferenceId++;
            var conference = new Conference
            {
                Id = conferenceId,
                Name = cd.Name,
                Abbreviation = cd.Abbr,
                IsPower = cd.Power,
            };

            foreach (FbsLeagueData.TeamData td in cd.Teams)
            {
                Team team = generator.GenerateTeam(rng, ++teamId, td.Name, td.Abbr, td.Prestige);
                team.ConferenceId = conferenceId;
                conference.Teams.Add(team);
            }

            league.Conferences.Add(conference);
        }

        foreach (FbsLeagueData.TeamData td in FbsLeagueData.Independents)
        {
            Team team = generator.GenerateTeam(rng, ++teamId, td.Name, td.Abbr, td.Prestige);
            team.ConferenceId = 0;
            league.Independents.Add(team);
        }

        return league;
    }
}
