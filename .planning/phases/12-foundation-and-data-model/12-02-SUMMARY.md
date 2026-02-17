---
phase: 12-foundation-and-data-model
plan: 02
subsystem: data-model
tags: [ScriptableObject, ChatBotConfig, ChatMessage, Unity, C#, editor-script, migration]

# Dependency graph
requires:
  - phase: none
    provides: "First data model plan in v1.0 milestone; uses PersonaConfig pattern from package runtime"
provides:
  - "ChatBotConfig ScriptableObject for per-bot persona configuration"
  - "ChatMessage runtime data container for chat feed messages"
  - "Editor migration script creating 6 bot persona .asset files"
affects: [12-03-LivestreamUI, 13-ChatBotSystem, 14-NarrativeDirector]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ChatBotConfig ScriptableObject pattern (following PersonaConfig)"
    - "ChatMessage plain C# data class with factory methods"
    - "Editor-only asmdef for migration tooling"

key-files:
  created:
    - Assets/AyaLiveStream/ChatBotConfig.cs
    - Assets/AyaLiveStream/ChatMessage.cs
    - Assets/AyaLiveStream/Editor/AyaLiveStream.Editor.asmdef
    - Assets/AyaLiveStream/Editor/MigrateChatBotConfigs.cs
  modified: []

key-decisions:
  - "ChatMessage is a plain C# class (not MonoBehaviour/ScriptableObject) for lightweight runtime data flow"
  - "Editor-only asmdef isolates migration script from runtime assembly"

patterns-established:
  - "ChatBotConfig: ScriptableObject with Header/TextArea/Range attributes for Inspector editing"
  - "ChatMessage: plain data class with source constructor and static factory for user messages"
  - "Editor migration: MenuItem-triggered one-time asset creation with SetDirty + SaveAssets"

# Metrics
duration: 2min
completed: 2026-02-17
---

# Phase 12 Plan 02: ChatBotConfig, ChatMessage, and Migration Summary

**ChatBotConfig ScriptableObject with 6 distinct bot personas, ChatMessage runtime data model, and editor migration script for asset generation**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-17T21:02:50Z
- **Completed:** 2026-02-17T21:04:37Z
- **Tasks:** 2
- **Files created:** 4

## Accomplishments
- ChatBotConfig ScriptableObject with Inspector-editable identity, personality, scripted messages, and behavior settings
- ChatMessage lightweight data class with bot-source constructor and static FromUser factory
- Editor migration script that creates 6 fully-populated ChatBotConfig .asset files via menu item
- Editor-only assembly definition isolating migration tooling from runtime

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ChatBotConfig ScriptableObject and ChatMessage data model** - `17d1b1a` (feat)
2. **Task 2: Create editor migration script and generate ChatBotConfig assets** - `ef25d85` (feat)

## Files Created/Modified
- `Assets/AyaLiveStream/ChatBotConfig.cs` - ScriptableObject defining chat bot persona (identity, personality, scripted messages, behavior)
- `Assets/AyaLiveStream/ChatMessage.cs` - Runtime data container for chat messages with bot and user factories
- `Assets/AyaLiveStream/Editor/AyaLiveStream.Editor.asmdef` - Editor-only assembly definition referencing runtime asmdef
- `Assets/AyaLiveStream/Editor/MigrateChatBotConfigs.cs` - One-time menu script creating 6 ChatBotConfig assets

## Decisions Made
- ChatMessage is a plain C# class (not MonoBehaviour or ScriptableObject) because it is a runtime data container that flows between systems, not a persisted asset
- Created a separate editor-only assembly definition for the migration script to keep UnityEditor references out of the runtime assembly
- Bot persona data is hardcoded in the migration script since nevatars source assets do not exist in this repository (confirmed by research)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Note: Migration Must Be Run in Unity
The migration script creates .asset files when run from Unity's menu: **AI Embodiment > Samples > Migrate Chat Bot Configs**. This menu item must be invoked once in the Unity Editor to generate the 6 ChatBotConfig .asset files in `Assets/AyaLiveStream/ChatBotConfigs/`. The .asset files are not committed to source control since they are generated artifacts.

## Next Phase Readiness
- ChatBotConfig and ChatMessage types are ready for Plan 03 (LivestreamUI ListView binding)
- ChatBotConfig assets will be available after running the migration menu item in Unity
- Phase 13 (Chat Bot System) can reference ChatBotConfig for scheduling and ChatMessage for message flow
- No blockers or concerns

---
*Phase: 12-foundation-and-data-model*
*Completed: 2026-02-17*
