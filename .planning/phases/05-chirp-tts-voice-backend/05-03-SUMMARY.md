---
phase: 05-chirp-tts-voice-backend
plan: 03
subsystem: audio
tags: [chirp-tts, voice-routing, synthesis-orchestration, persona-session, gemini-live]

# Dependency graph
requires:
  - phase: 05-chirp-tts-voice-backend
    provides: "ChirpTTSClient HTTP client (05-01), PersonaConfig Chirp fields and ChirpSynthesisMode enum (05-02)"
  - phase: 02-audio-pipeline
    provides: "AudioPlayback with EnqueueAudio ring buffer, AudioCapture push-to-talk"
  - phase: 03-sync-protocol
    provides: "PacketAssembler sentence-boundary SyncPacket emission, SyncPacket struct"
  - phase: 04-function-calling
    provides: "FunctionRegistry, GoalManager, DispatchFunctionCall in PersonaSession"
provides:
  - "Chirp TTS voice path in PersonaSession: Gemini audio discarded, transcription text drives Cloud TTS synthesis"
  - "Sentence-by-sentence synthesis mode via HandleSyncPacket"
  - "Full-response synthesis mode via TurnComplete text accumulation"
  - "End-to-end Chirp TTS integration: config, client, routing all wired together"
affects: [06-sample-scene-and-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [backend-gated-audio-routing, async-void-fire-and-forget-synthesis, dual-text-buffer-pattern]

key-files:
  created: []
  modified:
    - Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs

key-decisions:
  - "Gemini Live stays in audio mode (ResponseModality.Audio) for both backends -- native audio discarded for Chirp path"
  - "AI speaking events driven by Chirp synthesis lifecycle (SynthesizeAndEnqueue) not Gemini audio arrival for ChirpTTS"
  - "SynthesizeAndEnqueue is async void (fire-and-forget from main thread callback context) with internal error handling"
  - "Chirp text buffer (_chirpTextBuffer) is separate from PacketAssembler text buffer -- each serves different purpose"
  - "ChirpTTSClient.SynthesizeAsync receives chirpVoiceShortName (not full API name) for standard voices, customVoiceName for custom"

patterns-established:
  - "Backend-gated audio routing: VoiceBackend enum check gates audio data flow at each routing decision point"
  - "Dual synthesis modes via config enum: SentenceBySentence triggers in HandleSyncPacket, FullResponse triggers on TurnComplete"
  - "Silent-skip error pattern: TTS failure fires OnError but conversation continues with text-only display"

# Metrics
duration: 2min
completed: 2026-02-05
---

# Phase 5 Plan 03: PersonaSession Chirp TTS Integration Summary

**PersonaSession routes AI output transcription through ChirpTTSClient with backend-gated audio routing, sentence-by-sentence and full-response synthesis modes, and silent-skip error handling**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-05T23:05:29Z
- **Completed:** 2026-02-05T23:07:30Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Gated Gemini native audio routing on VoiceBackend.GeminiNative so ChirpTTS path discards native audio while keeping turn detection for PacketAssembler
- Sentence-by-sentence mode synthesizes each SyncPacket text via HandleSyncPacket triggering SynthesizeAndEnqueue
- Full-response mode accumulates output transcription text in _chirpTextBuffer, synthesizes once on TurnComplete
- ChirpTTSClient lifecycle managed: initialized in Connect() with Firebase API key, disposed in Disconnect() and OnDestroy()
- AI speaking events driven by Chirp synthesis lifecycle rather than Gemini audio arrival for accurate state tracking
- Interrupt handling clears Chirp text buffer to prevent stale synthesis after barge-in

## Task Commits

Each task was committed atomically:

1. **Task 1: PersonaSession Chirp TTS integration** - `98d619b` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` - Added ChirpTTSClient field, _chirpTextBuffer, backend-gated audio routing in ProcessResponse, SynthesizeAndEnqueue async method, HandleChirpError, Chirp client init in Connect(), disposal in Disconnect()/OnDestroy(), text buffer clearing on interruption

## Decisions Made
- [05-03]: Gemini Live stays in audio mode (ResponseModality.Audio) even for ChirpTTS backend -- native audio model rejects text-only mode (Research Pitfall 2), so audio is produced and discarded
- [05-03]: AI speaking events for ChirpTTS driven by SynthesizeAndEnqueue (first synthesis sets _aiSpeaking) rather than Gemini audio arrival -- ensures events reflect actual audio playback timing
- [05-03]: SynthesizeAndEnqueue is async void (fire-and-forget from callback context) because HandleSyncPacket is synchronous callback from PacketAssembler; errors caught internally and forwarded via OnError
- [05-03]: Chirp text buffer (_chirpTextBuffer) separate from PacketAssembler -- PacketAssembler handles sentence segmentation for SyncPackets, _chirpTextBuffer accumulates ALL text for full-response synthesis
- [05-03]: SynthesizeAndEnqueue passes chirpVoiceShortName for standard voices, customVoiceName for custom -- ChirpTTSClient handles full API name construction internally

## Deviations from Plan

None -- plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None -- no external service configuration required. (Note: developers need Cloud Text-to-Speech API enabled in Google Cloud Console when using Chirp backend, as documented in ChirpTTSClient error messages.)

## Next Phase Readiness
- Phase 5 (Chirp TTS Voice Backend) is now complete: client, config/editor, and session routing all integrated
- Full end-to-end Chirp TTS path ready: PersonaConfig selects backend, ChirpTTSClient synthesizes, AudioPlayback plays back
- Ready for Phase 6 (Sample Scene and Integration) which will demonstrate both voice backends

---
*Phase: 05-chirp-tts-voice-backend*
*Completed: 2026-02-05*
