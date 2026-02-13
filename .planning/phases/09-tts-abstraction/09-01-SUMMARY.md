---
phase: 09-tts-abstraction
plan: 01
subsystem: tts
tags: [ITTSProvider, TTSResult, TTSSynthesisMode, ChirpTTS, VoiceBackend, interface-abstraction]

# Dependency graph
requires:
  - phase: 08-persona-session-migration
    provides: "ChirpTTSClient with Newtonsoft.Json, PersonaSession using GeminiLiveClient"
provides:
  - "ITTSProvider interface with SynthesizeAsync returning Awaitable<TTSResult>"
  - "TTSResult readonly struct (Samples, SampleRate, Channels)"
  - "TTSSynthesisMode enum replacing ChirpSynthesisMode"
  - "VoiceBackend.Custom enum value"
  - "ChirpTTSClient implementing ITTSProvider"
  - "PersonaConfig.CustomTTSProvider property for developer-supplied providers"
affects: [09-02, 10-function-calling, 11-integration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ITTSProvider interface abstraction for pluggable TTS backends"
    - "TTSResult self-describing struct decouples providers from sample rate assumptions"
    - "MonoBehaviour field with runtime ITTSProvider cast for Unity Inspector integration"
    - "Provider-specific config (voiceCloningKey) at construction, not in interface methods"

key-files:
  created:
    - "Packages/com.google.ai-embodiment/Runtime/ITTSProvider.cs"
  modified:
    - "Packages/com.google.ai-embodiment/Runtime/VoiceBackend.cs"
    - "Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs"
    - "Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs"
    - "Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs"
    - "Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs"

key-decisions:
  - "voiceCloningKey moved from SynthesizeAsync parameter to ChirpTTSClient constructor"
  - "OnError event removed from ChirpTTSClient -- exceptions from SynthesizeAsync are the error mechanism"
  - "onAudioChunk callback accepted but ignored by ChirpTTSClient (REST non-streaming, forward-compatible slot)"

patterns-established:
  - "ITTSProvider : IDisposable pattern for TTS backend abstraction"
  - "TTSResult struct with HasAudio convenience property"
  - "MonoBehaviour _customTTSProvider field with ITTSProvider cast property"

# Metrics
duration: 3min
completed: 2026-02-13
---

# Phase 9 Plan 1: TTS Type Foundation Summary

**ITTSProvider interface, TTSResult struct, TTSSynthesisMode enum, VoiceBackend.Custom, and ChirpTTSClient refactored to implement the abstraction**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-13T20:27:10Z
- **Completed:** 2026-02-13T20:30:09Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Created ITTSProvider interface with SynthesizeAsync returning Awaitable<TTSResult>, extending IDisposable
- Created TTSResult readonly struct with self-describing audio metadata (Samples, SampleRate, Channels)
- Created TTSSynthesisMode enum as provider-agnostic replacement for ChirpSynthesisMode
- Extended VoiceBackend enum with Custom value for developer-supplied TTS providers
- Refactored ChirpTTSClient to implement ITTSProvider with TTSResult return type
- Added CustomTTSProvider MonoBehaviour slot to PersonaConfig for Inspector-based provider assignment
- Removed OnError event from ChirpTTSClient and all subscriptions in PersonaSession

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ITTSProvider interface, TTSResult struct, TTSSynthesisMode enum, and extend VoiceBackend** - `7fe8919` (feat)
2. **Task 2: Refactor ChirpTTSClient to implement ITTSProvider and update PersonaConfig** - `31b341d` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/ITTSProvider.cs` - NEW: ITTSProvider interface, TTSResult struct, TTSSynthesisMode enum
- `Packages/com.google.ai-embodiment/Runtime/VoiceBackend.cs` - Added Custom enum value
- `Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs` - Implements ITTSProvider, returns TTSResult, voiceCloningKey in constructor, OnError removed
- `Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs` - chirpSynthesisMode -> synthesisMode, ChirpSynthesisMode enum deleted, CustomTTSProvider property added
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` - TTSSynthesisMode references updated, HandleChirpError and OnError subscriptions removed
- `Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs` - Updated to use synthesisMode and TTSSynthesisMode

## Decisions Made
- voiceCloningKey moved from SynthesizeAsync parameter to ChirpTTSClient constructor (provider-specific config at construction, per CONTEXT.md)
- OnError event removed from ChirpTTSClient -- SynthesizeAsync throws exceptions, PersonaSession catches them in SynthesizeAndEnqueue
- onAudioChunk callback parameter accepted but ignored by ChirpTTSClient (REST is non-streaming; forward-compatible slot for future streaming providers)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated ChirpSynthesisMode references in PersonaSession.cs**
- **Found during:** Task 2 (after deleting ChirpSynthesisMode enum from PersonaConfig)
- **Issue:** PersonaSession.cs referenced `_config.chirpSynthesisMode == ChirpSynthesisMode.FullResponse` and `.SentenceBySentence` -- would not compile after enum deletion
- **Fix:** Replaced with `_config.synthesisMode == TTSSynthesisMode.FullResponse` and `.SentenceBySentence`
- **Files modified:** PersonaSession.cs
- **Verification:** grep for ChirpSynthesisMode in Runtime returns zero code matches
- **Committed in:** 31b341d (Task 2 commit)

**2. [Rule 3 - Blocking] Updated ChirpSynthesisMode references in PersonaConfigEditor.cs**
- **Found during:** Task 2 (after deleting ChirpSynthesisMode enum from PersonaConfig)
- **Issue:** Editor script referenced `_chirpSynthesisMode`, `"chirpSynthesisMode"`, and `ChirpSynthesisMode.` -- would not compile
- **Fix:** Renamed to `_synthesisMode`, `"synthesisMode"`, and `TTSSynthesisMode.`
- **Files modified:** PersonaConfigEditor.cs
- **Verification:** grep for ChirpSynthesisMode across entire package returns only a doc comment
- **Committed in:** 31b341d (Task 2 commit)

**3. [Rule 3 - Blocking] Removed HandleChirpError and OnError subscriptions from PersonaSession.cs**
- **Found during:** Task 2 (after removing OnError event from ChirpTTSClient)
- **Issue:** PersonaSession subscribed to `_chirpClient.OnError += HandleChirpError` and unsubscribed in two cleanup paths -- OnError property no longer exists on ChirpTTSClient
- **Fix:** Removed `HandleChirpError` method and all three OnError subscription/unsubscription lines
- **Files modified:** PersonaSession.cs
- **Verification:** grep for HandleChirpError and _chirpClient.OnError returns zero matches
- **Committed in:** 31b341d (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (3 blocking)
**Impact on plan:** All fixes necessary to prevent compilation errors from planned removals. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ITTSProvider type foundation complete, ready for 09-02-PLAN.md
- 09-02 will generalize PersonaSession TTS routing from _chirpClient to _ttsProvider, add SetTTSProvider API, and update PersonaConfigEditor for Custom backend UI
- PersonaSession still has _chirpClient field references (to be replaced with _ttsProvider in 09-02)

---
*Phase: 09-tts-abstraction*
*Completed: 2026-02-13*
