# Roadmap: AI Embodiment

## Overview

AI Embodiment delivers a Unity UPM package that lets developers add AI-powered conversational characters to their games. The roadmap moves from session infrastructure (threading, lifecycle, config) through audio streaming, synchronization, function calling with conversational goals, an alternative Chirp TTS voice backend, and finally a sample scene proving the full pipeline. Each phase delivers independently testable capability, with the hardest technical problems (threading, streaming audio playback) solved first to prevent architectural collapse downstream.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Foundation and Core Session** - UPM structure, threading, persona config, and Gemini Live session lifecycle
- [ ] **Phase 2: Audio Pipeline** - Microphone capture, streaming playback via ring buffer, and Gemini native audio voice path
- [ ] **Phase 3: Synchronization** - PacketAssembler correlates text, audio, and event timing into unified packets
- [ ] **Phase 4: Function Calling and Conversational Goals** - AI-triggered game actions via C# delegates and goal-driven conversation steering
- [ ] **Phase 5: Chirp TTS Voice Backend** - Alternative voice path via Cloud TTS HTTP API with per-persona backend selection
- [ ] **Phase 6: Sample Scene and Integration** - Sample scene demonstrating the full pipeline end-to-end

## Phase Details

### Phase 1: Foundation and Core Session
**Goal**: Developer can create a persona configuration, attach it to a GameObject, connect to Gemini Live, and exchange text messages over a stable multi-turn session with correct threading
**Depends on**: Nothing (first phase)
**Requirements**: SESS-01, SESS-02, SESS-03, SESS-04, SESS-05, SESS-06, SESS-07, SESS-08, SESS-09, PKG-01
**Success Criteria** (what must be TRUE):
  1. Developer can create a PersonaConfig ScriptableObject in the Inspector with personality fields, model selection, and voice backend choice
  2. Developer can add PersonaSession to a GameObject, assign a PersonaConfig, and call Connect() to establish a Gemini Live session
  3. PersonaSession sustains multi-turn text conversation (receives responses after TurnComplete without dying)
  4. PersonaSession fires state change events (Connecting, Connected, Error, Disconnected) that developer code can subscribe to
  5. PersonaSession.Disconnect() cleanly closes the session without leaked threads or WebSocket connections, including during scene transitions
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md -- UPM package skeleton, Firebase.AI asmdef, MainThreadDispatcher, SessionState enum
- [x] 01-02-PLAN.md -- PersonaConfig ScriptableObject, VoiceBackend enum, SystemInstructionBuilder
- [ ] 01-03-PLAN.md -- PersonaSession lifecycle, multi-turn receive loop, state events, SendText, Disconnect

### Phase 2: Audio Pipeline
**Goal**: User speaks into microphone, audio streams to Gemini Live, and AI voice response plays back through AudioSource without gaps or artifacts
**Depends on**: Phase 1
**Requirements**: AUDIO-01, AUDIO-02, AUDIO-03, AUDIO-04, VOICE-01, TRNS-01
**Success Criteria** (what must be TRUE):
  1. AudioCapture records from the user's microphone at 16kHz mono PCM and streams chunks to the active Gemini Live session
  2. AI voice response (Gemini native audio) plays through a Unity AudioSource in real time as chunks arrive
  3. Streaming playback uses a ring buffer with write-ahead watermark -- no pops, silence gaps, or garbled audio during continuous speech
  4. Developer can assign any AudioSource to AudioPlayback, enabling spatialization and audio mixing through standard Unity tools
  5. User input transcript (speech-to-text from Gemini) is exposed via event/callback on PersonaSession
**Plans**: TBD

Plans:
- [ ] 02-01: AudioCapture component and microphone-to-session streaming
- [ ] 02-02: AudioPlayback component with ring buffer streaming
- [ ] 02-03: Gemini native audio integration and end-to-end voice loop

### Phase 3: Synchronization
**Goal**: Text chunks, audio data, and event timing are correlated into unified packets so developers can synchronize subtitles, animations, and audio playback
**Depends on**: Phase 2
**Requirements**: SYNC-01, SYNC-02, SYNC-03, TRNS-02, TRNS-03
**Success Criteria** (what must be TRUE):
  1. PacketAssembler produces SyncPackets containing correlated text, audio, and function call events with timing information
  2. Text displayed as subtitles aligns with corresponding audio playback (no drift or mismatch)
  3. PacketAssembler works correctly for the Gemini native audio path (Chirp path support validated in Phase 5)
  4. AI output transcript text streams incrementally as chunks arrive for real-time subtitle display
**Plans**: TBD

Plans:
- [ ] 03-01: PacketAssembler and SyncPacket data model
- [ ] 03-02: Text-audio correlation and timing synchronization

### Phase 4: Function Calling and Conversational Goals
**Goal**: AI can trigger game actions via function calls with registered C# delegate handlers, and developers can define conversational goals that steer the AI's behavior with urgency-based prioritization
**Depends on**: Phase 1 (session receive loop), Phase 3 (timing correlation for function call events)
**Requirements**: FUNC-01, FUNC-02, FUNC-03, FUNC-04, GOAL-01, GOAL-02, GOAL-03, GOAL-04, GOAL-05
**Success Criteria** (what must be TRUE):
  1. Developer can declare function schemas and register C# delegate handlers on PersonaSession
  2. When the AI triggers a function call during conversation, the registered delegate fires on the main thread with parsed arguments
  3. Built-in emote function works as a reference implementation -- AI calls emote("wave") and the developer's handler receives it
  4. Developer can define conversational goals with priority levels (low, medium, high) and add/remove/reprioritize them at runtime
  5. System instruction builder incorporates active goals with urgency-appropriate framing, and AI signals goal completion via a built-in goal_reached function call
**Plans**: TBD

Plans:
- [ ] 04-01: FunctionCallHandler with delegate registration and dispatch
- [ ] 04-02: Built-in emote function and function call response round-trip
- [ ] 04-03: Conversational goal system and system instruction integration

### Phase 5: Chirp TTS Voice Backend
**Goal**: Developer can select Chirp 3 HD as the voice backend for a persona, getting access to 30+ high-quality voices as an alternative to Gemini native audio
**Depends on**: Phase 2 (audio playback pipeline), Phase 3 (PacketAssembler dual-path support)
**Requirements**: VOICE-02, VOICE-03, VOICE-04
**Success Criteria** (what must be TRUE):
  1. ChirpTTSClient sends text to Cloud TTS API via UnityWebRequest and returns PCM audio
  2. When a persona is configured with Chirp backend, text from Gemini Live is routed to Chirp TTS instead of using inline audio
  3. Voice backend selection (Gemini native vs Chirp) is configured per-persona in the ScriptableObject and applied at session creation time
**Plans**: TBD

Plans:
- [ ] 05-01: ChirpTTSClient HTTP integration
- [ ] 05-02: Voice backend router and per-persona selection

### Phase 6: Sample Scene and Integration
**Goal**: Developer can install the package and run a sample scene that demonstrates the full pipeline -- persona talking with synchronized voice, text, and animation function calls -- in under 5 minutes
**Depends on**: Phase 4, Phase 5
**Requirements**: PKG-02
**Success Criteria** (what must be TRUE):
  1. Sample scene loads and runs with a working persona that responds to voice input with synchronized speech, text, and animation events
  2. Sample scene demonstrates function calling (emote triggers) and at least one conversational goal in action
**Plans**: TBD

Plans:
- [ ] 06-01: Sample scene with full pipeline demonstration

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5 -> 6

Note: Phase 4 and Phase 5 can execute in parallel (config parallelization: enabled). Phase 4 depends on Phases 1+3. Phase 5 depends on Phases 2+3. Neither depends on the other.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation and Core Session | 2/3 | In progress | - |
| 2. Audio Pipeline | 0/3 | Not started | - |
| 3. Synchronization | 0/2 | Not started | - |
| 4. Function Calling and Conversational Goals | 0/3 | Not started | - |
| 5. Chirp TTS Voice Backend | 0/2 | Not started | - |
| 6. Sample Scene and Integration | 0/1 | Not started | - |
