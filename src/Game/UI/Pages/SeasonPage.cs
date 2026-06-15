using Godot;
using CfbSim.Core.Media;
using CfbSim.Core.Model;
using CfbSim.Core.Sim.Season;
using CfbSim.Core.Stats;

namespace CollegeFootballSimGoDot.UI;

/// <summary>
/// The Season hub. Two sub-tabs:
/// <list type="bullet">
/// <item><b>Overview</b> — a next-game spotlight, the week's daily strip (your games, by
/// day/slot), the national game of the week, headlines, and the three core lists
/// (your schedule, conference standings, Top 25).</item>
/// <item><b>Calendar</b> — the whole season's slate with a filter bar.</item>
/// </list>
/// All teams/games are clickable through to detail. Sub-tab + filter state live in fields so
/// they survive a <see cref="NavHost.Refresh"/> after a sim.
/// </summary>
public sealed class SeasonPage : Page
{
    private static readonly TimeSlot[] Slots = { TimeSlot.Morning, TimeSlot.Afternoon, TimeSlot.Evening };

    private string _sub = "Overview";
    private DateOnly _calMonth; // first-of-month currently shown in the Calendar tab

    public override string Title => "Season";

    public override Control Build()
    {
        var root = Ui.VBox(10);
        root.AddChild(SubTabs());

        Control content = _sub == "Calendar" ? CalendarView() : OverviewView();
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(content);
        return root;
    }

    private Control SubTabs()
    {
        var bar = Ui.HBox(6);
        foreach (string name in new[] { "Overview", "Calendar" })
        {
            string s = name;
            Button b = Ui.Button(s, () => { _sub = s; Nav.Refresh(); });
            b.CustomMinimumSize = new Vector2(120, 32);
            b.AddThemeColorOverride("font_color", _sub == s ? Ui.Accent : Ui.Text);
            bar.AddChild(b);
        }
        return bar;
    }

    // ---------------- Overview ----------------

    private Control OverviewView()
    {
        int week = Nav.Game.CurrentWeek;
        var v = Ui.VBox(10);

        var topRow = Ui.HBox(10);
        topRow.AddChild(SpotlightCard());
        var side = Ui.VBox(10);
        side.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        side.AddChild(GameOfWeekCard(week));
        side.AddChild(HeadlinesCard());
        topRow.AddChild(side);
        v.AddChild(topRow);

        v.AddChild(DailyStripCard(week));

        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(ScheduleCard());
        columns.AddChild(StandingsCard());
        columns.AddChild(Top25Card());
        v.AddChild(columns);
        return v;
    }

    private Control SpotlightCard()
    {
        int userId = Nav.Game.UserTeam.Id;
        Matchup? game = Nav.Game.UserNextGame();
        var v = Ui.VBox(6);

        if (game is null)
        {
            v.AddChild(Ui.Label("Regular season complete", 18, Ui.Text));
            v.AddChild(Ui.Label("Head to the playoff when you're ready.", 13, Ui.Muted));
            return Ui.Card("Next Game", v);
        }

        int oppId = game.Opponent(userId);
        string loc = game.HomeId == userId ? "vs" : "at";
        (int sw, int sl) = Nav.Game.SeriesVs(userId, oppId);
        string streak = Nav.Game.UserStreak();

        var headline = Ui.HBox(8);
        headline.AddChild(Ui.Label($"{loc}", 18, Ui.Muted));
        headline.AddChild(Ui.LinkButton(Nav.Game.RankedName(oppId), () => Nav.Push(new TeamProfilePage(oppId))));
        v.AddChild(headline);

        var when = Ui.HBox(6);
        when.AddChild(Ui.Label($"Week {game.Week}  ·  {SeasonCalendar.DateLabel(game.Kickoff)}  ·  {SeasonCalendar.TimeLabel(game.Kickoff)}", 13, Ui.Text));
        if (!string.IsNullOrEmpty(game.Network)) when.AddChild(Ui.Chip(game.Network, Ui.Accent, Ui.Bg));
        v.AddChild(when);

        string series = sw + sl == 0 ? "First meeting" : $"Series {sw}-{sl}";
        string ctx = string.IsNullOrEmpty(streak) ? series : $"{series}    ·    Streak {streak}";
        if (game.Rivalry && game.RivalryName is not null) ctx = $"{game.RivalryName}    ·    {ctx}";
        v.AddChild(Ui.Label(ctx, 12, Ui.Muted));
        return Ui.Card("Next Game", v);
    }

    private Control GameOfWeekCard(int week)
    {
        Matchup? g = Nav.Game.GameOfTheWeek(week);
        var v = Ui.VBox(4);
        if (g is null) { v.AddChild(Ui.Label("—", 13, Ui.Muted)); return Ui.Card("National Game of the Week", v); }

        var line = Ui.HBox(6);
        line.AddChild(Ui.LinkButton(Nav.Game.RankedName(g.AwayId), () => Nav.Push(new TeamProfilePage(g.AwayId))));
        line.AddChild(Ui.Label("at", 12, Ui.Muted));
        line.AddChild(Ui.LinkButton(Nav.Game.RankedName(g.HomeId), () => Nav.Push(new TeamProfilePage(g.HomeId))));
        v.AddChild(line);

        var when = Ui.HBox(6);
        when.AddChild(Ui.Label($"{SeasonCalendar.DateLabel(g.Kickoff)} · {SeasonCalendar.TimeLabel(g.Kickoff)}", 12, Ui.Muted));
        if (!string.IsNullOrEmpty(g.Network)) when.AddChild(Ui.Chip(g.Network, Ui.Accent2, Ui.Bg));
        v.AddChild(when);
        return Ui.Card("National Game of the Week", v);
    }

    private Control HeadlinesCard()
    {
        var v = Ui.VBox(3);
        var articles = Nav.Game.LatestHeadlines(3).ToList();
        if (articles.Count == 0)
            v.AddChild(Ui.Label("No coverage yet — sim a week.", 12, Ui.Muted));
        else
            foreach (NewsArticle a in articles)
                v.AddChild(Ui.LinkButton($"• {a.Headline}", () => Nav.Push(new MediaPage()), Ui.Text));
        return Ui.Card("Headlines", v);
    }

    private Control DailyStripCard(int week)
    {
        int userId = Nav.Game.UserTeam.Id;
        Matchup? userGame = Nav.Game.UserGameInWeek(week);
        DateOnly sunday = SeasonCalendar.SundayOfWeek(Nav.Game.Year, week);
        DateOnly today = Nav.Game.CurrentDate;

        var strip = Ui.HBox(8);
        for (int i = 0; i < 7; i++)
        {
            DateOnly date = sunday.AddDays(i);
            strip.AddChild(DayTile(date, userId, userGame, date == today));
        }
        return Ui.Card($"This Week — Week {week} of {Nav.Game.TotalWeeks}", strip);
    }

    private Control DayTile(DateOnly date, int userId, Matchup? userGame, bool isToday)
    {
        var v = Ui.VBox(3);
        var header = Ui.HBox(4);
        header.AddChild(Ui.Label($"{date.DayOfWeek.ToString()[..3].ToUpper()} {date:M/d}", 12, isToday ? Ui.Accent : Ui.Accent2));
        if (isToday) header.AddChild(Ui.Chip("TODAY", Ui.Accent, Ui.Bg));
        v.AddChild(header);

        bool mineToday = userGame is { Kickoff: var k } && k != default
                         && DateOnly.FromDateTime(userGame.Kickoff) == date;
        foreach (TimeSlot slot in Slots)
        {
            if (mineToday && userGame!.Slot == slot)
                v.AddChild(UserSlotRow(userId, userGame, slot));
            else
                v.AddChild(TrainingSlotRow(date, slot));
        }

        PanelContainer tile = Ui.Tile(isToday);
        tile.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        tile.CustomMinimumSize = new Vector2(0, 156);
        tile.AddChild(v);
        AttachSimToHere(tile, date);
        return tile;
    }

    private Control UserSlotRow(int userId, Matchup game, TimeSlot slot)
    {
        int oppId = game.Opponent(userId);
        string loc = game.HomeId == userId ? "vs" : "at";
        SeasonGameResult? res = Nav.Game.ResultFor(userId, game.Week);

        var row = Ui.VBox(1);
        row.AddChild(Ui.Label($"{SeasonCalendar.SlotLabel(slot)} · {SeasonCalendar.TimeLabel(game.Kickoff)}", 10, Ui.Accent));
        row.AddChild(Ui.LinkButton($"{loc} {Nav.Game.RankedName(oppId)}", () => Nav.Push(new TeamProfilePage(oppId))));

        var tags = Ui.HBox(4);
        if (!string.IsNullOrEmpty(game.Network)) tags.AddChild(Ui.Chip(game.Network, Ui.Accent, Ui.Bg));
        if (res is not null)
        {
            bool home = res.HomeId == userId;
            int us = home ? res.HomeScore : res.AwayScore;
            int them = home ? res.AwayScore : res.HomeScore;
            tags.AddChild(Ui.Label($"{(us >= them ? "W" : "L")} {us}-{them}", 12, us >= them ? Ui.Accent : Ui.Muted));
        }
        row.AddChild(tags);
        return row;
    }

    /// <summary>An assignable training slot (2K-style): a window's clock time + a dropdown of
    /// activities. Effects (temporary next-game prep with tradeoffs) are catalogued now and wired
    /// into the sim in a later pass. Only today-or-future slots are editable.</summary>
    private Control TrainingSlotRow(DateOnly date, TimeSlot slot)
    {
        bool editable = date >= Nav.Game.CurrentDate;
        TrainingActivity? act = Nav.Game.TrainingFor(date, slot);

        var row = Ui.VBox(1);
        row.AddChild(Ui.Label($"{SeasonCalendar.SlotLabel(slot)} · {SeasonCalendar.SlotTimeLabel(slot)}", 10, Ui.Muted));

        if (!editable)
        {
            row.AddChild(Ui.Label(act is null ? "—" : TrainingCatalog.For(act.Value).Name, 11,
                act is null ? Ui.Muted : Ui.Accent2));
            return row;
        }

        MenuButton mb = Ui.MenuButton(act is null ? "+ Training" : TrainingCatalog.For(act.Value).Name);
        mb.CustomMinimumSize = new Vector2(0, 26);
        mb.AddThemeFontSizeOverride("font_size", 11);
        if (act is not null)
        {
            TrainingOption o = TrainingCatalog.For(act.Value);
            mb.TooltipText = $"{o.Boost}\nTradeoff: {o.Tradeoff}";
        }
        PopupMenu pm = mb.GetPopup();
        pm.AddItem("— None —", -1);
        for (int i = 0; i < TrainingCatalog.All.Count; i++)
            pm.AddItem(TrainingCatalog.All[i].Name, i);
        pm.IdPressed += id =>
        {
            TrainingActivity? choice = id < 0 ? null : TrainingCatalog.All[(int)id].Activity;
            Nav.Game.SetTraining(date, slot, choice);
            Nav.Refresh();
        };
        row.AddChild(mb);
        return row;
    }

    /// <summary>Right-click a day to "sim to here" (routed through the shell's confirm + transition).</summary>
    private void AttachSimToHere(Control control, DateOnly date)
    {
        control.MouseFilter = Control.MouseFilterEnum.Stop;
        control.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }
                && date > Nav.Game.CurrentDate)
                Nav.RequestSim($"to {date:ddd, MMM d}", () => Nav.Game.SimToDate(date));
        };
    }

    // ---------------- The three core lists ----------------

    private Control ScheduleCard()
    {
        int userId = Nav.Game.UserTeam.Id;
        (Control panel, VBoxContainer body) = Ui.ScrollBox("Your Schedule");
        foreach (Matchup m in Nav.Game.UserSchedule())
        {
            int oppId = m.Opponent(userId);
            string loc = m.HomeId == userId ? "vs" : "at";
            SeasonGameResult? res = Nav.Game.ResultFor(userId, m.Week);

            var row = Ui.HBox(6);
            row.AddChild(Ui.Label($"Wk{m.Week,2} {loc}", 13, Ui.Muted));
            row.AddChild(Ui.LinkButton(Nav.Game.RankedName(oppId), () => Nav.Push(new TeamProfilePage(oppId))));
            if (res is null)
            {
                string slot = m.Kickoff == default ? "—" : $"{SeasonCalendar.DateLabel(m.Kickoff)} {(string.IsNullOrEmpty(m.Network) ? "" : "· " + m.Network)}";
                row.AddChild(Ui.Label(slot, 12, Ui.Muted));
            }
            else
            {
                bool home = res.HomeId == userId;
                int us = home ? res.HomeScore : res.AwayScore;
                int them = home ? res.AwayScore : res.HomeScore;
                string text = $"{(us >= them ? "W" : "L")} {us}-{them}";
                BoxScore? box = Nav.Game.SeasonBox(res.Week, res.HomeId, res.AwayId);
                if (box is not null)
                    row.AddChild(Ui.LinkButton(text, () => Nav.Push(new BoxScorePage(box)), us >= them ? Ui.Accent : Ui.Muted));
                else
                    row.AddChild(Ui.Label(text, 13));
            }
            body.AddChild(row);
        }
        return panel;
    }

    private Control StandingsCard()
    {
        int userId = Nav.Game.UserTeam.Id;
        (Control panel, VBoxContainer body) = Ui.ScrollBox("Conference Standings");
        int rank = 1;
        foreach (TeamRecord r in Nav.Game.UserConferenceStandings())
        {
            Team t = Nav.Game.Team(r.TeamId);
            string mark = r.TeamId == userId ? "▸" : " ";
            string streak = Nav.Game.StreakOf(t.Id);
            string tail = streak.Length == 0 ? "" : $"  {streak}";
            body.AddChild(Ui.RowButton(
                $"{mark}{rank,2}. {Nav.Game.RankedName(t.Id),-20} {r.Wins}-{r.Losses} ({r.ConfWins}-{r.ConfLosses}){tail}",
                () => Nav.Push(new TeamProfilePage(t.Id))));
            rank++;
        }
        return panel;
    }

    private Control Top25Card()
    {
        (Control panel, VBoxContainer body) = Ui.ScrollBox("Top 25");
        foreach (RankedTeam rt in Nav.Game.CurrentRankings().Take(25))
        {
            Team t = Nav.Game.Team(rt.TeamId);
            Conference? conf = Nav.Game.ConferenceOf(t.Id);
            string ca = conf is null ? "" : conf.Abbreviation;
            body.AddChild(Ui.RowButton(
                $"{rt.Rank,2}. {t.Name,-18} {ca,-5} {rt.Record.Wins}-{rt.Record.Losses}",
                () => Nav.Push(new TeamProfilePage(t.Id))));
        }
        return panel;
    }

    // ---------------- Calendar ----------------

    private DateOnly FirstMonth() { DateOnly d = SeasonCalendar.SundayOfWeek(Nav.Game.Year, 1); return new DateOnly(d.Year, d.Month, 1); }
    private DateOnly LastMonth() { DateOnly d = SeasonCalendar.SaturdayOfWeek(Nav.Game.Year, Nav.Game.TotalWeeks); return new DateOnly(d.Year, d.Month, 1); }

    private Control CalendarView()
    {
        if (_calMonth == default)
        {
            DateOnly cur = Nav.Game.CurrentDate;
            _calMonth = new DateOnly(cur.Year, cur.Month, 1);
            if (_calMonth < FirstMonth()) _calMonth = FirstMonth();
            if (_calMonth > LastMonth()) _calMonth = LastMonth();
        }

        var v = Ui.VBox(10);
        v.AddChild(MonthNavBar());

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(MonthGrid(_calMonth));
        v.AddChild(scroll);
        return v;
    }

    private Control MonthNavBar()
    {
        var bar = Ui.HBox(10);
        Button prev = Ui.Button("◀ Prev", () => { if (_calMonth > FirstMonth()) { _calMonth = _calMonth.AddMonths(-1); Nav.Refresh(); } });
        Button next = Ui.Button("Next ▶", () => { if (_calMonth < LastMonth()) { _calMonth = _calMonth.AddMonths(1); Nav.Refresh(); } });
        prev.CustomMinimumSize = new Vector2(90, 30);
        next.CustomMinimumSize = new Vector2(90, 30);
        prev.Disabled = _calMonth <= FirstMonth();
        next.Disabled = _calMonth >= LastMonth();
        bar.AddChild(prev);
        Label title = Ui.Label($"{_calMonth:MMMM yyyy}", 18, Ui.Accent2);
        title.CustomMinimumSize = new Vector2(200, 0);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        bar.AddChild(title);
        bar.AddChild(next);
        bar.AddChild(Ui.Label("Right-click a day to sim to it.", 12, Ui.Muted));
        return bar;
    }

    private Control MonthGrid(DateOnly monthStart)
    {
        var v = Ui.VBox(6);
        v.AddChild(Ui.Label($"{monthStart:MMMM yyyy}", 16, Ui.Accent2));

        var grid = new GridContainer { Columns = 7 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        foreach (string d in new[] { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" })
        {
            Label h = Ui.Label(d, 11, Ui.Muted);
            h.HorizontalAlignment = HorizontalAlignment.Center;
            h.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            grid.AddChild(h);
        }

        int lead = (int)new DateOnly(monthStart.Year, monthStart.Month, 1).DayOfWeek; // Sunday = 0
        for (int i = 0; i < lead; i++)
            grid.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        int days = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        DateOnly today = Nav.Game.CurrentDate;
        for (int day = 1; day <= days; day++)
        {
            var date = new DateOnly(monthStart.Year, monthStart.Month, day);
            grid.AddChild(DayCell(date, date == today));
        }
        v.AddChild(grid);
        return v;
    }

    private Control DayCell(DateOnly date, bool isToday)
    {
        var v = Ui.VBox(2);
        v.AddChild(Ui.Label(date.Day.ToString(), 11, isToday ? Ui.Accent : Ui.Muted));

        // Show the user's games and any ranked matchups (the noise-free default — no filter bar).
        var games = Nav.Game.GamesOn(date).Where(CalendarShows).ToList();
        for (int i = 0; i < games.Count; i++)
        {
            if (i >= 4) { v.AddChild(Ui.Label($"+{games.Count - i} more", 10, Ui.Muted)); break; }
            v.AddChild(CalCellGame(games[i]));
        }

        PanelContainer cell = Ui.Tile(isToday);
        cell.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cell.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        cell.CustomMinimumSize = new Vector2(0, 86);
        cell.AddChild(v);
        AttachSimToHere(cell, date);
        return cell;
    }

    private bool CalendarShows(Matchup m)
    {
        int userId = Nav.Game.UserTeam.Id;
        return m.Involves(userId) || Nav.Game.RankOf(m.HomeId) > 0 || Nav.Game.RankOf(m.AwayId) > 0;
    }

    private Control CalCellGame(Matchup m)
    {
        int userId = Nav.Game.UserTeam.Id;
        string aw = Nav.Game.Team(m.AwayId).Abbreviation;
        string hm = Nav.Game.Team(m.HomeId).Abbreviation;
        SeasonGameResult? res = Nav.Game.ResultFor(m.HomeId, m.Week);
        string text = res is null ? $"{aw} @ {hm}" : $"{aw} {res.AwayScore}-{res.HomeScore} {hm}";
        Color c = m.Involves(userId) ? Ui.Accent : Ui.Text;

        Action open;
        if (res is not null && Nav.Game.SeasonBox(res.Week, res.HomeId, res.AwayId) is BoxScore box)
            open = () => Nav.Push(new BoxScorePage(box));
        else
            open = () => Nav.Push(new TeamProfilePage(m.HomeId));

        Button b = Ui.LinkButton(text, open, c);
        b.AddThemeFontSizeOverride("font_size", 10);
        b.Alignment = HorizontalAlignment.Left;
        return b;
    }

}
