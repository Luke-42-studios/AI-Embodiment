# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-13)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** v0.8 WebSocket Migration -- Phase 8: PersonaSession Migration and Dependency Removal

## Current Position

Phase: 8 of 11 (PersonaSession Migration and Dependency Removal)
Plan: 1 of 2 in current phase
Status: In progress
Last activity: 2026-02-13 -- Completed 08-01-PLAN.md

Progress: [#################░░░] 86% (20/28 total plans -- 17 v1 complete, 3 v0.8 complete, 8 v0.8 pending)

## Performance Metrics

**Velocity:**
- Total plans completed: 20
- Average duration: 1.7 min
- Total execution time: 0.60 hours

## Accumulated Context

### Decisions

Full decision log in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- v0.8: Direct WebSocket to Gemini Live (replacing Firebase AI Logic SDK)
- v0.8: Newtonsoft.Json replaces MiniJSON for serialization
- v0.8: Audio-only Gemini models (gemini-2.5-flash-native-audio) as successor to 2.0-flash
- v0.8: API key in AIEmbodimentSettings ScriptableObject (Resources.Load singleton)
- v0.8: ITTSProvider interface for TTS backend abstraction
- 07-01: realtimeInput.audio (non-deprecated) replaces mediaChunks from reference implementation
- 07-02: Audio PCM-to-float conversion done in HandleJsonMessage for direct AudioPlayback compatibility
- 07-02: JSON detection via first-byte check (Gemini sends all as Binary WebSocket frames)
- 08-01: FunctionRegistry.Register takes (name, handler) only -- declaration deferred to Phase 10
- 08-01: PersonaSession stubbed for compilation, Plan 02 does full GeminiLiveClient rewrite

### Pending Todos

None.

### Blockers/Concerns

- Gemini output audio sample rate assumed 24kHz -- verify with actual API response
- Scene file wiring gaps on disk (Editor state not saved from v1)

## Session Continuity

Last session: 2026-02-13
Stopped at: Completed 08-01-PLAN.md (Firebase purge and dependency swap)
Resume file: None
