# Requirements: AI Embodiment

**Defined:** 2026-02-13
**Core Value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.

## v0.8 Requirements

### WebSocket Transport

- [x] **WS-01**: GeminiLiveClient connects to Gemini Live via direct WebSocket (`wss://generativelanguage.googleapis.com`)
- [x] **WS-02**: WebSocket setup handshake sends model config, generation config, system instruction, and tools
- [x] **WS-03**: Background receive loop parses JSON text frames and binary audio frames
- [x] **WS-04**: ConcurrentQueue-based event dispatching from WebSocket thread to Unity main thread
- [x] **WS-05**: Clean disconnect with WebSocket close handshake and CancellationToken propagation

### Audio Model Support

- [x] **AUD-01**: AUDIO response modality requested in generation config (not TEXT)
- [x] **AUD-02**: Gemini native audio (24kHz PCM) decoded from base64 inlineData and routed to AudioPlayback
- [x] **AUD-03**: Input transcription (user STT) extracted from `inputTranscription` field and exposed via event
- [x] **AUD-04**: Output transcription (AI speech text) extracted from `outputTranscription` field and exposed via event
- [x] **AUD-05**: Turn lifecycle events (turnComplete, interrupted) parsed from serverContent

### TTS Abstraction

- [ ] **TTS-01**: ITTSProvider interface with `SynthesizeAsync(text, voiceConfig)` returning PCM audio
- [ ] **TTS-02**: ChirpTTSClient implements ITTSProvider (existing HTTP client, adapted)
- [ ] **TTS-03**: Chirp TTS path: discard Gemini native audio, route outputTranscription text to ITTSProvider
- [ ] **TTS-04**: ChirpTTSClient uses Newtonsoft.Json for request/response serialization (replacing MiniJSON)

### PersonaSession Migration

- [ ] **MIG-01**: PersonaSession.Connect() uses GeminiLiveClient instead of Firebase LiveSession
- [ ] **MIG-02**: PersonaSession preserves all existing public events (OnTextReceived, OnTurnComplete, OnStateChanged, etc.)
- [ ] **MIG-03**: PersonaSession preserves all existing public methods (Connect, Disconnect, SendText, StartListening, StopListening)
- [ ] **MIG-04**: Function calling works via WebSocket-native tool declarations and function call/response messages
- [ ] **MIG-05**: Conversational goals and mid-session system instruction updates work via WebSocket messages
- [ ] **MIG-06**: AIEmbodimentSettings.Instance.ApiKey provides the API key for connection (replaces Firebase project config)

### Dependency Removal

- [ ] **DEP-01**: Firebase.AI assembly reference removed from runtime asmdef
- [ ] **DEP-02**: All Firebase.AI type references (LiveSession, LiveGenerativeModel, ModelContent, etc.) replaced
- [ ] **DEP-03**: Newtonsoft.Json added as dependency (com.unity.nuget.newtonsoft-json)
- [ ] **DEP-04**: MainThreadDispatcher pattern preserved (or replaced by GeminiLiveClient's ConcurrentQueue approach)

### Integration

- [ ] **INT-01**: Sample scene (AyaLiveStream) works with new WebSocket transport
- [ ] **INT-02**: PacketAssembler works with new transcription streams for Chirp TTS subtitle sync

## v1.0 Requirements (Deferred)

### Persistence

- **PERSIST-01**: Conversation history saved across sessions
- **PERSIST-02**: SQLite or similar local storage for conversation memory

### Additional TTS Backends

- **TTS-EL-01**: ElevenLabs TTS provider implementing ITTSProvider
- **TTS-EL-02**: ElevenLabs streaming TTS for lower latency

### Editor Tools

- **EDIT-01**: Live testing window -- talk to persona in Editor without entering Play Mode

## Out of Scope

| Feature | Reason |
|---------|--------|
| Runtime voice switching mid-session | Voice is set per-persona at connect time |
| Visual UI components | Headless library -- devs build their own UI |
| ElevenLabs implementation | ITTSProvider interface enables it, but only Chirp ships in v0.8 |
| Mobile-specific optimizations | Desktop-first |
| Persistent conversation memory | Defer to v1.0 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| WS-01 | Phase 7 | Complete |
| WS-02 | Phase 7 | Complete |
| WS-03 | Phase 7 | Complete |
| WS-04 | Phase 7 | Complete |
| WS-05 | Phase 7 | Complete |
| AUD-01 | Phase 7 | Complete |
| AUD-02 | Phase 7 | Complete |
| AUD-03 | Phase 7 | Complete |
| AUD-04 | Phase 7 | Complete |
| AUD-05 | Phase 7 | Complete |
| TTS-01 | Phase 9 | Pending |
| TTS-02 | Phase 9 | Pending |
| TTS-03 | Phase 9 | Pending |
| TTS-04 | Phase 9 | Pending |
| MIG-01 | Phase 8 | Pending |
| MIG-02 | Phase 8 | Pending |
| MIG-03 | Phase 8 | Pending |
| MIG-04 | Phase 10 | Pending |
| MIG-05 | Phase 10 | Pending |
| MIG-06 | Phase 8 | Pending |
| DEP-01 | Phase 8 | Pending |
| DEP-02 | Phase 8 | Pending |
| DEP-03 | Phase 8 | Pending |
| DEP-04 | Phase 8 | Pending |
| INT-01 | Phase 11 | Pending |
| INT-02 | Phase 11 | Pending |

**Coverage:**
- v0.8 requirements: 26 total
- Mapped to phases: 26
- Unmapped: 0

---
*Requirements defined: 2026-02-13*
*Last updated: 2026-02-13 after Phase 7 completion*
