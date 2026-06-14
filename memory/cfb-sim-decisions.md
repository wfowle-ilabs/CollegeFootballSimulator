---
name: cfb-sim-decisions
description: Core architecture decisions for the College Football Simulator (Godot/C# project)
metadata:
  type: project
---

College Football Simulator built in **Godot 4.6.3** using **C#/.NET (not GDScript)**.

v1 design decisions (locked 2026-06-14):
- **Sim granularity:** Play-by-play (every down simulated).
- **Resolution system:** BG3-style **d20 + attribute modifier vs Difficulty Class (DC)**; nat 20 = crit, nat 1 = blunder. No traditional "overall" rating — attributes + skills like a BG3 character sheet.
- **v1 scope:** In-season only — start save, pick a team in a conference, sim full season including the full CFP slate. FBS (FBS-IAA) only for v1.
- **User control:** Sim & watch (set gameplan/depth chart pre-game, watch the sim resolve).
- **Architecture style:** event-driven — sim emits typed domain events on an `EventBus`; UI/stats/media subscribe. Side-effect consumers never mutate sim state (keeps determinism). Consumers can run async on worker threads.
- **Media subsystem (v1):** in-game ESPN-style Media menu (weekly recaps, Featured tab, any-team lookup). Generated between weeks on a background thread. Pluggable `INarrativeGenerator`: `TemplateNarrativeGenerator` (deterministic baseline ships in v1) and `LlmNarrativeGenerator` (on-device ~7B GGUF via **LLamaSharp**, drops in v1.x once validated). Coverage **tiered** (featured/ranked/upset/user-team = full article; rest = templated recap). Articles persist in a **sidecar media cache** (`user://saves/<slot>.media`), not the core SaveGame. LLM is strictly downstream — never feeds the sim.
- **Future expansion:** all levels of college football down to community colleges; recruiting, transfer portal, coaching carousel are post-v1.

Design docs are now a **Quarto website** in `docs/` (sidebar + search, light/dark theme, auto section numbering + `@sec-` cross-refs). Canonical pages: `docs/architecture.qmd`, `docs/goals.qmd`, `docs/decisions.qmd`, `docs/index.qmd`. Config in `docs/_quarto.yml`; render with `quarto render` (output to `docs/_site/`, gitignored). Auto-publishes to GitHub Pages via `.github/workflows/publish-docs.yml` (gh-pages branch; Pages source must be enabled in repo settings after first run).
Use context7 ('/websites/godotengine_en_4_6') for Godot 4.6 API and ('/quarto-dev/quarto-web') for Quarto.
