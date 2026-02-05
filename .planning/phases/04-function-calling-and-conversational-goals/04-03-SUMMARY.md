---
phase: 04-function-calling-and-conversational-goals
plan: 03
subsystem: api
tags: [function-calling, conversational-goals, system-instruction, live-session, dispatch]

# Dependency graph
requires:
  - phase: 04-function-calling-and-conversational-goals
    provides: "FunctionRegistry, FunctionCallContext, GoalManager, ConversationalGoal, GoalPriority"
  - phase: 03-synchronization
    provides: "SyncPacket with FunctionCall type, PacketAssembler routing"
  - phase: 01-foundation-and-core-session
    provides: "PersonaSession, SystemInstructionBuilder, PersonaConfig"
provides:
  - "PersonaSession.RegisterFunction API for function calling registration"
  - "Full function dispatch pipeline: SyncPacket -> handler -> FunctionResponsePart auto-response"
  - "OnFunctionError event for handler exception reporting"
  - "LiveSessionToolCallCancellation handling in ProcessResponse"
  - "PersonaSession.AddGoal/RemoveGoal/ReprioritizeGoal runtime goal management API"
  - "Mid-session system instruction updates via SendGoalUpdate with fallback strategy"
  - "SystemInstructionBuilder.Build(config, goalManager) and BuildInstructionText internal helpers"
affects: [05-chirp-tts, 06-polish-and-packaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SyncPacket intercept pattern: HandleSyncPacket dispatches before forwarding to subscribers"
    - "Async void SendGoalUpdate with error fallback guidance for mid-session instruction rejection"
    - "Internal static helper extraction for cross-class access within assembly"

key-files:
  created: []
  modified:
    - "Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs"
    - "Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs"

key-decisions:
  - "SystemInstructionBuilder.BuildInstructionText is internal (not private) -- PersonaSession needs raw string for mid-session role system ModelContent"
  - "HandleSyncPacket intercepts FunctionCall packets before forwarding to OnSyncPacket -- developers can still observe function calls"
  - "SendGoalUpdate uses role system ModelContent with REPLACE semantics -- fallback is disconnect/reconnect if rejected"
  - "Connect() uses Build(_config, _goalManager) ensuring pre-connect goals are included in initial instruction"

patterns-established:
  - "SyncPacket intercept: dispatch internally, then always forward to subscribers for observability"
  - "Mid-session instruction update via role system ModelContent with async void and error fallback"

# Metrics
duration: 3min
completed: 2026-02-05
---

# Phase 4 Plan 3: Function Calling and Goal Management Integration Summary

**Full function dispatch pipeline with auto-response round-trip and runtime goal management with mid-session system instruction updates via PersonaSession API**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-05T21:57:33Z
- **Completed:** 2026-02-05T22:00:13Z
- **Tasks:** 3
- **Files modified:** 2

## Accomplishments
- SystemInstructionBuilder refactored with internal BuildInstructionText helpers and goal-inclusive Build overload
- PersonaSession function calling pipeline: RegisterFunction -> SyncPacket intercept -> handler dispatch -> FunctionResponsePart auto-send with cancellation checks
- PersonaSession goal management: AddGoal/RemoveGoal/ReprioritizeGoal with immediate mid-session system instruction updates and error fallback strategy
- Phase 4 complete: all FUNC-01 through FUNC-03 and GOAL-01 through GOAL-04 requirements satisfied

## Task Commits

Each task was committed atomically:

1. **Task 1: SystemInstructionBuilder goal-inclusive overload and internal text helper** - `6e39daf` (feat)
2. **Task 2: PersonaSession function calling integration** - `cbcb3a8` (feat)
3. **Task 3: PersonaSession goal management integration** - `7881adb` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs` - Extracted BuildInstructionText internal helpers, added Build(config, goalManager) overload
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` - Added FunctionRegistry/GoalManager fields, RegisterFunction/AddGoal/RemoveGoal/ReprioritizeGoal API, HandleSyncPacket dispatch, SendFunctionResponseAsync, SendGoalUpdate, LiveSessionToolCallCancellation handling

## Decisions Made
- [04-03]: SystemInstructionBuilder.BuildInstructionText is internal (not private) -- PersonaSession needs raw string access for mid-session role "system" ModelContent construction
- [04-03]: HandleSyncPacket intercepts FunctionCall packets before forwarding to OnSyncPacket subscribers -- developers can still observe function calls for logging/UI
- [04-03]: SendGoalUpdate uses async void with role "system" ModelContent and REPLACE semantics -- catch block logs fallback guidance (disconnect/reconnect) if wire format rejected
- [04-03]: Connect() uses Build(_config, _goalManager) ensuring goals added before Connect() are included in initial system instruction

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 4 (Function Calling and Conversational Goals) is complete
- All infrastructure types (Plans 01-02) and integration (Plan 03) are committed
- PersonaSession now exposes the full developer API: RegisterFunction, AddGoal, RemoveGoal, ReprioritizeGoal
- Ready for Phase 5 (Chirp TTS) or Phase 6 (Polish and Packaging)
- No blockers

---
*Phase: 04-function-calling-and-conversational-goals*
*Completed: 2026-02-05*
