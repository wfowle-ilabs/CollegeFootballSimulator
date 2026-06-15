using Godot;
using CfbSim.Core.Model;
using CfbSim.Core.Sim.Season;

namespace CollegeFootballSimGoDot.UI;

/// <summary>A conference's standings; teams are clickable through to their profiles.</summary>
public sealed class ConferencePage(int conferenceId) : Page
{
    public override string Title => Nav.Game.ConferenceById(conferenceId)?.Abbreviation ?? "Conference";

    public override Control Build()
    {
        Conference? conf = Nav.Game.ConferenceById(conferenceId);
        var root = Ui.VBox(10);
        if (conf is null) { root.AddChild(Ui.Label("Unknown conference.", 14)); return root; }

        root.AddChild(Ui.Label($"{conf.Name}  ({(conf.IsPower ? "Power 4" : "Group of 5")})", 22, Ui.Accent));

        (Control panel, VBoxContainer body) = Ui.ScrollBox("Standings  ·  click a team");
        int rank = 1;
        foreach (TeamRecord r in Nav.Game.ConferenceStandings(conf))
        {
            Team t = Nav.Game.Team(r.TeamId);
            body.AddChild(Ui.RowButton(
                $"{rank,2}. {Nav.Game.RankedName(t.Id),-24} {r.Wins}-{r.Losses}  ({r.ConfWins}-{r.ConfLosses})",
                () => Nav.Push(new TeamProfilePage(t.Id))));
            rank++;
        }
        root.AddChild(panel);
        return root;
    }
}
