---
phase: 09-tts-abstraction
plan: 02
subsystem: tts
tags: [ITTSProvider, PersonaSession, TTSResult, TTSSynthesisMode, VoiceBackend, Custom, SetTTSProvider, PersonaConfigEditor]

# Dependency graph
requires:
  - phase: 09-01
    provides: "ITTSProvider interface, TTSResult struct, TTSSynthesisMode enum, ChirpTTSClient implementing ITTSProvider, PersonaConfig.CustomTTSProvider"
provides:
  - "PersonaSession provider-agnostic TTS routing via _ttsProvider (ITTSProvider)"
  - "SetTTSProvider(ITTSProvider) public API for code-based provider registration"
  - "PersonaConfigEditor Custom backend UI with MonoBehaviour slot + ITTSProvider validation"
  - "Synthesis mode display for both ChirpTTS and Custom backends"
affects: [10-function-calling, 11-integration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Provider-agnostic TTS routing: _ttsProvider null check instead of VoiceBackend enum switch"
    - "TTSResult SampleRate mismatch warning for cross-provider audio format safety"
    - "Inspector ITTSProvider validation via MonoBehaviour cast at edit time"

key-files:
  created: []
  modified:
    - "Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs"
    - "Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs"

key-decisions:
  - "HandleAudioEvent uses _ttsProvider == null instead of VoiceBackend.GeminiNative check -- decouples routing from enum"
  - "SynthesizeAndEnqueue warns on SampleRate != 24000 rather than failing -- allows providers returning different rates"
  - "SetTTSProvider uses Debug.LogError + early return (not exception) for session-active guard -- Unity convention"

patterns-established:
  - "_ttsProvider null = native audio path, non-null = provider handles synthesis"
  - "SetTTSProvider must be called before Connect() -- provider fixed for session lifetime"

# Metrics
duration: 3min
completed: 2026-02-13
---

# Phase 9 Plan 2: PersonaSession TTS Routing and Custom Backend UI Summary

**Provider-agnostic TTS routing in PersonaSession via ITTSProvider with SetTTSProvider API and Inspector Custom backend UI with ITTSProvider validation**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-13T20:33:02Z
- **Completed:** 2026-02-13T20:36:17Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Replaced all _chirpClient/ChirpTTSClient-specific code in PersonaSession with provider-agnostic _ttsProvider (ITTSProvider)
- Added SetTTSProvider(ITTSProvider) public API with session state guard for code-based provider registration
- SynthesizeAndEnqueue now returns TTSResult with SampleRate mismatch warning for cross-provider safety
- PersonaConfigEditor supports all three VoiceBackend values (GeminiNative, ChirpTTS, Custom)
- Custom backend shows MonoBehaviour slot with ITTSProvider validation and synthesis mode selection
- Zero _chirpClient, ChirpSynthesisMode, HandleChirpError, or MainThreadDispatcher references remain

## Task Commits

Each task was committed atomically:

1. **Task 1: Generalize PersonaSession TTS routing from ChirpTTSClient to ITTSProvider** - `e61f1d2` (feat)
2. **Task 2: Update PersonaConfigEditor for Custom backend UI and TTSSynthesisMode** - `7b3f28d` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` - Provider-agnostic TTS routing via _ttsProvider, SetTTSProvider API, TTSResult handling
- `Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs` - Custom backend UI with MonoBehaviour slot, ITTSProvider validation, synthesis mode for ChirpTTS and Custom

## Decisions Made
- HandleAudioEvent uses `_ttsProvider == null` instead of `VoiceBackend.GeminiNative` check -- decouples audio routing from the enum value, making it work correctly even if Custom backend has no provider assigned
- SynthesizeAndEnqueue warns on SampleRate != 24000 rather than failing -- allows providers returning different sample rates while alerting developers to potential playback issues
- SetTTSProvider uses Debug.LogError + early return (not exception) for the session-active guard -- follows Unity convention where LogError is preferred over exceptions in MonoBehaviour methods

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 9 (TTS Abstraction) is now complete -- all ITTSProvider infrastructure in place
- PersonaSession routes TTS through any ITTSProvider (ChirpTTS or Custom), with Inspector fully supporting all three voice backends
- Ready for Phase 10 (Function Calling and Goals Migration) or Phase 11 (Integration Verification)

---
*Phase: 09-tts-abstraction*
*Completed: 2026-02-13*
