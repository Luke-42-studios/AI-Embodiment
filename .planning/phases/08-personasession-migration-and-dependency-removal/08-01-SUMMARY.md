---
phase: 08-personasession-migration-and-dependency-removal
plan: 01
subsystem: infra
tags: [firebase-removal, scriptableobject, newtonsoft-json, asmdef, api-key]

# Dependency graph
requires:
  - phase: 07
    provides: GeminiLiveClient, Newtonsoft.Json package dependency
provides:
  - AIEmbodimentSettings ScriptableObject for API key management
  - Firebase-free runtime code (zero Firebase.AI references)
  - FunctionRegistry with handler-only registration (Phase 10 ready)
  - ChirpTTSClient using Newtonsoft.Json
  - PersonaSession stubbed for GeminiLiveClient integration
affects: [phase-08-plan-02, phase-09, phase-10, phase-11]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ScriptableObject singleton via Resources.Load for project-wide settings"
    - "Password-masked API key field with EditorGUILayout.PasswordField"

key-files:
  created:
    - "Packages/com.google.ai-embodiment/Runtime/AIEmbodimentSettings.cs"
    - "Packages/com.google.ai-embodiment/Editor/AIEmbodimentSettingsEditor.cs"
  modified:
    - "Packages/com.google.ai-embodiment/Runtime/com.google.ai-embodiment.asmdef"
    - "Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs"
    - "Packages/com.google.ai-embodiment/Runtime/FunctionRegistry.cs"
    - "Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs"
    - "Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs"
    - "Packages/com.google.ai-embodiment/Runtime/GoalManager.cs"
    - "Assets/AyaLiveStream/AyaSampleController.cs"
    - "Assets/AyaLiveStream/AyaLiveStream.asmdef"
    - "Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs"
    - "Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaLiveStream.asmdef"

key-decisions:
  - "FunctionRegistry.Register takes (name, handler) only -- declaration parameter deferred to Phase 10"
  - "PersonaSession method bodies stubbed with TODO comments rather than deleted -- preserves class structure for Plan 02 rewrite"

patterns-established:
  - "AIEmbodimentSettings: ScriptableObject singleton via Resources.Load for API key"
  - "Handler-only function registration (Phase 10 adds declarations back)"

# Metrics
duration: 5min
completed: 2026-02-13
---

# Phase 8 Plan 1: Firebase Purge and Dependency Swap Summary

**Deleted Firebase SDK (149 files), created AIEmbodimentSettings with password-masked editor, migrated all runtime code to zero Firebase references**

## Performance

- **Duration:** 5 min
- **Started:** 2026-02-13T19:29:01Z
- **Completed:** 2026-02-13T19:34:08Z
- **Tasks:** 2
- **Files modified:** 14 (2 created, 12 modified/deleted)

## Accomplishments
- Deleted Assets/Firebase/ (119 files) and Assets/ExternalDependencyManager/ (30 files) from disk
- Created AIEmbodimentSettings ScriptableObject with Resources.Load singleton and CreateAssetMenu
- Created AIEmbodimentSettingsEditor with password-masked API key field, Show/Hide toggle, and empty-key warning
- Removed Firebase.AI from all 3 asmdef files (runtime, Assets sample, Samples~)
- Migrated SystemInstructionBuilder to return string instead of ModelContent
- Rewrote FunctionRegistry to handler-only registration (no FunctionDeclaration/Tool types)
- Migrated ChirpTTSClient from MiniJSON to Newtonsoft.Json JObject
- Updated AyaSampleController (both locations) to 2-parameter RegisterFunction calls
- Stubbed PersonaSession for compilation: replaced LiveSession with GeminiLiveClient field, stubbed Firebase methods

## Task Commits

Each task was committed atomically:

1. **Task 1: Delete Firebase directories, create AIEmbodimentSettings, clean asmdef files** - `183e5d0` (feat)
2. **Task 2: Migrate SystemInstructionBuilder, FunctionRegistry, ChirpTTSClient, sample scene, and stub PersonaSession** - `b142d7b` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/AIEmbodimentSettings.cs` - ScriptableObject singleton for API key via Resources.Load
- `Packages/com.google.ai-embodiment/Editor/AIEmbodimentSettingsEditor.cs` - Custom inspector with password-masked API key and reveal toggle
- `Packages/com.google.ai-embodiment/Runtime/com.google.ai-embodiment.asmdef` - Removed Firebase.AI reference
- `Assets/AyaLiveStream/AyaLiveStream.asmdef` - Removed Firebase.AI reference
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaLiveStream.asmdef` - Removed Firebase.AI reference
- `Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs` - Returns string, removed Firebase.AI using
- `Packages/com.google.ai-embodiment/Runtime/FunctionRegistry.cs` - Handler-only registration, removed FunctionDeclaration/Tool types
- `Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs` - Newtonsoft.Json JObject replaces MiniJSON
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` - Stubbed Firebase methods, GeminiLiveClient field placeholder
- `Packages/com.google.ai-embodiment/Runtime/GoalManager.cs` - Cleaned Firebase comment
- `Assets/AyaLiveStream/AyaSampleController.cs` - 2-parameter RegisterFunction calls
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs` - 2-parameter RegisterFunction calls

## Decisions Made
- FunctionRegistry.Register takes (name, handler) only -- function declaration parameter deferred to Phase 10 with WebSocket-native type (JObject schema)
- PersonaSession method bodies stubbed with TODO comments rather than deleted -- preserves public API surface and class structure for Plan 02 GeminiLiveClient rewrite
- GoalManager Firebase comment cleaned as incidental fix (Rule 1 category but trivially minor)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Cleaned stale Firebase reference in GoalManager.cs comment**
- **Found during:** Task 2 (comprehensive Firebase reference search)
- **Issue:** GoalManager.cs class doc comment mentioned "no Firebase dependencies" -- stale after this migration
- **Fix:** Removed "Firebase" from the comment
- **Files modified:** Packages/com.google.ai-embodiment/Runtime/GoalManager.cs
- **Verification:** grep confirms zero Firebase references in all .cs files
- **Committed in:** b142d7b (part of Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 trivial comment cleanup)
**Impact on plan:** No scope creep. Comment cleanup was necessary for the "zero Firebase references" success criteria.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All Firebase references purged from runtime code and asmdef files
- AIEmbodimentSettings ready for PersonaSession.Connect() to load API key
- PersonaSession stubbed and ready for Plan 02 (GeminiLiveClient rewire)
- FunctionRegistry ready for Phase 10 to add declaration types back
- ChirpTTSClient fully migrated to Newtonsoft.Json

---
*Phase: 08-personasession-migration-and-dependency-removal*
*Completed: 2026-02-13*
