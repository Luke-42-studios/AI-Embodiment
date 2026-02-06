---
phase: 06-sample-scene-and-integration
plan: 02
subsystem: ui
tags: [ui-toolkit, sample-scripts, push-to-talk, function-calling, conversational-goals, input-system]

# Dependency graph
requires:
  - phase: 06-sample-scene-and-integration
    provides: UXML layout with named elements (chat-log, persona-name, speaking-indicator, status-label, ptt-button), AyaLiveStream asmdef
  - phase: 01-project-scaffolding
    provides: PersonaSession public API, PersonaConfig, FunctionRegistry, GoalManager
  - phase: 04-function-calling-and-goals
    provides: RegisterFunction, AddGoal, FunctionCallContext, GoalPriority APIs
provides:
  - AyaChatUI MonoBehaviour managing UIDocument with streaming transcription display and PTT button wiring
  - AyaSampleController MonoBehaviour with function registration, intro coroutine, keyboard PTT, and goal injection
affects: [06-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Streaming transcription with replacement semantics (full text per chunk, not deltas)"
    - "Separate AudioSource for pre-recorded intro vs live AI audio playback"
    - "Exchange counting for deferred goal injection after warm-up turns"
    - "Fire-and-forget function handlers returning null with system message logging"

key-files:
  created:
    - Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaChatUI.cs
    - Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs
  modified: []

key-decisions:
  - "Streaming transcription uses replacement semantics -- currentMessage label text replaced on each chunk, not appended"
  - "OnAISpeakingStarted/Stopped mapped directly to CSS class toggle on speaking indicator (indicator--speaking)"
  - "Function handlers are all fire-and-forget (return null) with LogSystemMessage for observability"
  - "Goal injection at exactly 3 exchanges with bool guard preventing double-injection"

patterns-established:
  - "Consumer code pattern: RegisterFunction before Connect, event subscription in Start/OnEnable, keyboard poll in Update"
  - "Chat UI pattern: per-turn Label tracking with null reset on turn complete or speaking state change"

# Metrics
duration: 2min
completed: 2026-02-06
---

# Phase 6 Plan 02: Sample Scripts Summary

**AyaChatUI with streaming transcription display and PTT button wiring, plus AyaSampleController with 3 function declarations (emote/17 animations, start_movie, start_drawing), intro coroutine, spacebar PTT, and deferred goal injection**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-06T00:01:19Z
- **Completed:** 2026-02-06T00:03:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- UI Toolkit chat log controller that subscribes to all PersonaSession events and manages streaming AI/user transcription display with replacement semantics
- Push-to-talk via both on-screen button (PointerDown/Up) and spacebar (Input System Keyboard.current)
- Three function declarations registered before Connect: emote with Schema.Enum of 17 animations, start_movie, start_drawing
- Pre-recorded intro coroutine on separate AudioSource followed by live session Connect
- Conversational goal injection after 3 exchange turns with double-injection guard

## Task Commits

Each task was committed atomically:

1. **Task 1: AyaChatUI -- UI Toolkit chat log controller** - `c977b60` (feat)
2. **Task 2: AyaSampleController -- main controller with functions, intro, PTT, goals** - `2c9aa87` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaChatUI.cs` - UI Toolkit chat log controller with event subscriptions, streaming transcription display, speaking glow, PTT button, system messages, auto-scroll
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs` - Main controller with function registration, intro coroutine, keyboard PTT, exchange counting, goal injection

## Decisions Made
- Streaming transcription uses replacement semantics (full text per chunk replaces label, not appended) -- matches Gemini Live output/input transcription behavior
- Speaking indicator uses CSS class toggle (AddToClassList/RemoveFromClassList) rather than inline style changes
- All three function handlers are fire-and-forget (return null) with chat system messages for observability in the sample
- Goal injection happens at exactly 3 exchanges (WarmUpExchanges constant) with a bool guard to prevent double-injection
- PTT button uses PointerDown/PointerUp events (not Click) for hold-to-talk semantics matching spacebar behavior

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Both sample scripts ready for scene assembly in Plan 03
- AyaChatUI and AyaSampleController are pure consumer code using only PersonaSession's public API
- Plan 03 will create the Unity scene connecting these scripts to GameObjects with component references

---
*Phase: 06-sample-scene-and-integration*
*Completed: 2026-02-06*
