---
phase: 05-chirp-tts-voice-backend
plan: 02
subsystem: ui
tags: [unity-editor, custom-inspector, chirp-tts, scriptableobject, dropdown]

# Dependency graph
requires:
  - phase: 01-foundation-and-core-session
    provides: PersonaConfig ScriptableObject and VoiceBackend enum
provides:
  - Extended PersonaConfig with Chirp TTS language, voice, synthesis mode, and custom voice fields
  - PersonaConfigEditor with conditional Inspector UI for Chirp backend configuration
affects: [05-chirp-tts-voice-backend, 06-sample-scene-and-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [custom-editor-conditional-visibility, serialized-property-popup-sync]

key-files:
  created:
    - Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs
  modified:
    - Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs

key-decisions:
  - "ChirpSynthesisMode enum in same file as PersonaConfig -- collocated with the config that uses it"
  - "chirpVoiceName auto-synced by Editor from language + short name -- runtime code reads full API name directly"
  - "IsCustomChirpVoice convenience property on PersonaConfig references ChirpVoiceList.CustomVoice constant"
  - "Voice dropdown shows all 30 voices regardless of language -- Chirp 3 HD voices work across all locales"

patterns-established:
  - "Custom Editor conditional visibility: SerializedProperty cached in OnEnable, enum-based branching in OnInspectorGUI"
  - "Auto-sync derived field: Editor writes computed chirpVoiceName from language + voice, runtime reads it directly"

# Metrics
duration: 2min
completed: 2026-02-05
---

# Phase 5 Plan 02: PersonaConfig Chirp Fields and Inspector Editor Summary

**PersonaConfig extended with 6 Chirp TTS fields and custom Inspector with language/voice dropdowns, custom voice support, and synthesis mode selection**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-05T23:00:37Z
- **Completed:** 2026-02-05T23:02:14Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Extended PersonaConfig with chirpLanguageCode, chirpVoiceShortName, chirpVoiceName, chirpSynthesisMode, customVoiceName, and voiceCloningKey fields
- Created ChirpSynthesisMode enum with SentenceBySentence and FullResponse synthesis strategies
- Built PersonaConfigEditor with conditional field visibility: GeminiNative shows voice name, ChirpTTS shows language/voice dropdowns
- Custom voice fields (name + cloning key) appear only when "Custom" selected in voice dropdown

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend PersonaConfig with Chirp fields** - `4172005` (feat)
2. **Task 2: PersonaConfigEditor custom Inspector** - `35d4bf8` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs` - Added 6 Chirp TTS fields, ChirpSynthesisMode enum, IsCustomChirpVoice property
- `Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs` - Custom Inspector with conditional dropdowns and auto-sync

## Decisions Made
- ChirpSynthesisMode enum placed in same file as PersonaConfig (collocated with its consumer, same pattern as other small enums in the project)
- chirpVoiceName is auto-synced by the Editor whenever language or voice short name changes -- runtime code reads the full API name directly without needing ChirpVoiceList
- Voice dropdown shows all 30 Chirp 3 HD voices for every language (voices are cross-locale compatible)
- Default index fallback to 0 if stored value doesn't match any dropdown entry (graceful handling of invalid serialized data)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- PersonaConfig Chirp fields ready for Plan 03 (PersonaSession Chirp TTS routing)
- PersonaConfigEditor requires ChirpVoiceList.cs from Plan 01 to compile -- both are Wave 1 and code compiles once both complete
- ChirpSynthesisMode enum available for Plan 03 synthesis mode branching

---
*Phase: 05-chirp-tts-voice-backend*
*Completed: 2026-02-05*
