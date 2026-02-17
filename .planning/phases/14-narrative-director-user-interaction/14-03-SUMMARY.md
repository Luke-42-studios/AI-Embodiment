---
phase: 14-narrative-director-user-interaction
plan: 03
subsystem: narrative
tags: [narrative-director, scene-execution, sendtext, aya-dialogue, chat-checks, conditional-transitions, awaitable]

# Dependency graph
requires:
  - phase: 14-02-narrative-director-core
    provides: NarrativeDirector beat lifecycle, IsAyaSpeaking, OnTurnComplete, RunBeatLoop
  - phase: 13-chat-bot-system
    provides: ChatBotManager, TrackedChatMessage, GetUnrespondedMessages
provides:
  - Scene execution within beats (AyaDialogue, AyaChecksChat, AyaAction, ChatBurst, UserChoice)
  - Conditional transition handling (TimedOut, QuestionsAnswered, Always)
  - WaitForAyaIdle guard pattern for all SendText calls
  - User-priority message selection in AyaChecksChat
affects: [14-04 PTT controller, 15-scene-transitions, 16-experience-loop]

# Tech tracking
tech-stack:
  added: []
  patterns: [scene-type dispatch via ExecuteScene switch, WaitForAyaIdle + WaitForTurnComplete async guards, user-priority message filtering without LINQ]

key-files:
  created: []
  modified:
    - Assets/AyaLiveStream/NarrativeDirector.cs

key-decisions:
  - "Scene execution runs sequentially within beat time budget, exits early on budget expiry or goal-met"
  - "AyaChecksChat builds summary string rather than injecting individual messages (Pitfall 8: context window growth)"
  - "User messages always prioritized over bot messages in AyaChecksChat (foreach filter, not LINQ)"
  - "_questionsAnsweredCount resets at each beat start for per-beat conditional tracking"

patterns-established:
  - "WaitForAyaIdle guard: every SendText call preceded by async wait for _isAyaSpeaking == false"
  - "WaitForTurnComplete: set _turnComplete = false before SendText, await _turnComplete = true from HandleTurnComplete"
  - "Summary-based chat injection: build StringBuilder with director note framing, not raw message pass-through"

# Metrics
duration: 3min
completed: 2026-02-17
---

# Phase 14 Plan 03: Scene Execution Summary

**Scene-type execution within beats: AyaDialogue sends random dialogue via SendText, AyaChecksChat gathers unresponded messages with user-priority, conditional transitions (TimedOut/QuestionsAnswered/Always) gate scene advancement**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-17T23:49:34Z
- **Completed:** 2026-02-17T23:52:42Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- ExecuteBeatScenes iterates through scenes within each beat's time budget, exiting early on budget expiry or goal-met
- ExecuteAyaDialogue selects a random dialogue alternative and sends to Gemini via SendText with WaitForAyaIdle guard
- ExecuteAyaChecksChat gathers unresponded messages from ChatBotManager, prioritizes user messages over bot messages, builds a summary string, and sends as a director note
- WaitForCondition supports all 3 condition types: TimedOut (seconds), QuestionsAnswered (count), Always (immediate)
- All SendText calls are guarded by WaitForAyaIdle to prevent Pitfall 3 (interruption)
- HandleTurnComplete now sets _turnComplete = true for scene execution coordination while preserving pending beat transition handling

## Task Commits

Each task was committed atomically:

1. **Task 1: Add scene execution methods to NarrativeDirector** - `76cd99b` (feat)
2. **Task 2: Verify scene execution integrates correctly with beat lifecycle** - `2c1cb32` (fix)

## Files Created/Modified
- `Assets/AyaLiveStream/NarrativeDirector.cs` - Added scene execution methods (ExecuteBeatScenes, ExecuteScene, ExecuteAyaDialogue, ExecuteAyaChecksChat, ExecuteAyaAction), conditional transition handling (WaitForCondition), async helpers (WaitForAyaIdle, WaitForTurnComplete), ChatBotManager reference, turn tracking state (471 lines, +213 from 14-02)

## Decisions Made
- **Summary-based chat injection**: AyaChecksChat builds a StringBuilder summary with director note framing rather than passing raw messages individually, to minimize context window growth (Pitfall 8).
- **User-priority without LINQ**: Used foreach loop to separate user vs bot messages instead of `.Where()` to avoid LINQ allocation in a potentially hot path.
- **Per-beat questions counter**: `_questionsAnsweredCount` resets at each beat start, so QuestionsAnswered conditions are scoped to the current beat.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Missing `using AIEmbodiment;` directive**
- **Found during:** Task 2 (verification pass)
- **Issue:** NarrativeDirector.cs (from 14-02) used `PersonaSession` which is in the `AIEmbodiment` namespace, but only had `using System;` and `using UnityEngine;`. C# child namespaces do not automatically import parent namespace types.
- **Fix:** Added `using AIEmbodiment;` directive
- **Files modified:** Assets/AyaLiveStream/NarrativeDirector.cs
- **Verification:** All type references resolve correctly with the added import
- **Committed in:** `2c1cb32` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Essential for compilation. No scope creep.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. In Unity Inspector, drag ChatBotManager onto NarrativeDirector's `_chatBotManager` field to enable AyaChecksChat scene execution.

## Next Phase Readiness
- Scene execution complete, ready for 14-04 PTT controller integration
- WaitForAyaIdle/WaitForTurnComplete patterns available for reuse in PTT acknowledgment flow
- AyaAction placeholder ready for Phase 15 scene transition implementation
- _questionsAnsweredCount accessible for potential goal-met signals in Phase 16

---
*Phase: 14-narrative-director-user-interaction*
*Completed: 2026-02-17*
