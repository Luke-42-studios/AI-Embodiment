---
phase: 14-narrative-director-user-interaction
plan: 02
subsystem: narrative
tags: [narrative-director, beat-lifecycle, sendtext, director-notes, dual-queue, pacing, awaitable]

# Dependency graph
requires:
  - phase: 14-01-narrative-beat-data-model
    provides: NarrativeBeatConfig SO, GoalUrgency enum, NarrativeSceneConfig, 3 beat assets
  - phase: 13-chat-bot-system
    provides: ChatBotManager burst loop, PersonaSession event pattern
provides:
  - NarrativeDirector MonoBehaviour with beat lifecycle and SendText steering
  - IsAyaSpeaking flag for downstream pacing
  - OnBeatTransition/OnBeatStarted/OnBeatEnded/OnAllBeatsComplete events
  - ChatBotManager pacing integration (slower bursts when Aya speaks, pause at transitions)
  - MarkGoalMet() for external goal completion signals
  - Skip keyword detection via OnInputTranscription
affects: [14-03 scene execution, 14-04 PTT controller, 15-scene-transitions, 16-experience-loop]

# Tech tracking
tech-stack:
  added: []
  patterns: [NarrativeDirector beat lifecycle with SendText steering, dual-queue coordination via events, pacing-aware burst timing]

key-files:
  created:
    - Assets/AyaLiveStream/NarrativeDirector.cs
  modified:
    - Assets/AyaLiveStream/ChatBotManager.cs

key-decisions:
  - "Pending beat queue: director notes queued for OnTurnComplete when Aya is speaking (Pitfall 3 guard)"
  - "5-second resume delay after beat transition for director note response to settle"
  - "Skip keywords advance to final beat (index _beats.Length-2 so loop increments to last)"
  - "Event handler references stored for clean unsubscription in OnDestroy"

patterns-established:
  - "NarrativeDirector: event-based coordination with downstream systems (IsAyaSpeaking, OnBeatTransition) -- never direct field manipulation"
  - "Pacing-aware burst timing: GetBurstLullDuration/GetMaxBotsForBurst helper methods with NarrativeDirector null-check fallback"
  - "Beat transition sync: _pausedForTransition flag checked in burst loop, cleared by async ResumeAfterTransition"

# Metrics
duration: 2min
completed: 2026-02-17
---

# Phase 14 Plan 02: NarrativeDirector Core Summary

**NarrativeDirector with time-based 3-beat lifecycle, SendText director notes guarded by IsAyaSpeaking, skip keyword detection, and ChatBotManager pacing integration (2x lull, 1.5x max, half bots when Aya speaks)**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-17T23:45:00Z
- **Completed:** 2026-02-17T23:47:25Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- NarrativeDirector drives beat progression through _beats array with time budget and early-exit on MarkGoalMet()
- Director notes sent via SendText only when Aya is not speaking; queued in _pendingBeatTransition for HandleTurnComplete if she is (Pitfall 3)
- Skip keyword detection on OnInputTranscription advances to final beat for user-triggered narrative acceleration
- ChatBotManager burst loop pacing: 2x min lull, 1.5x max lull, half bot count when Aya speaks
- Beat transition sync: ChatBotManager pauses for 5 seconds during transitions to prevent stale-context bursts (Pitfall 7)
- Full event API: OnBeatStarted, OnBeatEnded, OnBeatTransition, OnAllBeatsComplete for downstream systems

## Task Commits

Each task was committed atomically:

1. **Task 1: Create NarrativeDirector with beat lifecycle and SendText steering** - `46fcb63` (feat)
2. **Task 2: Add NarrativeDirector pacing integration to ChatBotManager** - `aeaa4a9` (feat)

## Files Created/Modified
- `Assets/AyaLiveStream/NarrativeDirector.cs` - Beat lifecycle loop, SendText steering, dual-queue coordination events, IsAyaSpeaking flag, skip keywords, urgency-aware pacing (263 lines)
- `Assets/AyaLiveStream/ChatBotManager.cs` - Added NarrativeDirector field, pacing helpers, beat transition pause/resume, modified ScriptedBurstLoop (+80 lines)

## Decisions Made
- **Pending beat queue pattern**: When a beat timer expires while Aya is speaking, the beat is stored in `_pendingBeatTransition` and `ExecuteBeatTransition` is called from `HandleTurnComplete`. This prevents the SendText interruption pitfall without blocking.
- **5-second transition resume delay**: ChatBotManager waits 5 seconds after a beat transition before resuming bursts, giving the director note response time to settle. This is a conservative initial value; can be tuned via testing.
- **Skip to last beat via index manipulation**: When skip keywords are detected, `_currentBeatIndex` is set to `_beats.Length - 2` so the for-loop increments to the final beat. This preserves the final beat's full execution rather than skipping it.
- **Event handler references for unsubscription**: Lambda event handlers stored in private fields (`_onAISpeakingStarted`, etc.) to enable clean `-=` unsubscription in OnDestroy, preventing memory leaks.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. After opening Unity, drag the NarrativeDirector component onto the same GameObject as PersonaSession and wire the Inspector references (_session, _beats, _livestreamUI). Drag NarrativeDirector onto ChatBotManager's _narrativeDirector field.

## Next Phase Readiness
- NarrativeDirector IsAyaSpeaking and events ready for 14-03 scene execution
- OnAllBeatsComplete ready for Phase 15 reveal trigger
- CurrentBeat.urgency accessible for PTT acknowledgment pacing (14-04)
- Beat transition sync points prevent stale-context bursts in ChatBotManager
- StartNarrative() ready to be called by AyaSampleController on session connect

---
*Phase: 14-narrative-director-user-interaction*
*Completed: 2026-02-17*
