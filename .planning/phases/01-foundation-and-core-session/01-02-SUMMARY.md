---
phase: 01-foundation-and-core-session
plan: 02
subsystem: config
tags: [unity, scriptableobject, persona, firebase, modelcontent, voice-backend]

# Dependency graph
requires:
  - phase: 01-01
    provides: "UPM package skeleton with Runtime asmdef referencing Firebase.AI"
provides:
  - "PersonaConfig ScriptableObject with personality, model, and voice configuration"
  - "VoiceBackend enum for voice synthesis path selection"
  - "SystemInstructionBuilder converting PersonaConfig into Firebase ModelContent"
affects: [01-03, 04-03, 05-02]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ScriptableObject with CreateAssetMenu for developer-facing configuration"
    - "Static builder pattern: SystemInstructionBuilder.Build(config) converts domain types to SDK types at boundary"

key-files:
  created:
    - "Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs"
    - "Packages/com.google.ai-embodiment/Runtime/VoiceBackend.cs"
    - "Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs"
  modified: []

key-decisions:
  - "PersonaConfig is a pure Unity ScriptableObject with no Firebase imports -- Firebase boundary is only in SystemInstructionBuilder"
  - "VoiceBackend enum in separate file for independent referencing by PersonaSession and future ChirpTTSClient"

patterns-established:
  - "Firebase SDK boundary: only SystemInstructionBuilder and PersonaSession touch Firebase types; config layer stays pure Unity"
  - "CreateAssetMenu convention: AI Embodiment/ submenu for all package ScriptableObjects"

# Metrics
duration: 1min
completed: 2026-02-05
---

# Phase 1 Plan 2: PersonaConfig, VoiceBackend, and SystemInstructionBuilder Summary

**PersonaConfig ScriptableObject with Inspector-editable personality/model/voice fields, VoiceBackend enum, and SystemInstructionBuilder converting config to Firebase ModelContent system instruction**

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-05T18:51:52Z
- **Completed:** 2026-02-05T18:52:59Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- PersonaConfig ScriptableObject with 4 field sections (Identity, Personality, Model, Voice) creatable via Assets > Create > AI Embodiment > Persona Config
- VoiceBackend enum with GeminiNative and ChirpTTS values for voice synthesis path selection
- SystemInstructionBuilder composes persona fields into formatted ModelContent system instruction with null-safe section handling
- Clean Firebase SDK boundary: only SystemInstructionBuilder imports Firebase.AI; PersonaConfig stays pure Unity

## Task Commits

Each task was committed atomically:

1. **Task 1: PersonaConfig ScriptableObject and VoiceBackend enum** - `7f755c2` (feat)
2. **Task 2: SystemInstructionBuilder** - `139e077` (feat)

## Files Created/Modified

- `Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs` - ScriptableObject with persona personality, model, and voice configuration
- `Packages/com.google.ai-embodiment/Runtime/VoiceBackend.cs` - Enum for voice backend selection (GeminiNative, ChirpTTS)
- `Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs` - Static builder composing PersonaConfig into ModelContent system instruction

## Decisions Made

- PersonaConfig has no Firebase imports -- the Firebase boundary is solely in SystemInstructionBuilder and (future) PersonaSession. This keeps the config layer testable without Firebase SDK.
- VoiceBackend enum placed in its own file because other classes (PersonaSession, future ChirpTTSClient) reference it independently of PersonaConfig.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- PersonaConfig is ready for Plan 01-03 (PersonaSession assigns PersonaConfig, calls SystemInstructionBuilder.Build at connect time)
- VoiceBackend is ready for Plan 01-03 (PersonaSession reads voiceBackend to configure LiveGenerationConfig)
- SystemInstructionBuilder is ready for Plan 01-03 (called during Connect() to generate system instruction)
- No blockers

---
*Phase: 01-foundation-and-core-session*
*Completed: 2026-02-05*
