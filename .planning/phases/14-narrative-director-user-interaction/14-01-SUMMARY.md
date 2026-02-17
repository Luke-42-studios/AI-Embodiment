---
phase: 14-narrative-director-user-interaction
plan: 01
subsystem: narrative
tags: [scriptableobject, narrative-beats, director-notes, unity-inspector, enums]

# Dependency graph
requires:
  - phase: 13-chat-bot-system
    provides: ChatBotConfig pattern for ScriptableObject authoring and Editor script conventions
provides:
  - NarrativeBeatConfig ScriptableObject type
  - NarrativeSceneConfig serializable class
  - SceneType enum (5 values)
  - ConditionType enum (3 values)
  - GoalUrgency enum (3 values)
  - 3 beat assets via editor menu (warm-up, art process, characters)
affects: [14-02 NarrativeDirector, 14-03 user interaction, 14-04 integration, 15-scene-transitions, 16-experience-loop]

# Tech tracking
tech-stack:
  added: []
  patterns: [NarrativeBeatConfig SO for beat authoring, editor menu for asset generation, GoalUrgency escalation pattern]

key-files:
  created:
    - Assets/AyaLiveStream/NarrativeBeatConfig.cs
    - Assets/AyaLiveStream/Editor/CreateBeatAssets.cs
  modified: []

key-decisions:
  - "Editor script approach over raw YAML .asset files (avoids GUID issues, follows MigrateChatBotConfigs.cs pattern)"
  - "Non-conditional scenes use isConditional=false with conditionType=Always (advance when done, no waiting)"
  - "GoalUrgency enum with Low/Medium/High values for escalating narrative intensity across beats"

patterns-established:
  - "NarrativeBeatConfig: Inspector-authored beat definitions with time budget, urgency, director notes, and ordered scenes"
  - "CreateBeatAssets: editor menu for one-time asset generation matching MigrateChatBotConfigs pattern"

# Metrics
duration: 2min
completed: 2026-02-17
---

# Phase 14 Plan 01: Narrative Beat Data Model Summary

**NarrativeBeatConfig SO with 3 enums (SceneType, ConditionType, GoalUrgency), NarrativeSceneConfig serializable class, and editor script generating 3 beat assets with escalating urgency (Low/Medium/High)**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-17T23:41:21Z
- **Completed:** 2026-02-17T23:43:27Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- NarrativeBeatConfig ScriptableObject with full Inspector-friendly field set (beatId, title, timeBudget, urgency, goalDescription, directorNote, scenes, slowChatDuringAya, skipKeywords)
- 3 enums: SceneType (5 values), ConditionType (3 values), GoalUrgency (3 values) -- simplified from nevatars 6/8/0
- NarrativeSceneConfig serializable class with scene type, dialogue alternatives, conditional transitions, and Phase 15 action placeholder
- Editor script creating 3 beat assets: Warm-Up (Low, 180s), Art Process (Medium, 240s), Characters (High, 180s)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create NarrativeBeatConfig ScriptableObject and NarrativeSceneConfig data model** - `99e8e8b` (feat)
2. **Task 2: Author 3 beat ScriptableObject assets** - `393d781` (feat)

## Files Created/Modified
- `Assets/AyaLiveStream/NarrativeBeatConfig.cs` - NarrativeBeatConfig SO, NarrativeSceneConfig class, SceneType/ConditionType/GoalUrgency enums
- `Assets/AyaLiveStream/Editor/CreateBeatAssets.cs` - Editor menu script generating 3 beat assets in Assets/AyaLiveStream/Data/

## Decisions Made
- **Editor script over raw YAML**: Used ScriptableObject.CreateInstance + AssetDatabase.CreateAsset approach instead of handwriting .asset YAML files. This avoids MonoScript GUID issues and follows the established MigrateChatBotConfigs.cs pattern.
- **Non-conditional scene defaults**: Scenes that advance immediately use isConditional=false with conditionType=Always, keeping the transition logic simple for NarrativeDirector.
- **GoalUrgency as separate enum**: Created a dedicated 3-value enum (Low, Medium, High) rather than using numeric values, making beats self-documenting in the Inspector.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

After opening the Unity project, run the editor menu item: **AI Embodiment > Samples > Create Demo Beat Assets** to generate the 3 beat .asset files in Assets/AyaLiveStream/Data/.

## Next Phase Readiness
- NarrativeBeatConfig type is available for NarrativeDirector (14-02) to reference
- GoalUrgency enum ready for director note phrasing and beat pacing logic
- 3 beat assets define the complete 10-minute narrative arc content
- All scene types and condition types ready for scene orchestration implementation

---
*Phase: 14-narrative-director-user-interaction*
*Completed: 2026-02-17*
