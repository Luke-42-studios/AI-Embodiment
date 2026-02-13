---
phase: 10-function-calling-and-goals-migration
plan: 02
subsystem: api
tags: [function-calling, gemini-live, websocket, prompt-fallback, goal-system, regex]

# Dependency graph
requires:
  - phase: 10-function-calling-and-goals-migration
    provides: FunctionDeclaration builder, FunctionRegistry dual-path output, GeminiEvent.FunctionId, GeminiLiveConfig.ToolsJson, SendToolResponse
provides:
  - Full function calling pipeline wired into PersonaSession (register -> setup -> dispatch -> response)
  - Native and prompt-based dual-path function calling via UseNativeFunctionCalling flag
  - FunctionCallCancellation event routing
  - Centralized system instruction composition with function instructions overload
  - Goal update finalization with informational mid-session limitation log
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dual-path function calling: static flag switches between native WebSocket tools and prompt-based [CALL:] tag parsing"
    - "Prompt-based regex parsing with bounded buffer (1000 char cap with 500 char trim)"

key-files:
  created: []
  modified:
    - Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs
    - Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs

key-decisions:
  - "UseNativeFunctionCalling is a public static field (not instance) -- global toggle, not per-session"
  - "Prompt-based [CALL:] tags left in transcription events (stripping would break incremental stream)"
  - "Prompt-based calls fire-and-forget only (no server-assigned ID, no toolResponse)"
  - "SendGoalUpdate uses Debug.Log (informational) not LogWarning -- expected behavior per CONTEXT.md"
  - "SystemInstructionBuilder 3-param overload centralizes all instruction composition"

patterns-established:
  - "Dual-path pattern: static flag + conditional logic in Connect() for native vs prompt-based paths"
  - "Buffer management pattern: clear on interruption, turn complete, and disconnect"

# Metrics
duration: 2min
completed: 2026-02-13
---

# Phase 10 Plan 02: Function Calling Session Wiring Summary

**Full function calling pipeline wired into PersonaSession with native/prompt-based dual-path, ID-correlated tool responses, and goal update finalization**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-13T21:38:45Z
- **Completed:** 2026-02-13T21:41:21Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Wired complete function calling lifecycle: RegisterFunction with declarations -> Connect() setup handshake with ToolsJson -> FunctionCall dispatch with FunctionId -> handler invocation -> SendToolResponse back to Gemini
- Added prompt-based fallback path parsing [CALL: name {...}] tags from transcription with bounded buffer management
- Routed FunctionCallCancellation events to FunctionRegistry.MarkCancelled for race condition prevention
- Finalized SendGoalUpdate with informational log about Gemini Live mid-session limitation
- Centralized system instruction composition in SystemInstructionBuilder with 3-parameter overload

## Task Commits

Each task was committed atomically:

1. **Task 1: PersonaSession function calling wiring and prompt-based fallback** - `cab844a` (feat)
2. **Task 2: SystemInstructionBuilder prompt-based function instructions** - `9d2a6ac` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` - Full function calling wiring (native + prompt-based), goal update finalization, buffer management
- `Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs` - 3-parameter Build overload for centralized instruction composition

## Decisions Made
- UseNativeFunctionCalling is a public static field (global toggle) rather than per-instance, matching the plan's intent for a simple developer switch
- Prompt-based [CALL:] tags are left in transcription events to avoid breaking incremental stream fragmentation
- Prompt-based function calls are fire-and-forget only (null callId) since there is no server-assigned ID
- SendGoalUpdate uses Debug.Log (not LogWarning) since mid-session limitation is expected behavior, not a warning
- SystemInstructionBuilder owns all instruction composition via the 3-parameter overload, keeping PersonaSession clean

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 10 complete: all function calling infrastructure (10-01) and session wiring (10-02) delivered
- MIG-04 (function calling via WebSocket) complete
- MIG-05 (goal system migration) complete
- Ready for Phase 11 (final phase)

---
*Phase: 10-function-calling-and-goals-migration*
*Completed: 2026-02-13*
