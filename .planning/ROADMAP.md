# Roadmap: AI Embodiment

## Milestones

- **v1 MVP** -- Phases 1-6 (shipped 2026-02-13)
- **v0.8 WebSocket Migration** -- Phases 7-11 (shipped 2026-02-13)

## Phases

<details>
<summary>v1 MVP (Phases 1-6) -- SHIPPED 2026-02-13</summary>

- [x] Phase 1: Foundation and Core Session (3/3 plans) -- completed 2026-02-05
- [x] Phase 2: Audio Pipeline (3/3 plans) -- completed 2026-02-05
- [x] Phase 3: Synchronization (2/2 plans) -- completed 2026-02-05
- [x] Phase 4: Function Calling and Conversational Goals (3/3 plans) -- completed 2026-02-05
- [x] Phase 5: Chirp TTS Voice Backend (3/3 plans) -- completed 2026-02-05
- [x] Phase 6: Sample Scene and Integration (3/3 plans) -- completed 2026-02-05

</details>

### v0.8 WebSocket Migration

**Milestone Goal:** Replace Firebase AI Logic SDK with direct WebSocket client, support audio-only Gemini models, and introduce ITTSProvider abstraction for custom voice cloning. Same public API surface, zero Firebase dependency.

- [x] **Phase 7: WebSocket Transport and Audio Parsing** - GeminiLiveClient with full Gemini Live protocol support
- [x] **Phase 8: PersonaSession Migration and Dependency Removal** - Rewire PersonaSession to GeminiLiveClient, remove Firebase
- [x] **Phase 9: TTS Abstraction** - ITTSProvider interface and ChirpTTSClient adaptation
- [x] **Phase 10: Function Calling and Goals Migration** - WebSocket-native tool declarations and mid-session instruction updates
- [x] **Phase 11: Integration Verification** - Sample scene and PacketAssembler validation with new transport
- [ ] **Phase 11.1: Queued Response Sample** - Push-to-talk transcript approval UX with pre-fetched AI response playback (INSERTED)
- [ ] **Phase 11.2: Chirp Custom Voice Bearer Auth** - OAuth2 bearer token auth for Chirp Custom Voice TTS via service account credentials (INSERTED)

## Phase Details

### Phase 7: WebSocket Transport and Audio Parsing
**Goal**: A standalone GeminiLiveClient connects to Gemini Live over WebSocket, sends/receives audio, and exposes all server events via a thread-safe event queue
**Depends on**: Nothing (foundation phase)
**Requirements**: WS-01, WS-02, WS-03, WS-04, WS-05, AUD-01, AUD-02, AUD-03, AUD-04, AUD-05
**Success Criteria** (what must be TRUE):
  1. GeminiLiveClient connects to Gemini Live, completes setup handshake, and receives setupComplete acknowledgment
  2. Audio sent via SendAudio produces AI audio responses that are decoded from base64 inlineData as 24kHz PCM float arrays
  3. outputTranscription and inputTranscription text arrives via the event queue as distinct event types
  4. turnComplete and interrupted server events are parsed and enqueued correctly
  5. Calling Disconnect performs a clean WebSocket close handshake and the receive loop exits without exceptions
**Plans:** 2 plans

Plans:
- [x] 07-01-PLAN.md -- GeminiLiveClient core: data types (GeminiEvent, GeminiLiveConfig), WebSocket connect, setup handshake, send methods (SendAudio, SendText), disconnect, ConcurrentQueue event infrastructure, Newtonsoft.Json dependency
- [x] 07-02-PLAN.md -- Receive loop and event dispatch: multi-frame message accumulation, JSON type dispatch, audio decoding (base64 -> PCM -> float[]), transcription extraction, turn lifecycle events, function call event parsing

### Phase 8: PersonaSession Migration and Dependency Removal
**Goal**: PersonaSession uses GeminiLiveClient instead of Firebase LiveSession, all Firebase references are removed, and existing public API (events, methods, properties) works identically
**Depends on**: Phase 7
**Requirements**: MIG-01, MIG-02, MIG-03, MIG-06, DEP-01, DEP-02, DEP-03, DEP-04
**Success Criteria** (what must be TRUE):
  1. PersonaSession.Connect() establishes a session via GeminiLiveClient and transitions through Connecting -> Connected states
  2. All existing public events (OnTextReceived, OnTurnComplete, OnStateChanged, OnInputTranscription, OnOutputTranscription, OnInterrupted, OnAISpeakingStarted/Stopped, OnUserSpeakingStarted/Stopped, OnError, OnSyncPacket) fire with correct data
  3. SendText, StartListening, StopListening, Disconnect all work as before
  4. AIEmbodimentSettings.Instance.ApiKey provides the API key for connection (no Firebase project config)
  5. The project compiles with zero Firebase.AI references in runtime asmdef and source files
**Plans:** 2 plans

Plans:
- [x] 08-01-PLAN.md -- Firebase purge and dependency swap: delete Firebase/ExternalDependencyManager directories, create AIEmbodimentSettings ScriptableObject with password-masked editor, clean all asmdef files, migrate SystemInstructionBuilder to return string, stub FunctionRegistry without Firebase types, migrate ChirpTTSClient from MiniJSON to Newtonsoft.Json, stub sample scene
- [x] 08-02-PLAN.md -- PersonaSession rewire: replace LiveSession with GeminiLiveClient, rewrite Connect/Disconnect/SendText/HandleAudioCaptured, implement HandleGeminiEvent event bridge with complete GeminiEventType-to-public-event mapping, add FloatToPcm16 audio conversion, ProcessEvents polling in Update

### Phase 9: TTS Abstraction
**Goal**: Developers can choose between Gemini native audio and custom TTS backends via a clean ITTSProvider interface, with ChirpTTSClient as the shipped implementation
**Depends on**: Phase 8
**Requirements**: TTS-01, TTS-02, TTS-03, TTS-04
**Success Criteria** (what must be TRUE):
  1. ITTSProvider interface exists with SynthesizeAsync method returning PCM audio
  2. ChirpTTSClient implements ITTSProvider and uses Newtonsoft.Json for request/response serialization (MiniJSON removed)
  3. When voice backend is ChirpTTS, Gemini native audio is discarded and outputTranscription text is routed through ITTSProvider for synthesis and playback
**Plans:** 2 plans

Plans:
- [x] 09-01-PLAN.md -- ITTSProvider interface + TTSResult struct + TTSSynthesisMode enum, VoiceBackend.Custom, ChirpTTSClient implements ITTSProvider, PersonaConfig field renames
- [x] 09-02-PLAN.md -- PersonaSession provider-agnostic TTS routing, SetTTSProvider API, PersonaConfigEditor Custom backend UI

### Phase 10: Function Calling and Goals Migration
**Goal**: AI-triggered function calls and conversational goals work over the WebSocket transport with the same developer-facing API, including both native toolCall and prompt-based fallback paths
**Depends on**: Phase 8
**Requirements**: MIG-04, MIG-05
**Success Criteria** (what must be TRUE):
  1. RegisterFunction declarations are sent as WebSocket-native tool JSON in the setup handshake, and AI-triggered function calls dispatch to registered handlers with correct arguments
  2. Function responses are sent back via WebSocket toolResponse messages and the AI continues the conversation
  3. AddGoal/RemoveGoal/ReprioritizeGoal accumulate goals locally, applied at Connect() time; mid-session updates log informational message about API limitation
**Plans:** 2 plans

Plans:
- [x] 10-01-PLAN.md -- Function calling infrastructure: FunctionDeclaration builder, FunctionRegistry declaration support with dual-path output, GeminiEvent FunctionId, GeminiLiveConfig ToolsJson, GeminiLiveClient tools in setup + SendToolResponse + toolCallCancellation parsing
- [x] 10-02-PLAN.md -- PersonaSession wiring: RegisterFunction with declaration, function call ID flow, toolResponse sending, prompt-based fallback parsing, FunctionCallCancellation handling, SendGoalUpdate finalization

### Phase 11: Integration Verification
**Goal**: The complete v0.8 package works end-to-end in the sample scene with both voice backends
**Depends on**: Phase 9, Phase 10
**Requirements**: INT-01, INT-02
**Success Criteria** (what must be TRUE):
  1. AyaLiveStream sample scene connects, sends/receives audio, and displays transcription text using the WebSocket transport
  2. PacketAssembler produces correct SyncPackets from the new transcription streams for both Gemini native audio and Chirp TTS paths
**Plans:** 2 plans

Plans:
- [x] 11-01-PLAN.md -- Infrastructure fixes: create missing .meta files for v0.8 source files, create AIEmbodimentSettings asset in Resources, fix scene YAML (AudioPlayback._audioSource wiring, PlayOnAwake)
- [x] 11-02-PLAN.md -- AyaSampleController status feedback and SyncPacket validation logging, Samples~ sync, end-to-end human verification

### Phase 11.1: Queued Response Sample (INSERTED)
**Goal**: A new sample scene demonstrating a push-to-talk transcript-approve-then-play UX where the user holds spacebar to speak, sees their transcript, approves with Enter, and the AI response (pre-fetched in background) plays back immediately
**Depends on**: Phase 11
**Success Criteria** (what must be TRUE):
  1. User holds spacebar to record, releases to stop -- their input transcript appears on screen in real time
  2. AI response audio is fetched and queued in the background while the user reviews their transcript
  3. User presses Enter to approve their message -- the queued AI response begins playback immediately (near-zero latency)
  4. The AI response transcript displays alongside audio playback
  5. The flow loops: after AI response completes, user can hold spacebar again for next turn
**Plans:** 2 plans

Plans:
- [ ] 11.1-01-PLAN.md -- Project structure, UI assets (UXML/USS), QueuedResponseController state machine with audio buffering, QueuedResponseUI with state-driven display
- [ ] 11.1-02-PLAN.md -- Samples~ sync and end-to-end human verification of the queued response UX

### Phase 11.2: Chirp Custom Voice Bearer Auth (INSERTED)
**Goal**: ChirpTTSClient authenticates with Google Cloud TTS via OAuth2 bearer tokens (service account JWT exchange) instead of API key, enabling Chirp Custom Voice cloning on the v1beta1 endpoint
**Depends on**: Phase 11.1
**Success Criteria** (what must be TRUE):
  1. ChirpTTSClient generates a JWT from a service account JSON key, exchanges it for an OAuth2 access token via `oauth2.googleapis.com/token`, and uses `Authorization: Bearer` header for TTS requests
  2. Access tokens are cached and automatically refreshed before the 1-hour expiry
  3. Custom voice synthesis requests target `texttospeech.googleapis.com/v1beta1/text:synthesize` with `voiceClone.voiceCloningKey` in the request body
  4. Standard (non-custom) Chirp voices continue to work via the same bearer token auth path
  5. Service account credentials are loaded securely (not shipped in player builds)
**Plans:** 2 plans

Plans:
- [ ] 11.2-01-PLAN.md -- GoogleServiceAccountAuth credential provider (JWT signing, token exchange, caching), AIEmbodimentSettings service account path field, editor UI file picker, .gitignore updates
- [ ] 11.2-02-PLAN.md -- ChirpTTSClient dual-auth (API key v1 / bearer token v1beta1), PersonaSession service account lifecycle and auth-aware TTS provider creation

## Progress

**Execution Order:** 7 -> 8 -> 9 -> 10 -> 11 -> 11.1 -> 11.2 (Phases 9 and 10 can run in parallel after Phase 8)

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Foundation and Core Session | v1 | 3/3 | Complete | 2026-02-05 |
| 2. Audio Pipeline | v1 | 3/3 | Complete | 2026-02-05 |
| 3. Synchronization | v1 | 2/2 | Complete | 2026-02-05 |
| 4. Function Calling and Conversational Goals | v1 | 3/3 | Complete | 2026-02-05 |
| 5. Chirp TTS Voice Backend | v1 | 3/3 | Complete | 2026-02-05 |
| 6. Sample Scene and Integration | v1 | 3/3 | Complete | 2026-02-05 |
| 7. WebSocket Transport and Audio Parsing | v0.8 | 2/2 | Complete | 2026-02-13 |
| 8. PersonaSession Migration and Dependency Removal | v0.8 | 2/2 | Complete | 2026-02-13 |
| 9. TTS Abstraction | v0.8 | 2/2 | Complete | 2026-02-13 |
| 10. Function Calling and Goals Migration | v0.8 | 2/2 | Complete | 2026-02-13 |
| 11. Integration Verification | v0.8 | 2/2 | Complete | 2026-02-13 |
| 11.1. Queued Response Sample | v0.8 | 0/2 | Planned | - |
| 11.2. Chirp Custom Voice Bearer Auth | v0.8 | 0/2 | Planned | - |
