using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using Xunit;

namespace CfbSim.Tests;

public class LeagueBuilderTests
{
    private static League Build(ulong seed = 1) => LeagueBuilder.Build(new Pcg32Rng(seed));

    [Fact]
    public void League_HasFullFbsFieldWithUniqueIds()
    {
        League league = Build();
        int teams = league.AllTeams.Count();

        Assert.InRange(league.Conferences.Count, 9, 12);
        Assert.InRange(teams, 130, 140);                       // ~134 FBS teams
        Assert.Equal(teams, league.AllTeams.Select(t => t.Id).Distinct().Count()); // unique ids
        Assert.All(league.Conferences, c => Assert.NotEmpty(c.Teams));
        Assert.Equal(4, league.Conferences.Count(c => c.IsPower)); // SEC, B1G, ACC, B12
    }

    [Fact]
    public void EveryTeam_HasARosterAndItsConferenceId()
    {
        League league = Build();
        foreach (Conference c in league.Conferences)
            foreach (Team t in c.Teams)
            {
                Assert.Equal(c.Id, t.ConferenceId);
                Assert.NotNull(t.Starter(Position.QB));
                Assert.NotNull(t.Starter(Position.OL));
                Assert.NotNull(t.Starter(Position.DL));
            }
        Assert.All(league.Independents, t => Assert.Equal(0, t.ConferenceId));
    }

    [Fact]
    public void KnownTeams_AreFindable_InExpectedConferences()
    {
        League league = Build();
        Team georgia = league.FindTeam("Georgia")!;
        Team ohioState = league.FindTeam("OSU")!;
        Team notreDame = league.FindTeam("Notre Dame")!;

        Assert.Equal("SEC", league.ConferenceOf(georgia)!.Abbreviation);
        Assert.Equal("B1G", league.ConferenceOf(ohioState)!.Abbreviation);
        Assert.Null(league.ConferenceOf(notreDame)); // independent
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        League a = Build(7);
        League b = Build(7);
        var aTeams = a.AllTeams.ToList();
        var bTeams = b.AllTeams.ToList();
        Assert.Equal(aTeams.Count, bTeams.Count);
        for (int i = 0; i < aTeams.Count; i++)
        {
            Assert.Equal(aTeams[i].Name, bTeams[i].Name);
            Assert.Equal(aTeams[i].Starter(Position.QB)!.Attributes.Awareness,
                         bTeams[i].Starter(Position.QB)!.Attributes.Awareness);
        }
    }
}
