# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-13)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** v0.8 WebSocket Migration -- Phase 9: TTS Abstraction

## Current Position

Phase: 9 of 11 (TTS Abstraction)
Plan: 1 of 2 in current phase
Status: In progress
Last activity: 2026-02-13 -- Completed 09-01-PLAN.md

Progress: [##################░░] 93% (22/28 total plans -- 17 v1 complete, 5 v0.8 complete, 6 v0.8 pending)

## Performance Metrics

**Velocity:**
- Total plans completed: 21
- Average duration: 1.7 min
- Total execution time: 0.67 hours

## Accumulated Context

### Decisions

Full decision log in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- v0.8: Direct WebSocket to Gemini Live (replacing Firebase AI Logic SDK)
- v0.8: Newtonsoft.Json replaces MiniJSON for serialization
- v0.8: Audio-only Gemini models (gemini-2.5-flash-native-audio) as successor to 2.0-flash
- v0.8: API key in AIEmbodimentSettings ScriptableObject (Resources.Load singleton)
- v0.8: ITTSProvider interface for TTS backend abstraction
- 09-01: voiceCloningKey moved from SynthesizeAsync to ChirpTTSClient constructor
- 09-01: OnError event removed from ChirpTTSClient -- exceptions are the error mechanism
- 09-01: onAudioChunk callback parameter is forward-compatible slot (ignored by REST providers)
- 07-01: realtimeInput.audio (non-deprecated) replaces mediaChunks from reference implementation
- 07-02: Audio PCM-to-float conversion done in HandleJsonMessage for direct AudioPlayback compatibility
- 07-02: JSON detection via first-byte check (Gemini sends all as Binary WebSocket frames)
- 08-01: FunctionRegistry.Register takes (name, handler) only -- declaration deferred to Phase 10
- 08-02: Connected state set by HandleGeminiEvent on setupComplete, not in Connect()
- 08-02: Disconnect() and SendText() are synchronous (not async void)
- 08-02: FunctionCall events pass null callId -- Phase 10 adds FunctionId to GeminiEvent

### Pending Todos

None.

### Blockers/Concerns

- Gemini output audio sample rate assumed 24kHz -- verify with actual API response
- Scene file wiring gaps on disk (Editor state not saved from v1)

## Session Continuity

Last session: 2026-02-13
Stopped at: Completed 09-01-PLAN.md
Resume file: None
