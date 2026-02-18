---
phase: 16-integration-experience-loop
plan: 01
subsystem: orchestration
tags: [unity, monobehaviour, async-awaitable, fact-tracker, transcript-buffer, dead-air, loading-ui]

# Dependency graph
requires:
  - phase: 12-livestream-ui
    provides: LivestreamUI MonoBehaviour, chat feed, Aya transcript panel, toast system
  - phase: 13-chat-bots
    provides: ChatBotManager with StartBursts/StopBursts, TrackedChatMessage
  - phase: 14-narrative-beats
    provides: NarrativeDirector with beat loop, OnAllBeatsComplete event, PushToTalkController
  - phase: 15-scene-transition
    provides: SceneTransitionHandler, AnimationConfig, play_animation function pattern
provides:
  - LivestreamController top-level orchestrator with connection-wait gate and going-live transition
  - FactTracker shared fact dictionary for cross-system coherence
  - AyaTranscriptBuffer ring buffer of Aya transcript turns for prompt enrichment
  - LivestreamUI loading/going-live/thinking indicator extensions
  - Dead air detection background monitor
affects: [16-02-cross-system-context, 16-03-validation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Connection-wait gate: poll SessionState with configurable timeout before starting subsystems"
    - "Going-live transition: loading overlay -> GOING LIVE! -> start experience"
    - "Dead air monitor: async Awaitable background loop tracking _lastOutputTime"
    - "Plain C# context objects created in Start(), not MonoBehaviours"

key-files:
  created:
    - Assets/AyaLiveStream/LivestreamController.cs
    - Assets/AyaLiveStream/FactTracker.cs
    - Assets/AyaLiveStream/AyaTranscriptBuffer.cs
  modified:
    - Assets/AyaLiveStream/LivestreamUI.cs
    - Assets/AyaLiveStream/UI/LivestreamPanel.uxml
    - Assets/AyaLiveStream/UI/LivestreamPanel.uss

key-decisions:
  - "Named event handler references for clean unsubscription (NarrativeDirector pattern)"
  - "Graceful degradation on connection timeout: start bots only, skip narrative"
  - "Dead air tracks only Aya silence, not total silence -- bot messages are visual-only"

patterns-established:
  - "Pattern: LivestreamController as thin orchestrator holding SerializeField refs to all subsystems"
  - "Pattern: Plain C# context objects (FactTracker, AyaTranscriptBuffer) created in Start, not MonoBehaviours"
  - "Pattern: Connection-wait gate with configurable timeout before starting experience"

# Metrics
duration: 4min
completed: 2026-02-17
---

# Phase 16 Plan 01: Foundation Orchestrator Summary

**LivestreamController with connection-wait gate, going-live transition, dead air monitor, plus FactTracker and AyaTranscriptBuffer context objects**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-18T01:12:41Z
- **Completed:** 2026-02-18T01:16:58Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- LivestreamController replaces AyaSampleController as the root orchestrator for the livestream scene
- FactTracker and AyaTranscriptBuffer provide injectable context for cross-system coherence (ready for Plan 02)
- Connection-wait gate with 15s timeout and graceful degradation (bots-only mode on failure)
- "Going live" transition: loading overlay, "GOING LIVE!" emphasis, then subsystem startup
- Dead air monitor: "Aya is thinking..." at 5s, dead air log at 10s
- LivestreamUI extended with SetLoadingState, ShowGoingLive, ShowThinkingIndicator

## Task Commits

Each task was committed atomically:

1. **Task 1: FactTracker, AyaTranscriptBuffer, and LivestreamUI extensions** - `e810d98` (feat -- pre-existing, committed in prior session's docs commit)
2. **Task 2: LivestreamController orchestrator** - `9361f51` (feat)

**Plan metadata:** (pending)

## Files Created/Modified
- `Assets/AyaLiveStream/LivestreamController.cs` - Top-level orchestrator MonoBehaviour with all subsystem references, initialization, dead air, shutdown
- `Assets/AyaLiveStream/FactTracker.cs` - Plain C# Dictionary<string,bool> fact store with SetFact/HasFact/GetFactsSummary
- `Assets/AyaLiveStream/AyaTranscriptBuffer.cs` - Ring buffer of Aya turns with AppendText/CompleteTurn/GetRecentTurns
- `Assets/AyaLiveStream/LivestreamUI.cs` - Added SetLoadingState, ShowGoingLive, ShowThinkingIndicator + element references
- `Assets/AyaLiveStream/UI/LivestreamPanel.uxml` - Added loading-overlay and thinking-indicator elements
- `Assets/AyaLiveStream/UI/LivestreamPanel.uss` - Added loading-overlay, loading-text, thinking-indicator style classes

## Decisions Made
- Named event handler references stored as private fields for clean unsubscription (matches NarrativeDirector.OnDestroy pattern, avoids lambda memory leaks)
- Graceful degradation on connection timeout: starts bots only (no narrative) rather than failing completely
- Dead air timer tracks only Aya output time, not bot messages -- bots are visual-only and don't indicate the system is "working"
- FactTracker logs only on change (not every SetFact call) to reduce console noise

## Deviations from Plan

### Pre-existing Files

**1. Task 1 files committed in prior session**
- **Found during:** Task 1 staging
- **Issue:** FactTracker.cs, AyaTranscriptBuffer.cs, LivestreamUI.cs modifications, and USS additions were accidentally included in a prior session's docs commit (e810d98)
- **Impact:** Task 1 files already exist with correct content in HEAD. No new commit needed for Task 1.
- **Resolution:** Verified all content matches plan specification. Proceeded to Task 2.

---

**Total deviations:** 1 (pre-existing work from prior session)
**Impact on plan:** No scope change. All files have correct content. LivestreamController (Task 2) was the only new commit needed.

## Issues Encountered
None -- plan executed cleanly. Task 1 was pre-committed but content was verified correct.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- LivestreamController is ready as the root orchestrator for the livestream scene
- FactTracker and AyaTranscriptBuffer are ready for Plan 02 to inject into ChatBotManager and NarrativeDirector
- UXML/USS loading and thinking UI elements are ready for runtime use
- Plan 02 will wire cross-system context injection (Aya-to-bot via transcript buffer, bot prompt enrichment via FactTracker)

---
*Phase: 16-integration-experience-loop*
*Completed: 2026-02-17*
