using Godot;
using CfbSim.Core.Model;
using CfbSim.Core.Sim.Season;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Season dashboard: your schedule, conference standings, and the Top 25 — all
/// teams/games clickable through to detail.</summary>
public sealed class SeasonPage : Page
{
    public override string Title => "Season";

    public override Control Build()
    {
        var columns = Ui.HBox(16);
        columns.AddChild(ScheduleCard());
        columns.AddChild(StandingsCard());
        columns.AddChild(Top25Card());
        return columns;
    }

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
                row.AddChild(Ui.Label("—", 13, Ui.Muted));
            else
            {
                bool home = res.HomeId == userId;
                int us = home ? res.HomeScore : res.AwayScore;
                int them = home ? res.AwayScore : res.HomeScore;
                string text = $"{(us >= them ? "W" : "L")} {us}-{them}";
                var box = Nav.Game.SeasonBox(res.Week, res.HomeId, res.AwayId);
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
            body.AddChild(Ui.RowButton(
                $"{mark}{rank,2}. {Nav.Game.RankedName(t.Id)}   {r.Wins}-{r.Losses} ({r.ConfWins}-{r.ConfLosses})",
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
            body.AddChild(Ui.RowButton(
                $"{rt.Rank,2}. {t.Name,-18} {rt.Record.Wins}-{rt.Record.Losses}",
                () => Nav.Push(new TeamProfilePage(t.Id))));
        }
        return panel;
    }
}
