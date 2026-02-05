# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-05)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** Phase 2 - Audio Pipeline

## Current Position

Phase: 2 of 6 (Audio Pipeline)
Plan: 2 of 3 in current phase
Status: In progress
Last activity: 2026-02-05 -- Completed 02-01-PLAN.md (AudioRingBuffer and AudioPlayback)

Progress: [████░░░░░░] 36%

## Performance Metrics

**Velocity:**
- Total plans completed: 5
- Average duration: 1.6 min
- Total execution time: 0.13 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1 | 3/3 | 5 min | 1.7 min |
| 2 | 2/3 | 3 min | 1.5 min |

**Recent Trend:**
- Last 5 plans: 01-02 (1 min), 01-03 (2 min), 02-01 (2 min), 02-02 (1 min)
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

### Pending Todos

None.

### Blockers/Concerns

- [Research]: Firebase AI Logic SDK may have VertexAI backend bug in ConnectAsync -- using GoogleAI backend as recommended
- [Research]: Gemini output audio sample rate (assumed 24kHz) must be verified with actual API response in Phase 2

## Session Continuity

Last session: 2026-02-05T20:25:18Z
Stopped at: Completed 02-01-PLAN.md (AudioRingBuffer and AudioPlayback)
Resume file: None
