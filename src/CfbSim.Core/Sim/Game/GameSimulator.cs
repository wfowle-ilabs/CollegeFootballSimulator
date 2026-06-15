using CfbSim.Core.Events;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Play;
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
        GameTimeline timeline = result.Timeline;

        int homeScore = 0, awayScore = 0;
        bool homeReceivesFirst = rng.NextInt(0, 1) == 0;
        bool offenseIsHome = homeReceivesFirst;
        int ballOn = 25;
        bool secondHalfStarted = false;
        string? pendingMarker = null;

        while (true)
        {
            Team offense = offenseIsHome ? home : away;
            Team defense = offenseIsHome ? away : home;
            int offScore = offenseIsHome ? homeScore : awayScore;
            int defScore = offenseIsHome ? awayScore : homeScore;

            Drive drive = StartDrive(timeline, offense, ballOn, clock, ref pendingMarker);
            DriveResult dr = DriveSimulator.Run(rng, offense, defense, box, clock, drive,
                ballOn, offScore, defScore, passBias, driveInFirstHalf: !secondHalfStarted, untimed: false);
            drive.Result = dr.Reason;
            drive.Points = dr.OffensePoints;

            // Award points.
            if (dr.OffensePoints > 0)
            {
                if (offenseIsHome) homeScore += dr.OffensePoints; else awayScore += dr.OffensePoints;
                AddScore(drive, sink, offense, ScoreKind(dr.Reason), homeScore, awayScore, clock, home, away);
            }
            if (dr.SafetyConceded)
            {
                if (offenseIsHome) awayScore += 2; else homeScore += 2;
                AddScore(drive, sink, defense, "Safety", homeScore, awayScore, clock, home, away);
            }
            if (dr.DefenseReturnTd)
            {
                if (offenseIsHome) awayScore += 7; else homeScore += 7;
                AddScore(drive, sink, defense, "Punt return TD", homeScore, awayScore, clock, home, away);
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
                pendingMarker = "== HALFTIME ==";
                continue;
            }

            if (dr.OffenseRetains)
            {
                // Recovered punt muff — same offense keeps the ball, no flip, no kickoff.
                ballOn = dr.NextBallOn;
                continue;
            }
            if (dr.DefenseReturnTd)
            {
                // Defense took the punt back; the offense now receives the ensuing kickoff (no flip).
                ballOn = 25;
                continue;
            }

            // Possession flips to the other team; a score is followed by a kickoff return.
            offenseIsHome = !offenseIsHome;
            Team receiving = offenseIsHome ? home : away;
            if (dr.Reason == DriveEndReason.Safety)
            {
                ballOn = 35;
            }
            else if (dr.OffensePoints > 0)
            {
                KickoffResult ko = SpecialTeamsResolver.Kickoff(rng, receiving);
                if (ko.ReturnTouchdown)
                {
                    if (offenseIsHome) homeScore += 7; else awayScore += 7;
                    AddScore(drive, sink, receiving, "Kickoff return TD", homeScore, awayScore, clock, home, away);
                    box.Home.Points = homeScore;
                    box.Away.Points = awayScore;
                    offenseIsHome = !offenseIsHome; // the team that just scored receives the next kickoff
                    ballOn = 25;
                }
                else
                {
                    ballOn = ko.Spot;
                }
            }
            else
            {
                ballOn = dr.NextBallOn == -1 ? 25 : dr.NextBallOn;
            }
        }

        // Overtime: alternating possessions from the opponent's 25 until someone leads.
        int ot = 0;
        while (homeScore == awayScore && ot < 6)
        {
            ot++;
            pendingMarker = $"== OVERTIME {ot} ==";
            Drive hDrive = StartDrive(timeline, home, 75, clock, ref pendingMarker);
            DriveResult h = DriveSimulator.Run(rng, home, away, box, clock, hDrive, 75, homeScore, awayScore, passBias, false, untimed: true);
            hDrive.Result = h.Reason; hDrive.Points = h.OffensePoints;
            homeScore += h.OffensePoints; if (h.SafetyConceded) awayScore += 2;

            Drive aDrive = StartDrive(timeline, away, 75, clock, ref pendingMarker);
            DriveResult a = DriveSimulator.Run(rng, away, home, box, clock, aDrive, 75, awayScore, homeScore, passBias, false, untimed: true);
            aDrive.Result = a.Reason; aDrive.Points = a.OffensePoints;
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

    /// <summary>Open a new drive on the timeline, attaching any pending marker (halftime/OT).</summary>
    private static Drive StartDrive(GameTimeline timeline, Team offense, int ballOn, GameClock clock, ref string? pendingMarker)
    {
        var drive = new Drive
        {
            OffenseId = offense.Id,
            OffenseAbbr = offense.Abbreviation,
            StartBallOn = ballOn,
            StartClockLabel = clock.Label(),
            PrecedingMarker = pendingMarker,
        };
        pendingMarker = null;
        timeline.Drives.Add(drive);
        return drive;
    }

    /// <summary>Record a scoring beat as a <see cref="SegmentKind.Score"/> segment on the drive
    /// (the scoring summary derives from these) and publish the score event.</summary>
    private static void AddScore(Drive drive, IEventSink? sink, Team scorer, string kind,
        int homeScore, int awayScore, GameClock clock, Team home, Team away)
    {
        drive.Segments.Add(new GameSegment
        {
            Kind = SegmentKind.Score,
            ClockLabel = clock.Label(),
            ClockElapsed = clock.Elapsed,
            Text = $"{clock.Label()}  {scorer.Abbreviation} {kind}  ({home.Abbreviation} {homeScore}, {away.Abbreviation} {awayScore})",
            ScoreKind = kind,
            HomeScore = homeScore,
            AwayScore = awayScore,
        });
        sink?.Publish(new ScoreChanged(scorer.Id, scorer.Name, kind, homeScore, awayScore));
    }

    private static string ScoreKind(DriveEndReason reason) => reason switch
    {
        DriveEndReason.Touchdown => "TD",
        DriveEndReason.FieldGoalGood => "FG",
        _ => "score",
    };
}
