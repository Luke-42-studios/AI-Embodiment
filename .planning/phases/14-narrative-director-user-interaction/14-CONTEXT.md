# Phase 14: Narrative Director & User Interaction - Context

**Gathered:** 2026-02-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Aya drives a time-based narrative arc through beat/scene structure, responds to user push-to-talk with finish-first priority (completing her current thought before addressing the user), and the dual-queue system orchestrates chat scenes and Aya scenes in parallel. Scene transitions and animation function calls are Phase 15. Full experience loop wiring is Phase 16.

</domain>

<decisions>
## Implementation Decisions

### Narrative beat design
- 3 beats for the demo (not 5): warm-up (getting to know Aya), art process, characters -- character discussion leads naturally into the movie reveal
- Developer/producer configurable beats via Unity Inspector (ScriptableObject or similar) so beats can be authored without code changes
- Mostly time-driven with early-exit: each beat has a time budget but can end early if its goal is met
- Goal is the primary completion signal, time is the fallback if conversation stalls
- User can trigger a skip to the final reveal beat if they say something directly on-point (e.g., asking about the movie)
- Seamless flow transitions between beats -- Aya naturally pivots topic, no visible break or marker

### Beat content and steering
- Pre-authored scripts for Aya's beat dialogue (not Gemini-generated)
- Nevatars beat/scene data used as inspiration for redesigned beats, not migrated as-is
- Dual steering: system prompt establishes the overall narrative arc, SendText director notes provide beat-specific nudges at transitions

### Scene orchestration feel
- Chat bursts slow down when Aya is talking -- bots say too much at full pace and it causes user confusion waiting for Aya to respond
- Aya checks chat at scheduled beat boundaries (between beats), not reactively mid-beat
- User messages always get priority over bot messages when Aya checks chat
- User speaking forces Aya to pick up the conversation and respond (overrides scheduled chat check timing)
- Queues sync at beat boundaries -- run independently within a beat, sync up at transitions for coherence
- Only Aya pauses during PTT (chat keeps flowing, feels like a real stream)

### Transcript approval flow
- After user releases PTT, transcript appears as a bottom overlay (slides up over chat feed)
- 3-second auto-submit timer -- transcript sends automatically if user takes no action
- User can press Enter to submit immediately
- Cancel button to discard -- silent discard, no feedback animation
- Approve or cancel only -- transcript is not editable (no text field editing)

### Claude's Discretion
- PTT visual acknowledgment design ("Aya noticed you" indicator)
- Exact beat time proportions within the 10-minute session
- How chat slowdown is implemented (reduced burst frequency, longer delays, fewer bots per burst)
- Auto-submit timer visual treatment (countdown, progress bar, etc.)
- How beat skip detection works (keyword matching, semantic analysis, etc.)

</decisions>

<specifics>
## Specific Ideas

- "Sometimes bots say too much and it takes a while for her to respond to us, which leads to user confusion" -- chat pacing during Aya dialogue is a known pain point to solve
- "I would like a producer in Unity to be able to set these things if possible" -- beat configuration should be Inspector-friendly, not hardcoded
- The narrative arc should feel like: meet Aya -> watch her work -> she talks about characters -> one character captivates her -> this leads into showing the movie about that character

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 14-narrative-director-user-interaction*
*Context gathered: 2026-02-17*
