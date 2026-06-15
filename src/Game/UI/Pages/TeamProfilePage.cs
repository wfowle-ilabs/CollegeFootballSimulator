using System.Text;
using Godot;
using CfbSim.Core.Model;
using CfbSim.Core.Sim.Season;

namespace CollegeFootballSimGoDot.UI;

/// <summary>A team's profile: overview (record, rank, home/away, next game), schedule
/// (clickable games + opponents), and links to its roster and conference.</summary>
public sealed class TeamProfilePage(int teamId) : Page
{
    public override string Title => Nav.Game.Team(teamId).Abbreviation;

    public override Control Build()
    {
        Team team = Nav.Game.Team(teamId);
        Conference? conf = Nav.Game.ConferenceOf(teamId);
        var root = Ui.VBox(12);

        var head = Ui.HBox(10);
        int rank = Nav.Game.RankOf(teamId);
        string rankStr = rank > 0 ? $"#{rank} " : "";
        head.AddChild(Ui.Label($"{rankStr}{team.Name}", 24, Ui.Accent));
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("View Roster", () => Nav.Push(new RosterPage(teamId))));
        if (teamId == Nav.Game.UserTeam.Id)
            head.AddChild(Ui.Button("Depth Chart", () => Nav.Push(new DepthChartPage())));
        if (conf is not null)
            head.AddChild(Ui.LinkButton(conf.Abbreviation, () => Nav.Push(new ConferencePage(conf.Id))));
        root.AddChild(head);

        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(Ui.Card("Overview", Overview(team, conf)));
        columns.AddChild(ScheduleCard(team));
        root.AddChild(columns);
        return root;
    }

    private Control Overview(Team team, Conference? conf)
    {
        TeamRecord r = Nav.Game.RecordOf(teamId);
        (int hw, int hl, int aw, int al) = Nav.Game.HomeAwayRecord(teamId);
        Matchup? next = Nav.Game.NextOpponent(teamId);

        var sb = new StringBuilder();
        sb.AppendLine($"Record:     {r.Wins}-{r.Losses}");
        sb.AppendLine($"Conference: {r.ConfWins}-{r.ConfLosses}  ({conf?.Abbreviation ?? "Independent"})");
        sb.AppendLine($"Home:       {hw}-{hl}");
        sb.AppendLine($"Away:       {aw}-{al}");
        sb.AppendLine($"Points:     {r.PointsFor} for / {r.PointsAgainst} against");
        sb.AppendLine($"Prestige:   {team.Prestige}");
        sb.AppendLine();
        if (next is not null)
        {
            int oppId = next.Opponent(teamId);
            string loc = next.HomeId == teamId ? "vs" : "at";
            sb.AppendLine($"Next: Wk{next.Week} {loc} {Nav.Game.Team(oppId).Name}");
        }
        else sb.AppendLine("Next: — (season complete)");

        var label = Ui.Label(sb.ToString(), 13);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(label);
        return scroll;
    }

    private Control ScheduleCard(Team team)
    {
        (Control panel, VBoxContainer body) = Ui.ScrollBox("Schedule");
        foreach (Matchup m in Nav.Game.ScheduleOf(teamId))
        {
            int oppId = m.Opponent(teamId);
            string loc = m.HomeId == teamId ? "vs" : "at";
            SeasonGameResult? res = Nav.Game.ResultFor(teamId, m.Week);

            var row = Ui.HBox(6);
            row.AddChild(Ui.Label($"Wk{m.Week,2} {loc}", 13, Ui.Muted));
            row.AddChild(Ui.LinkButton(Nav.Game.RankedName(oppId), () => Nav.Push(new TeamProfilePage(oppId))));

            if (res is null)
                row.AddChild(Ui.Label("—", 13, Ui.Muted));
            else
            {
                bool home = res.HomeId == teamId;
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
}
