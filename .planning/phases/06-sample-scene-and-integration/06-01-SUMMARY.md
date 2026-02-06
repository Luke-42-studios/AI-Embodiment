---
phase: 06-sample-scene-and-integration
plan: 01
subsystem: ui
tags: [upm, ui-toolkit, uxml, uss, sample-scene, unity-package-manager]

# Dependency graph
requires:
  - phase: 01-project-scaffolding
    provides: UPM package structure with package.json and runtime asmdef
provides:
  - UPM sample entry "Aya Live Stream" visible in Package Manager
  - AyaLiveStream assembly definition referencing ai-embodiment, Firebase.AI, Input System
  - AyaPanel.uxml UI Toolkit layout with named elements for C# binding
  - AyaPanel.uss dark-themed styling with speaking indicator, message classes, push-to-talk states
affects: [06-02, 06-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "UPM Samples~ convention for importable sample assets"
    - "UI Toolkit UXML/USS separation for layout and styling"
    - "Named UXML elements as contract between layout and C# code"

key-files:
  created:
    - Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaLiveStream.asmdef
    - Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/UI/AyaPanel.uxml
    - Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/UI/AyaPanel.uss
  modified:
    - Packages/com.google.ai-embodiment/package.json

key-decisions:
  - "UXML element names (chat-log, persona-name, speaking-indicator, status-label, ptt-button) are the binding contract for Plan 02 C# scripts"
  - "USS uses border-color glow for speaking indicator since box-shadow is unsupported in UI Toolkit"

patterns-established:
  - "Samples~/AyaLiveStream/ folder pattern for UPM importable samples"
  - "UXML named elements as API contract between layout and C# code"

# Metrics
duration: 1min
completed: 2026-02-05
---

# Phase 6 Plan 01: Sample Scaffold Summary

**UPM sample entry, AyaLiveStream asmdef with three assembly references, and dark-themed UI Toolkit chat panel layout with UXML/USS**

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-05T23:58:35Z
- **Completed:** 2026-02-05T23:59:48Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Package Manager "Aya Live Stream" sample entry with Import button
- Assembly definition enabling sample scripts to reference ai-embodiment, Firebase.AI, and Input System
- Complete UI Toolkit layout with chat log ScrollView, speaking indicator, persona name, status, and push-to-talk button
- Dark-themed USS with distinct message colors (purple Aya, blue user, gray system) and interactive button states

## Task Commits

Each task was committed atomically:

1. **Task 1: Package config and sample asmdef** - `4acc05c` (feat)
2. **Task 2: UI Toolkit UXML layout and USS styling** - `b8ea7e1` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/package.json` - Added samples array with Aya Live Stream entry
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaLiveStream.asmdef` - Assembly definition with three references
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/UI/AyaPanel.uxml` - UI Toolkit chat panel layout with 8 named elements
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/UI/AyaPanel.uss` - Dark-themed styling with 14 selectors

## Decisions Made
- UXML element names (chat-log, persona-name, speaking-indicator, status-label, ptt-button) serve as the binding contract between layout and C# -- Plan 02 AyaChatUI.cs will use Q<>() queries against these names
- USS uses border-color for speaking indicator glow since box-shadow is unsupported in UI Toolkit

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Sample folder structure ready for Plan 02 C# scripts (AyaChatUI.cs, AyaBootstrap.cs)
- UXML element names documented as contract for Q<>() binding
- Plan 03 can create the Unity scene referencing these assets

---
*Phase: 06-sample-scene-and-integration*
*Completed: 2026-02-05*
