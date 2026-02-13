# Milestone v1: MVP

**Status:** SHIPPED 2026-02-13
**Phases:** 1-6
**Total Plans:** 17

## Overview

AI Embodiment delivers a Unity UPM package that lets developers add AI-powered conversational characters to their games. The roadmap moves from session infrastructure (threading, lifecycle, config) through audio streaming, synchronization, function calling with conversational goals, an alternative Chirp TTS voice backend, and finally a sample scene proving the full pipeline. Each phase delivers independently testable capability, with the hardest technical problems (threading, streaming audio playback) solved first to prevent architectural collapse downstream.

## Phases

### Phase 1: Foundation and Core Session

**Goal**: Developer can create a persona configuration, attach it to a GameObject, connect to Gemini Live, and exchange text messages over a stable multi-turn session with correct threading
**Depends on**: Nothing (first phase)
**Requirements**: SESS-01, SESS-02, SESS-03, SESS-04, SESS-05, SESS-06, SESS-07, SESS-08, SESS-09, PKG-01
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md -- UPM package skeleton, Firebase.AI asmdef, MainThreadDispatcher, SessionState enum
- [x] 01-02-PLAN.md -- PersonaConfig ScriptableObject, VoiceBackend enum, SystemInstructionBuilder
- [x] 01-03-PLAN.md -- PersonaSession lifecycle, multi-turn receive loop, state events, SendText, Disconnect

### Phase 2: Audio Pipeline

**Goal**: User speaks into microphone, audio streams to Gemini Live, and AI voice response plays back through AudioSource without gaps or artifacts
**Depends on**: Phase 1
**Requirements**: AUDIO-01, AUDIO-02, AUDIO-03, AUDIO-04, VOICE-01, TRNS-01
**Plans**: 3 plans

Plans:
- [x] 02-01-PLAN.md -- AudioRingBuffer and AudioPlayback with OnAudioFilterRead streaming, resampling, and watermark buffering
- [x] 02-02-PLAN.md -- AudioCapture with microphone recording, permission handling, and chunked output callback
- [x] 02-03-PLAN.md -- PersonaSession audio integration: push-to-talk API, audio routing, speaking events, and clean teardown

### Phase 3: Synchronization

**Goal**: Text chunks, audio data, and event timing are correlated into unified packets so developers can synchronize subtitles, animations, and audio playback
**Depends on**: Phase 2
**Requirements**: SYNC-01, SYNC-02, SYNC-03, TRNS-02, TRNS-03
**Plans**: 2 plans

Plans:
- [x] 03-01-PLAN.md -- SyncPacket readonly struct, ISyncDriver interface, and PacketAssembler with sentence boundary buffering
- [x] 03-02-PLAN.md -- PersonaSession integration: OnSyncPacket event, ProcessResponse routing through PacketAssembler

### Phase 4: Function Calling and Conversational Goals

**Goal**: AI can trigger game actions via function calls with registered C# delegate handlers, and developers can define conversational goals that steer the AI's behavior with urgency-based prioritization
**Depends on**: Phase 1, Phase 3
**Requirements**: FUNC-01, FUNC-02, FUNC-03, FUNC-04, GOAL-01, GOAL-02, GOAL-03, GOAL-04, GOAL-05
**Plans**: 3 plans

Plans:
- [x] 04-01-PLAN.md -- FunctionCallContext typed argument wrapper and FunctionRegistry with registration, freeze, build, and cancellation
- [x] 04-02-PLAN.md -- GoalPriority enum, ConversationalGoal data class, and GoalManager with lifecycle and instruction composition
- [x] 04-03-PLAN.md -- PersonaSession and SystemInstructionBuilder integration: function dispatch, response round-trip, goal API, mid-session updates

### Phase 5: Chirp TTS Voice Backend

**Goal**: Developer can select Chirp 3 HD as the voice backend for a persona, getting access to 30+ high-quality voices as an alternative to Gemini native audio
**Depends on**: Phase 2, Phase 3
**Requirements**: VOICE-02, VOICE-03, VOICE-04
**Plans**: 3 plans

Plans:
- [x] 05-01-PLAN.md -- ChirpTTSClient HTTP client and ChirpVoiceList static voice/language data
- [x] 05-02-PLAN.md -- PersonaConfig Chirp fields and PersonaConfigEditor custom Inspector with voice dropdowns
- [x] 05-03-PLAN.md -- PersonaSession Chirp TTS routing: audio discard, transcription-driven synthesis, sentence and full-response modes

### Phase 6: Sample Scene and Integration

**Goal**: Developer can install the package and run a sample scene that demonstrates the full pipeline -- persona talking with synchronized voice, text, and animation function calls -- in under 5 minutes
**Depends on**: Phase 4, Phase 5
**Requirements**: PKG-02
**Plans**: 3 plans

Plans:
- [x] 06-01-PLAN.md -- UPM sample folder structure, package.json samples entry, asmdef, UI Toolkit layout (UXML) and styling (USS)
- [x] 06-02-PLAN.md -- AyaChatUI chat log controller and AyaSampleController with function calls, intro, push-to-talk, goal injection
- [x] 06-03-PLAN.md -- Unity Editor scene and asset creation checkpoint (scene, PersonaConfig, PanelSettings, component wiring)

## Milestone Summary

**Key Decisions:**
- UPM package structure (not Asset Store) for clean dependency management
- Firebase AI Logic SDK for Gemini Live WebSocket protocol
- ScriptableObjects for persona config (Unity-native, Inspector-editable)
- C# delegates for function handlers (flexible, composable)
- Separate Chirp TTS client via HTTP (Firebase SDK doesn't include TTS)
- Lock-free SPSC ring buffer for audio streaming (zero GC in audio thread)
- Outer while loop for ReceiveAsync solving single-turn trap
- async void for MonoBehaviour entry points with full try-catch
- Per-persona voice backend selection (not runtime toggle)

**Issues Resolved:**
- Single-turn trap in Gemini Live receive loop (outer while loop pattern)
- Unity main thread constraint (MainThreadDispatcher with ConcurrentQueue)
- Audio thread zero-allocation requirement (pre-allocated ring buffer)
- Cross-assembly Firebase reference (overrideReferences with precompiled DLLs)
- WAV header stripping for Chirp TTS PCM extraction (RIFF validation with fallback)

**Issues Deferred:**
- Firebase VertexAI backend bug in ConnectAsync (using GoogleAI backend as workaround)
- Gemini output audio sample rate verification (assumed 24kHz)

**Technical Debt Incurred:**
- Scene file on disk has null references for AyaChatUI, AudioPlayback._audioSource, and AyaSampleController fields (Editor wiring not saved to disk)
- Missing .meta files for Phase 2-3 scripts (auto-generated on next Unity Editor open)

---

_For current project status, see .planning/PROJECT.md_
