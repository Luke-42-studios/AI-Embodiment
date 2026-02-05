---
phase: 01-foundation-and-core-session
plan: 01
subsystem: infra
tags: [unity, upm, asmdef, firebase, threading, concurrentqueue]

# Dependency graph
requires:
  - phase: none
    provides: "First plan -- no prior dependencies"
provides:
  - "Firebase.AI assembly definition enabling cross-assembly referencing from UPM package"
  - "UPM package skeleton (com.google.ai-embodiment) with Runtime, Editor, Tests asmdefs"
  - "MainThreadDispatcher singleton for background-to-main-thread marshaling"
  - "SessionState enum defining session lifecycle states"
affects: [01-02, 01-03, 02-01, 02-02, 02-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ConcurrentQueue<Action> MainThreadDispatcher with RuntimeInitializeOnLoadMethod auto-init"
    - "UPM embedded package at Packages/ with asmdef cross-assembly references"

key-files:
  created:
    - "Assets/Firebase/FirebaseAI/Firebase.AI.asmdef"
    - "Packages/com.google.ai-embodiment/package.json"
    - "Packages/com.google.ai-embodiment/Runtime/com.google.ai-embodiment.asmdef"
    - "Packages/com.google.ai-embodiment/Runtime/MainThreadDispatcher.cs"
    - "Packages/com.google.ai-embodiment/Runtime/SessionState.cs"
    - "Packages/com.google.ai-embodiment/Editor/com.google.ai-embodiment.editor.asmdef"
    - "Packages/com.google.ai-embodiment/Tests/Runtime/com.google.ai-embodiment.tests.asmdef"
    - "Packages/com.google.ai-embodiment/CHANGELOG.md"
    - "Packages/com.google.ai-embodiment/LICENSE.md"
  modified: []

key-decisions:
  - "Firebase AI SDK gets its own asmdef (Firebase.AI) with overrideReferences for precompiled DLLs"
  - "UPM package placed under Packages/ as embedded package for auto-discovery"

patterns-established:
  - "ConcurrentQueue<Action> dispatch: background threads call MainThreadDispatcher.Enqueue(), Update() drains"
  - "HideAndDontSave singleton: library internals hidden from developer hierarchy"

# Metrics
duration: 2min
completed: 2026-02-05
---

# Phase 1 Plan 1: UPM Package Skeleton and Infrastructure Summary

**Firebase.AI asmdef for cross-assembly referencing, UPM package skeleton with Runtime/Editor/Tests asmdefs, ConcurrentQueue MainThreadDispatcher, and SessionState lifecycle enum**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-05T18:48:15Z
- **Completed:** 2026-02-05T18:50:01Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments

- Firebase AI SDK source files compile as separate Firebase.AI assembly with precompiled DLL references, enabling cross-assembly referencing from the UPM package
- Complete UPM package structure under Packages/com.google.ai-embodiment/ with Runtime, Editor, and Tests assembly definitions
- MainThreadDispatcher singleton auto-initializes before scene load, drains ConcurrentQueue in Update, survives scene transitions, hidden from hierarchy
- SessionState enum with 5 lifecycle states (Disconnected, Connecting, Connected, Disconnecting, Error)

## Task Commits

Each task was committed atomically:

1. **Task 1: Firebase AI asmdef and UPM package skeleton** - `6ffc296` (feat)
2. **Task 2: MainThreadDispatcher and SessionState enum** - `2f191f3` (feat)

## Files Created/Modified

- `Assets/Firebase/FirebaseAI/Firebase.AI.asmdef` - Assembly definition for Firebase AI SDK source with precompiled DLL references
- `Packages/com.google.ai-embodiment/package.json` - UPM package manifest (v0.1.0)
- `Packages/com.google.ai-embodiment/Runtime/com.google.ai-embodiment.asmdef` - Runtime assembly referencing Firebase.AI
- `Packages/com.google.ai-embodiment/Editor/com.google.ai-embodiment.editor.asmdef` - Editor-only assembly referencing Runtime
- `Packages/com.google.ai-embodiment/Tests/Runtime/com.google.ai-embodiment.tests.asmdef` - Test assembly with UNITY_INCLUDE_TESTS constraint
- `Packages/com.google.ai-embodiment/Runtime/MainThreadDispatcher.cs` - Thread-safe main thread dispatch via ConcurrentQueue
- `Packages/com.google.ai-embodiment/Runtime/SessionState.cs` - Session lifecycle state enum
- `Packages/com.google.ai-embodiment/CHANGELOG.md` - Keepachangelog format
- `Packages/com.google.ai-embodiment/LICENSE.md` - MIT license

## Decisions Made

- Firebase AI SDK gets its own asmdef (Firebase.AI) with `overrideReferences: true` and `precompiledReferences` listing the 4 Firebase DLLs (Google.MiniJson.dll, Firebase.App.dll, Firebase.Platform.dll, Firebase.TaskExtension.dll). This allows the UPM package's Runtime asmdef to reference Firebase.AI by name.
- UPM package placed under `Packages/` (not project root) so Unity auto-discovers it as an embedded package without requiring manifest.json edits.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- UPM package skeleton is ready for Plan 01-02 (PersonaConfig ScriptableObject, VoiceBackend enum, SystemInstructionBuilder)
- Firebase.AI assembly reference is established -- Plan 01-02 and 01-03 can import Firebase.AI types
- MainThreadDispatcher is ready for Plan 01-03 (PersonaSession receive loop marshaling)
- No blockers

---
*Phase: 01-foundation-and-core-session*
*Completed: 2026-02-05*
