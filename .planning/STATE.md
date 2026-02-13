# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-13)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** v0.8 WebSocket Migration -- Phase 10 complete and verified. Ready for Phase 11.

## Current Position

Phase: 10 of 11 (Function Calling and Goals Migration)
Plan: 2 of 2 in current phase
Status: Phase complete and verified
Last activity: 2026-02-13 -- Phase 10 verified (10/10 must-haves passed)

Progress: [###################░] 96% (25/26 total plans -- 17 v1 complete, 8 v0.8 complete, 1 v0.8 pending)

## Performance Metrics

**Velocity:**
- Total plans completed: 25
- Average duration: 1.8 min
- Total execution time: 0.82 hours

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
- 09-02: HandleAudioEvent uses _ttsProvider == null instead of VoiceBackend enum check -- decoupled routing
- 09-02: SetTTSProvider uses Debug.LogError + early return (Unity convention) for session-active guard
- 07-01: realtimeInput.audio (non-deprecated) replaces mediaChunks from reference implementation
- 07-02: Audio PCM-to-float conversion done in HandleJsonMessage for direct AudioPlayback compatibility
- 07-02: JSON detection via first-byte check (Gemini sends all as Binary WebSocket frames)
- 08-02: Connected state set by HandleGeminiEvent on setupComplete, not in Connect()
- 08-02: Disconnect() and SendText() are synchronous (not async void)
- 10-01: FunctionDeclaration fluent builder with inner ParameterDef class (flat primitives only)
- 10-01: FunctionRegistry.Register takes (name, declaration, handler) -- declaration is required
- 10-01: BuildToolsJson/BuildPromptInstructions return null when no registrations
- 10-01: GeminiEvent.FunctionId captures call ID for response correlation
- 10-01: SendToolResponse uses IDictionary<string,object> converted via JObject.FromObject
- 10-02: UseNativeFunctionCalling is public static (global toggle, not per-session)
- 10-02: Prompt-based [CALL:] tags left in transcription events (stripping breaks incremental stream)
- 10-02: Prompt-based calls fire-and-forget only (no server-assigned ID)
- 10-02: SendGoalUpdate uses Debug.Log not LogWarning (expected behavior, not a warning)
- 10-02: SystemInstructionBuilder 3-param overload centralizes all instruction composition

### Pending Todos

None.

### Blockers/Concerns

- Gemini output audio sample rate assumed 24kHz -- verify with actual API response
- Scene file wiring gaps on disk (Editor state not saved from v1)

## Session Continuity

Last session: 2026-02-13
Stopped at: Phase 10 complete and verified. Ready to plan Phase 11.
Resume file: None
