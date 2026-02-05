# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-05)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** Phase 4 complete. Next: Phase 5 - Chirp TTS Voice Backend

## Current Position

Phase: 4 of 6 (Function Calling and Conversational Goals) -- COMPLETE
Plan: 3 of 3 in phase 4 (complete)
Status: Phase complete
Last activity: 2026-02-05 -- Completed 04-03-PLAN.md

Progress: [████████░░] 78%

## Performance Metrics

**Velocity:**
- Total plans completed: 11
- Average duration: 1.5 min
- Total execution time: 0.28 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1 | 3/3 | 5 min | 1.7 min |
| 2 | 3/3 | 5 min | 1.7 min |
| 3 | 2/2 | 4 min | 2.0 min |
| 4 | 3/3 | 5 min | 1.7 min |

**Recent Trend:**
- Last 5 plans: 03-02 (2 min), 04-01 (1 min), 04-02 (1 min), 04-03 (3 min)
- Trend: stable

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap]: UPM package structure (PKG-01) pulled into Phase 1 -- asmdef affects compilation, must be set up early
- [Roadmap]: Phases 4 and 5 can execute in parallel -- function calling and Chirp TTS are independent work streams
- [01-01]: Firebase.AI asmdef uses overrideReferences with 4 precompiled DLLs for cross-assembly referencing
- [01-01]: UPM package at Packages/ as embedded package for auto-discovery
- [01-02]: PersonaConfig is pure Unity ScriptableObject -- Firebase boundary only in SystemInstructionBuilder
- [01-02]: VoiceBackend enum in separate file for independent cross-class referencing
- [01-03]: async void for Connect/SendText/Disconnect as MonoBehaviour entry points with full try-catch
- [01-03]: Outer while loop for ReceiveAsync solves single-turn trap (Pitfall 1)
- [01-03]: All ProcessResponse callbacks dispatched through MainThreadDispatcher (Pitfall 2)
- [01-03]: OnDestroy uses synchronous Cancel/Dispose -- no async Disconnect in Unity lifecycle
- [02-01]: Ring buffer capacity 2 seconds (48000 samples) absorbs network jitter; watermark 150ms (3600 samples) balances latency vs jitter
- [02-01]: Zero allocations in OnAudioFilterRead -- all buffers pre-allocated in Initialize
- [02-01]: Dummy silent AudioClip trick activates OnAudioFilterRead (Research Pitfall 8)
- [02-01]: Linear interpolation resampling with fractional position tracking across callbacks for pitch-accurate continuity
- [02-02]: System default microphone only (null device) -- no device selection API per CONTEXT.md
- [02-02]: 100ms chunk accumulation (1600 samples at 16kHz) before callback to prevent WebSocket flooding
- [02-02]: Preprocessor guards for Android using directive to avoid compile errors on non-Android
- [02-03]: Push-to-talk API lives on PersonaSession per CONTEXT.md -- session is the single developer-facing API surface
- [02-03]: Audio components are optional SerializeField references -- null checks preserve text-only fallback
- [02-03]: HandleAudioCaptured fires OnUserSpeakingStarted on first chunk for accurate speaking state
- [02-03]: AudioPlayback.Initialize() called after SetState(Connected) so playback pipeline is ready before first audio
- [03-01]: SyncPacket readonly struct with 9-property constructor -- follows Firebase SDK convention, zero GC pressure
- [03-01]: SyncPacketType enum discriminator (TextAudio, FunctionCall) -- avoids polymorphism per CONTEXT.md
- [03-01]: PacketAssembler is plain C# class -- only Unity dependency is Time.time for flush timeout
- [03-01]: FindSentenceBoundary returns last boundary found -- greedy sentence emission per scan
- [03-01]: FunctionCall packets emit immediately (no sentence buffering) -- function calls are discrete events
- [03-02]: Audio routing split: AudioPlayback inside null check, PacketAssembler outside it but inside audioChunks check
- [03-02]: Turn start detection via _turnStarted flag on first audio or transcription chunk per AI response
- [03-02]: Assembler routing after existing events -- backward-compatible event ordering preserved
- [03-02]: FunctionCallPart.Args is IReadOnlyDictionary -- matches PacketAssembler.AddFunctionCall signature directly
- [04-01]: FunctionHandler delegate returns IDictionary<string, object> or null -- null means fire-and-forget, non-null auto-sends response
- [04-01]: IsCancelled uses one-shot Remove semantics -- avoids double-checking and automatic cleanup
- [04-01]: GetObject/GetArray use as-cast with concrete type fallback for MiniJSON compatibility
- [04-02]: ConversationalGoal is a class (not struct) -- reference type with identity, managed in a list
- [04-02]: GoalManager is plain C# (no Unity/Firebase) -- same pattern as PacketAssembler
- [04-02]: Priority ordering via sequential iteration per level -- zero allocations versus sorting
- [04-03]: SystemInstructionBuilder.BuildInstructionText is internal (not private) -- PersonaSession needs raw string for mid-session role system ModelContent
- [04-03]: HandleSyncPacket intercepts FunctionCall packets before forwarding to OnSyncPacket -- developers can still observe function calls
- [04-03]: SendGoalUpdate uses role system ModelContent with REPLACE semantics -- fallback is disconnect/reconnect if rejected
- [04-03]: Connect() uses Build(_config, _goalManager) ensuring pre-connect goals are included in initial instruction

### Pending Todos

None.

### Blockers/Concerns

- [Research]: Firebase AI Logic SDK may have VertexAI backend bug in ConnectAsync -- using GoogleAI backend as recommended
- [Research]: Gemini output audio sample rate (assumed 24kHz) must be verified with actual API response in Phase 2

## Session Continuity

Last session: 2026-02-05
Stopped at: Completed 04-03-PLAN.md (Phase 4 complete)
Resume file: None
