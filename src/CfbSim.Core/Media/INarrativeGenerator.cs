namespace CfbSim.Core.Media;

/// <summary>
/// Turns a <see cref="NarrativeContext"/> into a <see cref="NewsArticle"/>. The v1
/// implementation is <see cref="TemplateNarrativeGenerator"/> (deterministic, instant).
/// A future <c>LlmNarrativeGenerator</c> (LLamaSharp + a bundled GGUF, on-device) drops
/// in behind this interface with no changes elsewhere — see docs/architecture.qmd.
/// </summary>
public interface INarrativeGenerator
{
    NewsArticle Generate(NarrativeContext context);
}
