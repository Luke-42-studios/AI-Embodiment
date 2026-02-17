---
phase: 14-narrative-director-user-interaction
plan: 04
subsystem: ui
tags: [push-to-talk, state-machine, finish-first, transcript-review, unity-ui-toolkit, uxml, uss]

# Dependency graph
requires:
  - phase: 14-02
    provides: NarrativeDirector with IsAyaSpeaking property and OnTurnComplete coordination
provides:
  - PushToTalkController with 5-state finish-first state machine
  - Transcript review overlay with 3-second auto-submit countdown
  - PTT acknowledgment indicator for finish-first visual feedback
  - LivestreamUI extended with overlay and acknowledgment public API
affects: [14-03, 15-reveal-system, 16-polish]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Finish-first PTT: defer StartListening via WaitingForAya state until OnTurnComplete"
    - "Transcript review with auto-submit countdown and idempotent submission guard"

key-files:
  created:
    - Assets/AyaLiveStream/PushToTalkController.cs
  modified:
    - Assets/AyaLiveStream/LivestreamUI.cs
    - Assets/AyaLiveStream/UI/LivestreamPanel.uxml
    - Assets/AyaLiveStream/UI/LivestreamPanel.uss

key-decisions:
  - "WaitingForAya sub-state defers mic until Aya finishes (prevents Gemini audio interruption)"
  - "Idempotent SubmitTranscript guard prevents double submission from Enter + timer race"
  - "ChatBotManager not paused during PTT (only Aya pauses, chat keeps flowing)"

patterns-established:
  - "Finish-first PTT pattern: WaitingForAya -> Recording transition on OnTurnComplete"
  - "Transcript approval flow: Recording -> Reviewing -> Submitted with auto-submit timer"

# Metrics
duration: 3min
completed: 2026-02-17
---

# Phase 14 Plan 04: Push-to-Talk Controller Summary

**Finish-first PushToTalkController with 5-state machine (Idle/WaitingForAya/Recording/Reviewing/Submitted), transcript review overlay with 3-second auto-submit, and acknowledgment indicator when Aya is speaking**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-17T23:49:25Z
- **Completed:** 2026-02-17T23:52:02Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- PushToTalkController with finish-first pattern: defers StartListening via WaitingForAya state until OnTurnComplete fires, preventing Gemini audio interruption (Pitfall 3)
- Transcript review overlay with 3-second auto-submit countdown bar, Enter to submit immediately, Escape to cancel silently
- Visual acknowledgment indicator ("Aya heard you -- finishing her thought...") appears within 500ms (same-frame CSS class toggle)
- LivestreamUI extended with 4 new public methods: ShowPTTAcknowledgment, ShowTranscriptOverlay, SetTranscriptText, UpdateAutoSubmitProgress

## Task Commits

Each task was committed atomically:

1. **Task 1: Add transcript overlay and acknowledgment elements to UXML/USS and LivestreamUI** - `598b70c` (feat)
2. **Task 2: Create PushToTalkController with finish-first state machine and transcript approval** - `f1a3868` (feat)

## Files Created/Modified
- `Assets/AyaLiveStream/PushToTalkController.cs` - 5-state PTT controller with finish-first logic, transcript approval, auto-submit
- `Assets/AyaLiveStream/LivestreamUI.cs` - Extended with transcript overlay and acknowledgment public API (4 new methods)
- `Assets/AyaLiveStream/UI/LivestreamPanel.uxml` - Added ptt-ack indicator and transcript-overlay container with countdown bar
- `Assets/AyaLiveStream/UI/LivestreamPanel.uss` - Dark-theme styles for overlay slide-up, countdown bar, acknowledgment pulse

## Decisions Made
- WaitingForAya is a distinct state (not a flag on Idle) to cleanly handle the deferred recording lifecycle and allow cancellation if user releases SPACE before Aya finishes
- Idempotent guard in SubmitTranscript prevents the Enter + auto-submit timer race condition (Pitfall 6)
- ChatBotManager is NOT paused during PTT -- only Aya defers, chat keeps flowing per CONTEXT.md spec
- User message posted to chat feed via ChatMessage.FromUser on submission (visible to other "viewers")

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- PushToTalkController ready for wiring into AyaSampleController scene hierarchy
- NarrativeDirector.IsAyaSpeaking provides the finish-first coordination signal
- Transcript review overlay ready for visual polish in Phase 16
- ChatBotManager _chatBotManager field on PushToTalkController needs Inspector assignment in scene

---
*Phase: 14-narrative-director-user-interaction*
*Completed: 2026-02-17*
