using CfbSim.Core.Generation;
using CfbSim.Core.Media;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Save;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;
using Xunit;

namespace CfbSim.Tests;

public class MediaTests
{
    private sealed record Setup(League League, SeasonState Season, SeriesHistory History, MediaStore Media, int UserId);

    private static Setup SimWithMedia(ulong seed, int weeks)
    {
        var rng = new Pcg32Rng(seed);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(rng, league, history, 2026);
        SeasonState state = SeasonDriver.Initialize(league, schedule);

        int userId = schedule.InWeek(1).First().HomeId;
        var media = new MediaStore();
        var subsystem = new MediaSubsystem(new TemplateNarrativeGenerator());

        for (int w = 1; w <= weeks; w++)
        {
            SeasonDriver.AdvanceWeek(rng, league, state, history);
            var rankings = RankingService.Rank(league, state.Records, state.Games);
            subsystem.GenerateWeek(w, league, state, rankings, userId, media);
        }
        return new Setup(league, state, history, media, userId);
    }

    [Fact]
    public void GeneratesCoverage_WithBothFeaturedAndShortArticles()
    {
        Setup s = SimWithMedia(1, 3);
        Assert.Equal(new[] { 1, 2, 3 }, s.Media.WeeksWithCoverage(2026));
        Assert.Contains(s.Media.Articles, a => a.Full);
        Assert.Contains(s.Media.Articles, a => !a.Full);
    }

    [Fact]
    public void UserGame_IsAlwaysFeatured()
    {
        Setup s = SimWithMedia(1, 2);
        // The user's week-1 game must have a full article mentioning the user team.
        Assert.Contains(s.Media.ForTeam(s.UserId), a => a.Full && a.Week == 1);
    }

    [Fact]
    public void RivalryGames_AreFeatured_AndNamed()
    {
        Setup s = SimWithMedia(1, 14);
        foreach (NewsArticle a in s.Media.Articles.Where(a => a.Type == ArticleType.RivalryResult))
        {
            Assert.True(a.Full);
            Assert.Contains("prevails", a.Headline); // rivalry template
        }
        Assert.Contains(s.Media.Articles, a => a.Type == ArticleType.RivalryResult);
    }

    [Fact]
    public void Generation_IsDeterministic()
    {
        var a = SimWithMedia(9, 3).Media.Articles.Select(x => (x.Headline, x.Body)).ToList();
        var b = SimWithMedia(9, 3).Media.Articles.Select(x => (x.Headline, x.Body)).ToList();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Media_RoundTripsInSave()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cfbsim_tests", nameof(Media_RoundTripsInSave));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);

        Setup s = SimWithMedia(2, 3);
        var save = new GameSave
        {
            Year = 2026,
            League = s.League,
            History = s.History,
            Season = s.Season,
            Media = s.Media,
            Rng = new Pcg32Rng(2).Snapshot(),
        };

        SaveManager.Save(dir, save);
        GameSave loaded = SaveManager.Load(dir);

        Assert.Equal(s.Media.Articles.Count, loaded.Media.Articles.Count);
        Assert.Equal(s.Media.Articles[0].Headline, loaded.Media.Articles[0].Headline);
        Assert.Equal(s.Media.Articles[0].Body, loaded.Media.Articles[0].Body);
    }
}
