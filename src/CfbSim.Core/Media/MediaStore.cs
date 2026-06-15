namespace CfbSim.Core.Media;

/// <summary>
/// The per-save media cache (a sidecar). Holds generated articles and answers the
/// queries the Media menu needs: by week, featured, and team lookup.
/// </summary>
public sealed class MediaStore
{
    public List<NewsArticle> Articles { get; init; } = new();
    public int NextId { get; set; } = 1;

    public NewsArticle Add(NewsArticle article)
    {
        article.Id = NextId++;
        Articles.Add(article);
        return article;
    }

    public IEnumerable<int> WeeksWithCoverage(int year)
        => Articles.Where(a => a.Year == year).Select(a => a.Week).Distinct().OrderBy(w => w);

    /// <summary>A week's coverage: full (featured) articles first, then short recaps.</summary>
    public IEnumerable<NewsArticle> ForWeek(int year, int week)
        => Articles.Where(a => a.Year == year && a.Week == week)
                   .OrderByDescending(a => a.Full).ThenBy(a => a.Id);

    public IEnumerable<NewsArticle> Featured(int year, int week)
        => ForWeek(year, week).Where(a => a.Full);

    public IEnumerable<NewsArticle> ForTeam(int teamId)
        => Articles.Where(a => a.TeamIds.Contains(teamId)).OrderByDescending(a => a.Id);
}
