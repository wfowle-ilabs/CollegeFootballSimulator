using CfbSim.Core.Events;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Stats;

namespace CfbSim.Core.Sim.Game;

/// <summary>
/// Top of the play-by-play engine: runs a full game as a sequence of drives,
/// managing possession, the clock, halves, kickoffs (touchback-to-25 in M2),
/// scoring, and overtime. Emits domain events through an optional sink.
/// </summary>
public static class GameSimulator
{
    public static GameResult Simulate(IRng rng, Team home, Team away, IEventSink? sink = null, double passBias = 0.46)
    {
        var box = new BoxScore
        {
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            HomeName = home.Name,
            AwayName = away.Name,
        };
        var clock = new GameClock();
        var result = new GameResult { Home = home, Away = away, Box = box };

        int homeScore = 0, awayScore = 0;
        bool homeReceivesFirst = rng.NextInt(0, 1) == 0;
        bool offenseIsHome = homeReceivesFirst;
        int ballOn = 25;
        bool secondHalfStarted = false;

        while (true)
        {
            Team offense = offenseIsHome ? home : away;
            Team defense = offenseIsHome ? away : home;
            int offScore = offenseIsHome ? homeScore : awayScore;
            int defScore = offenseIsHome ? awayScore : homeScore;

            result.PlayLog.Add($"-- {offense.Abbreviation} ball at {Field.Describe(ballOn)} ({clock.Label()}) --");
            DriveResult dr = DriveSimulator.Run(rng, offense, defense, box, clock, result.PlayLog,
                ballOn, offScore, defScore, passBias, driveInFirstHalf: !secondHalfStarted, untimed: false);

            // Award points.
            if (dr.OffensePoints > 0)
            {
                if (offenseIsHome) homeScore += dr.OffensePoints; else awayScore += dr.OffensePoints;
                AddScore(result, sink, offense, ScoreKind(dr.Reason), homeScore, awayScore, clock, home, away);
            }
            if (dr.SafetyConceded)
            {
                if (offenseIsHome) awayScore += 2; else homeScore += 2;
                AddScore(result, sink, defense, "Safety", homeScore, awayScore, clock, home, away);
            }
            box.Home.Points = homeScore;
            box.Away.Points = awayScore;

            if (clock.RegulationOver || dr.Reason == DriveEndReason.EndOfGame)
                break;

            if (!secondHalfStarted && (dr.Reason == DriveEndReason.EndOfHalf || clock.PastHalf))
            {
                secondHalfStarted = true;
                offenseIsHome = !homeReceivesFirst; // the other team receives the 2nd-half kickoff
                ballOn = 25;
                result.PlayLog.Add("== HALFTIME ==");
                continue;
            }

            // Possession flips to the other team.
            offenseIsHome = !offenseIsHome;
            ballOn = dr.Reason == DriveEndReason.Safety ? 35 : dr.NextBallOn == -1 ? 25 : dr.NextBallOn;
        }

        // Overtime: alternating possessions from the opponent's 25 until someone leads.
        int ot = 0;
        while (homeScore == awayScore && ot < 6)
        {
            ot++;
            result.PlayLog.Add($"== OVERTIME {ot} ==");
            DriveResult h = DriveSimulator.Run(rng, home, away, box, clock, result.PlayLog, 75, homeScore, awayScore, passBias, false, untimed: true);
            homeScore += h.OffensePoints; if (h.SafetyConceded) awayScore += 2;
            DriveResult a = DriveSimulator.Run(rng, away, home, box, clock, result.PlayLog, 75, awayScore, homeScore, passBias, false, untimed: true);
            awayScore += a.OffensePoints; if (a.SafetyConceded) homeScore += 2;
            box.Home.Points = homeScore;
            box.Away.Points = awayScore;
        }

        result.Overtimes = ot;
        result.HomeScore = homeScore;
        result.AwayScore = awayScore;
        sink?.Publish(new GameConcluded(home.Id, away.Id, home.Name, away.Name, homeScore, awayScore));
        return result;
    }

    private static void AddScore(GameResult result, IEventSink? sink, Team scorer, string kind,
        int homeScore, int awayScore, GameClock clock, Team home, Team away)
    {
        result.ScoringSummary.Add($"{clock.Label()}  {scorer.Abbreviation} {kind}  ({home.Abbreviation} {homeScore}, {away.Abbreviation} {awayScore})");
        sink?.Publish(new ScoreChanged(scorer.Id, scorer.Name, kind, homeScore, awayScore));
    }

    private static string ScoreKind(DriveEndReason reason) => reason switch
    {
        DriveEndReason.Touchdown => "TD",
        DriveEndReason.FieldGoalGood => "FG",
        _ => "score",
    };
}
