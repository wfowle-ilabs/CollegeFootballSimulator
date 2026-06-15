using CfbSim.Core.Model;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Sim.Season;

/// <summary>The three daily windows a game (or training session) can occupy.</summary>
public enum TimeSlot { Morning, Afternoon, Evening }

/// <summary>
/// Maps the abstract week grid onto a real calendar. Week 1 opens on the last Saturday of
/// August; each subsequent week is the next Saturday. Games sit on Thu/Fri/Sat in one of
/// three windows. Pure date math — no RNG, fully deterministic from (year, week, day, slot).
/// </summary>
public static class SeasonCalendar
{
    /// <summary>The Saturday of regular-season week 1 — the last Saturday of August.</summary>
    public static DateOnly OpeningSaturday(int year)
    {
        var d = new DateOnly(year, 8, 31);
        while (d.DayOfWeek != DayOfWeek.Saturday) d = d.AddDays(-1);
        return d;
    }

    /// <summary>The Saturday anchoring a given regular-season week.</summary>
    public static DateOnly SaturdayOfWeek(int year, int week) => OpeningSaturday(year).AddDays(7 * (week - 1));

    /// <summary>The Sunday that opens week 1 — the start of the season's day cursor.</summary>
    public static DateOnly OpeningSunday(int year) => OpeningSaturday(year).AddDays(-6);

    /// <summary>The Sunday that opens a given week (its calendar week runs Sun..Sat).</summary>
    public static DateOnly SundayOfWeek(int year, int week) => SaturdayOfWeek(year, week).AddDays(-6);

    /// <summary>The regular-season week a calendar date falls in (1-based; clamped at the low end).</summary>
    public static int WeekOf(int year, DateOnly date)
    {
        DateOnly sun = OpeningSunday(year);
        if (date < sun) return 1;
        return (date.DayNumber - sun.DayNumber) / 7 + 1;
    }

    /// <summary>The fixed kickoff clock time (Eastern) for each daily window.</summary>
    public static TimeOnly SlotTime(TimeSlot slot) => slot switch
    {
        TimeSlot.Morning => new TimeOnly(12, 0),    // noon ET early window
        TimeSlot.Afternoon => new TimeOnly(15, 30),
        _ => new TimeOnly(19, 30),                  // evening / primetime
    };

    /// <summary>Kickoff datetime for a game on <paramref name="day"/> in <paramref name="slot"/>.</summary>
    public static DateTime Kickoff(int year, int week, DayOfWeek day, TimeSlot slot)
    {
        DateOnly sat = SaturdayOfWeek(year, week);
        int offset = day switch { DayOfWeek.Thursday => -2, DayOfWeek.Friday => -1, _ => 0 };
        return sat.AddDays(offset).ToDateTime(SlotTime(slot));
    }

    public static string SlotLabel(TimeSlot slot) => slot switch
    {
        TimeSlot.Morning => "Morning",
        TimeSlot.Afternoon => "Afternoon",
        _ => "Evening",
    };

    /// <summary>"7:30 PM ET" style clock label for a window.</summary>
    public static string SlotTimeLabel(TimeSlot slot) => SlotTime(slot).ToString("h:mm tt") + " ET";

    /// <summary>"3:30 PM ET" style kickoff label.</summary>
    public static string TimeLabel(DateTime kickoff)
        => kickoff == default ? "TBD" : kickoff.ToString("h:mm tt") + " ET";

    /// <summary>"Sat, Sep 5" style date label.</summary>
    public static string DateLabel(DateTime kickoff)
        => kickoff == default ? "TBD" : kickoff.ToString("ddd, MMM d");
}

/// <summary>
/// Assigns each game a day, kickoff window, and broadcast network — deterministically, off
/// the seeded RNG and the game's "appeal" (combined prestige + a rivalry bump). The marquee
/// game of each week lands Saturday primetime on a flagship; a couple flex to Thu/Fri night;
/// the rest fill the Saturday noon/afternoon windows on conference-appropriate networks.
/// Broadcast data is presentation flavor — it never feeds the sim, preserving determinism.
/// </summary>
public static class BroadcastScheduler
{
    // Conference network pools, flagship-first. Power conferences front-load broadcast networks.
    private static readonly Dictionary<string, string[]> ConfNetworks = new()
    {
        ["SEC"] = new[] { "ABC", "CBS", "ESPN", "SEC Network" },
        ["B1G"] = new[] { "FOX", "NBC", "CBS", "BTN" },
        ["ACC"] = new[] { "ESPN", "ABC", "ACC Network", "ESPN2" },
        ["B12"] = new[] { "FOX", "ESPN", "ABC", "FS1" },
        ["AAC"] = new[] { "ESPN", "ESPN2", "ESPNU" },
        ["MWC"] = new[] { "FS1", "CBSSN", "ESPN2" },
        ["PAC"] = new[] { "The CW", "FS1", "CBSSN" },
        ["CUSA"] = new[] { "CBSSN", "ESPN+" },
        ["MAC"] = new[] { "ESPN2", "ESPNU", "CBSSN" },
        ["SBC"] = new[] { "ESPNU", "ESPN+" },
    };
    private static readonly string[] Fallback = { "ESPN+", "ESPNU" };

    public static void Assign(IRng rng, League league, Schedule schedule)
    {
        var teams = league.AllTeams.ToDictionary(t => t.Id);
        var confAbbr = league.Conferences.ToDictionary(c => c.Id, c => c.Abbreviation);

        int Appeal(Matchup m)
        {
            int home = teams.TryGetValue(m.HomeId, out Team? h) ? h.Prestige : 40;
            int away = teams.TryGetValue(m.AwayId, out Team? a) ? a.Prestige : 40;
            return home + away + (m.Rivalry ? 30 : 0);
        }

        for (int week = 1; week <= schedule.Weeks; week++)
        {
            // Highest-appeal games first; stable tiebreak keeps it deterministic.
            var games = schedule.InWeek(week)
                .OrderByDescending(Appeal).ThenBy(m => m.HomeId).ThenBy(m => m.AwayId)
                .ToList();
            int count = games.Count;
            if (count == 0) continue;

            int primetimeCut = Math.Max(1, (int)Math.Ceiling(count * 0.18));
            int afternoonCut = (int)Math.Ceiling(count * 0.55);

            for (int i = 0; i < count; i++)
            {
                Matchup m = games[i];
                TimeSlot slot = i < primetimeCut ? TimeSlot.Evening
                              : i < afternoonCut ? TimeSlot.Afternoon
                              : TimeSlot.Morning;
                DayOfWeek day = DayOfWeek.Saturday;

                // Weekday texture: flex a marquee game to Friday, sometimes Thursday night.
                if (i == 1) { day = DayOfWeek.Friday; slot = TimeSlot.Evening; }
                else if (i == 2 && rng.NextInt(0, 1) == 0) { day = DayOfWeek.Thursday; slot = TimeSlot.Evening; }

                m.Kickoff = SeasonCalendar.Kickoff(schedule.Year, week, day, slot);

                // Broadcast pool comes from the higher-prestige side's conference.
                int ownerId = Appeal(m) >= 0 && teams.TryGetValue(m.HomeId, out Team? hh) &&
                              teams.TryGetValue(m.AwayId, out Team? aa) && aa.Prestige > hh.Prestige
                              ? m.AwayId : m.HomeId;
                int ownerConf = teams.TryGetValue(ownerId, out Team? owner) ? owner.ConferenceId : 0;
                string[] pool = ConfNetworks.GetValueOrDefault(confAbbr.GetValueOrDefault(ownerConf, ""), Fallback);
                m.Network = PickNetwork(rng, pool, slot, i, primetimeCut);
            }
        }
    }

    private static string PickNetwork(IRng rng, string[] pool, TimeSlot slot, int rank, int primetimeCut)
    {
        // Flagship windows (front of the pool) go to the marquee/primetime games; cable to the rest.
        int idx = slot switch
        {
            TimeSlot.Evening => rank < primetimeCut ? 0 : Math.Min(1, pool.Length - 1),
            TimeSlot.Afternoon => Math.Min(1, pool.Length - 1),
            _ => Math.Min(rng.NextInt(0, 1) + 1, pool.Length - 1),
        };
        return pool[Math.Clamp(idx, 0, pool.Length - 1)];
    }
}
