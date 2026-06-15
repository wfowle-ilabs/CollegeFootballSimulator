namespace CfbSim.Core.Events;

/// <summary>
/// Base for typed domain events the simulation emits (see docs/architecture.qmd —
/// Event-Driven Architecture). Events are facts, not commands; consumers subscribe.
/// </summary>
public abstract record DomainEvent;

/// <summary>A game has finished. Carries the full result for downstream consumers
/// (stats, media narratives, standings).</summary>
public sealed record GameConcluded(
    int HomeTeamId, int AwayTeamId,
    string HomeName, string AwayName,
    int HomeScore, int AwayScore) : DomainEvent;

/// <summary>A score occurred during a game (useful for live consumers).</summary>
public sealed record ScoreChanged(
    int TeamId, string TeamName, string Kind, int HomeScore, int AwayScore) : DomainEvent;

/// <summary>A week of the season finished (the trigger for between-week consumers like media).</summary>
public sealed record WeekAdvanced(int Year, int Week) : DomainEvent;

/// <summary>The regular season finished.</summary>
public sealed record SeasonConcluded(int Year) : DomainEvent;

/// <summary>A national champion has been crowned (end of the postseason).</summary>
public sealed record NationalChampionCrowned(int Year, int TeamId, string TeamName) : DomainEvent;

/// <summary>Anything that wants to receive domain events.</summary>
public interface IEventSink
{
    void Publish(DomainEvent domainEvent);
}

/// <summary>
/// Engine-free event hub. The Godot-side autoload `EventBus` can wrap/forward this
/// to UI signals later; the sim only ever talks to <see cref="IEventSink"/>.
/// </summary>
public sealed class EventBus : IEventSink
{
    private readonly List<Action<DomainEvent>> _handlers = new();

    public void Subscribe(Action<DomainEvent> handler) => _handlers.Add(handler);

    public void Subscribe<T>(Action<T> handler) where T : DomainEvent
        => _handlers.Add(e => { if (e is T t) handler(t); });

    public void Publish(DomainEvent domainEvent)
    {
        foreach (Action<DomainEvent> handler in _handlers)
            handler(domainEvent);
    }
}
