# Phase 16: Integration & Experience Loop - Context

**Gathered:** 2026-02-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire all subsystems (Aya, chat bots, narrative director, scene transition) into a single cohesive 10-minute livestream experience. Cross-system context injection ensures coherence -- Aya knows what bots said, bots know what Aya said, and no subsystem contradicts another. The experience runs start-to-finish without manual intervention beyond optional push-to-talk.

</domain>

<decisions>
## Implementation Decisions

### Startup & wiring sequence
- LivestreamController is a single MonoBehaviour with explicit `[SerializeField]` references to all subsystems -- no runtime discovery
- Subsystems initialize in parallel, each signals readiness. LivestreamController waits for all before starting the experience
- If a subsystem fails (e.g., Gemini connection timeout), the experience degrades gracefully -- start with what's available, log warnings, don't block
- Brief loading state while subsystems connect, then a "going live" transition moment before Aya starts talking

### Context injection & coherence
- Bot→Aya flow uses the existing AyaChecksChat scene system (StringBuilder director notes). LivestreamController just ensures scenes run -- no new injection path
- Aya→Bot flow: include last 2-3 Aya transcript turns in the Gemini prompt when generating dynamic bot responses, so bots can reference what Aya actually said
- Shared fact tracker: a central object tracks established facts (e.g., "Aya mentioned the movie clip", "user asked about characters"). Subsystems query it to avoid contradictions

### Narrative catalyst tuning
- Beat-specific scripted catalyst messages: each beat's bot message pool includes catalyst messages designed to push the narrative forward
- Catalyst direction authored via `catalystGoal` string field on NarrativeBeatConfig (e.g., "Get Aya to talk about the character she drew"). ChatBotManager uses this to pick appropriate catalyst messages
- Catalyst messages sprinkled throughout the beat, mixed with regular messages -- organic, not clustered at the end
- User PTT skips ahead: if the user asks about a future beat topic, the director jumps to that beat. User can fast-forward the narrative by asking the right questions

### Experience polish & edge cases
- Dead air handling: if silence exceeds a threshold, bots post messages to re-engage. Aya then reacts to bot messages naturally
- Latency feedback: show a subtle "Aya is thinking..." indicator in the UI when Gemini response takes 5+ seconds
- Passive viewer support: full auto progression regardless of user input. Bots and Aya carry the whole narrative. User is just watching a stream
- Validation bar: one complete Play Mode session from start to movie clip. If it runs without errors and feels coherent, it's done

### Claude's Discretion
- Dead air silence threshold duration
- Exact "going live" transition presentation
- Fact tracker data structure and query interface
- How PTT topic matching detects future beat relevance for skip-ahead

</decisions>

<specifics>
## Specific Ideas

- Designer authors `catalystGoal` on each NarrativeBeatConfig to express "what are we driving toward" -- this is the hook for narrative steering without hard-coding bot lines to beats
- The experience should feel like tuning into a real stream that's just starting up -- loading state, then "going live" moment, then Aya begins

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 16-integration-experience-loop*
*Context gathered: 2026-02-17*
