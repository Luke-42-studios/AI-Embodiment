---
phase: 04-function-calling-and-conversational-goals
plan: 02
subsystem: ai-goals
tags: [conversational-goals, priority-enum, goal-manager, system-instruction]

# Dependency graph
requires:
  - phase: 01-foundation-and-core-session
    provides: "Namespace conventions, enum/class patterns (SessionState, VoiceBackend)"
provides:
  - "GoalPriority enum (Low, Medium, High)"
  - "ConversationalGoal data class with mutable priority"
  - "GoalManager with goal lifecycle and urgency-framed instruction composition"
affects: [04-03 PersonaSession integration, 04-03 SystemInstructionBuilder goal injection]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Priority-ordered urgency framing for system instructions", "Plain C# manager class with no Unity/Firebase dependencies"]

key-files:
  created:
    - "Packages/com.google.ai-embodiment/Runtime/GoalPriority.cs"
    - "Packages/com.google.ai-embodiment/Runtime/ConversationalGoal.cs"
    - "Packages/com.google.ai-embodiment/Runtime/GoalManager.cs"
  modified: []

key-decisions:
  - "ConversationalGoal is a class (not struct) -- goals are reference types with identity, managed in a list"
  - "GoalManager is plain C# (not MonoBehaviour) -- no Unity or Firebase dependencies, similar to PacketAssembler"
  - "Priority ordering via sequential AppendGoalsForPriority calls -- simple and allocation-free versus sorting"

patterns-established:
  - "Urgency framing: HIGH PRIORITY / MEDIUM PRIORITY / LOW PRIORITY with escalating action language"
  - "Goal lookup by string ID for reprioritization: manager.GetGoal(id).Priority = GoalPriority.High"

# Metrics
duration: 1min
completed: 2026-02-05
---

# Phase 4 Plan 2: Conversational Goals Infrastructure Summary

**GoalPriority enum, ConversationalGoal data class, and GoalManager with urgency-framed instruction composition**

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-05T21:53:10Z
- **Completed:** 2026-02-05T21:54:16Z
- **Tasks:** 2
- **Files created:** 3

## Accomplishments
- GoalPriority enum with Low, Medium, High urgency levels following VoiceBackend/SessionState pattern
- ConversationalGoal class with immutable Id/Description, mutable Priority, and validating constructor
- GoalManager with full lifecycle API (add, remove, get, get-all, has-goals) and ComposeGoalInstruction producing priority-ordered urgency-framed text

## Task Commits

Each task was committed atomically:

1. **Task 1: GoalPriority enum and ConversationalGoal data class** - `928e4b9` (feat)
2. **Task 2: GoalManager with lifecycle and instruction composition** - `add0fd4` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/GoalPriority.cs` - Priority level enum (Low, Medium, High) with XML docs
- `Packages/com.google.ai-embodiment/Runtime/ConversationalGoal.cs` - Goal data class with immutable ID/description, mutable priority, validating constructor
- `Packages/com.google.ai-embodiment/Runtime/GoalManager.cs` - Goal storage, lifecycle operations, and urgency-framed instruction text composition

## Decisions Made
- ConversationalGoal is a class (not struct) -- goals are reference types with identity semantics, managed in a list where lookup-by-ID matters
- GoalManager is plain C# with no Unity or Firebase dependencies -- same pattern as PacketAssembler, keeps Firebase boundary in SystemInstructionBuilder only
- Priority ordering uses sequential iteration per priority level rather than sorting -- zero allocations, simple, and deterministic

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- GoalPriority, ConversationalGoal, and GoalManager are ready for PersonaSession and SystemInstructionBuilder integration in Plan 04-03
- GoalManager.ComposeGoalInstruction produces plain string text that SystemInstructionBuilder will incorporate
- No blockers for next plan

---
*Phase: 04-function-calling-and-conversational-goals*
*Completed: 2026-02-05*
