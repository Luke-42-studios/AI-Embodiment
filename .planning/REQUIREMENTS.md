# Requirements: AI Embodiment

**Defined:** 2026-02-05
**Core Value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.

## v1 Requirements

### Core Session

- [x] **SESS-01**: Developer can create a PersonaConfig ScriptableObject with personality fields (archetype, traits, backstory, speech patterns)
- [x] **SESS-02**: Developer can select Gemini model in PersonaConfig (e.g., gemini-2.0-flash-exp, gemini-2.5-flash-native-audio)
- [x] **SESS-03**: Developer can select voice backend and voice name in PersonaConfig (Gemini native or Chirp)
- [x] **SESS-04**: Developer can add PersonaSession MonoBehaviour to a GameObject and assign a PersonaConfig
- [x] **SESS-05**: PersonaSession.Connect() establishes a Gemini Live session via Firebase AI Logic
- [x] **SESS-06**: PersonaSession handles the receive loop lifecycle (re-calls ReceiveAsync after each TurnComplete)
- [x] **SESS-07**: Thread-safe dispatcher marshals Firebase background callbacks to Unity main thread
- [x] **SESS-08**: PersonaSession fires state change events (Connecting, Connected, Error, Disconnected)
- [x] **SESS-09**: PersonaSession.Disconnect() cleanly closes the session

### Audio Pipeline

- [ ] **AUDIO-01**: AudioCapture component records from Unity Microphone API at 16kHz mono PCM
- [ ] **AUDIO-02**: AudioCapture streams PCM data to PersonaSession for sending to Gemini Live
- [ ] **AUDIO-03**: AudioPlayback component plays AI voice through a Unity AudioSource
- [ ] **AUDIO-04**: AudioPlayback uses ring buffer for streaming real-time PCM without gaps or pops

### Voice Backends

- [ ] **VOICE-01**: Gemini native audio path -- audio received directly from LiveSession response
- [ ] **VOICE-02**: Chirp 3 HD TTS path -- text from LiveSession sent via HTTP to Cloud TTS, PCM audio returned
- [ ] **VOICE-03**: Voice backend selected per-persona in ScriptableObject config
- [ ] **VOICE-04**: ChirpTTSClient handles HTTP requests to texttospeech.googleapis.com via UnityWebRequest

### Transcription

- [ ] **TRNS-01**: PersonaSession exposes user input transcript (speech-to-text from Gemini) via event/callback
- [ ] **TRNS-02**: PersonaSession exposes AI output transcript (response text for subtitles) via event/callback
- [ ] **TRNS-03**: Output transcript text streams incrementally as chunks arrive (not buffered until turn end)

### Synchronization

- [ ] **SYNC-01**: PacketAssembler correlates text chunks, audio data, and emote timing into unified SyncPackets
- [ ] **SYNC-02**: SyncPackets expose text, audio, and function call events with timing information
- [ ] **SYNC-03**: PacketAssembler works for both voice paths (Gemini native audio and Chirp TTS)

### Function Calling

- [ ] **FUNC-01**: Developer can declare function schemas (name, parameters, description) on PersonaSession
- [ ] **FUNC-02**: Developer registers C# delegate handlers for each declared function
- [ ] **FUNC-03**: When AI triggers a function call, the registered delegate fires with parsed arguments
- [ ] **FUNC-04**: Built-in emote function with animation name enum as a reference implementation

### Conversational Goals

- [ ] **GOAL-01**: Developer can define conversational goals on a persona (objective text, priority level)
- [ ] **GOAL-02**: Goal priorities (low, medium, high) control how urgently the AI steers conversation toward the goal
- [ ] **GOAL-03**: Developer can add, remove, and reprioritize goals at runtime via API
- [ ] **GOAL-04**: System instruction builder folds active goals into the prompt with urgency-appropriate framing
- [ ] **GOAL-05**: AI signals goal completion via a built-in function call (e.g., goal_reached("goal_id"))

### Packaging

- [x] **PKG-01**: Project structured as UPM package (Runtime/, Samples~/, package.json, asmdef)
- [ ] **PKG-02**: Sample scene demonstrates full pipeline -- persona talking with animation function calls

## v2 Requirements

### Persistence

- **PERSIST-01**: Conversation history saved across sessions
- **PERSIST-02**: SQLite or similar local storage for conversation memory
- **PERSIST-03**: Memory context injected into system instruction

### Voice Cloning

- **CLONE-01**: Instant Custom Voice (ICV) support for Chirp 3 HD
- **CLONE-02**: Developer can provide voice cloning key for custom voices

### Editor Tools

- **EDIT-01**: Custom Inspector for PersonaConfig with personality preview
- **EDIT-02**: Live testing window -- talk to persona in Editor without entering Play Mode

### Advanced Audio

- **ADVAUD-01**: Platform-specific native audio plugins for lower latency
- **ADVAUD-02**: Automatic sample rate conversion across all boundaries

## Out of Scope

| Feature | Reason |
|---------|--------|
| Runtime voice switching mid-session | Voice is set per-persona at connect time -- simpler architecture |
| Visual UI components (chat window, text input) | Headless library -- devs build their own UI |
| OAuth/service account auth | Devs configure Firebase project auth separately |
| Mobile-specific optimizations | Desktop-first for v1, mobile tested but not optimized |
| Multi-persona conversations | Single persona per session for v1 |
| Language translation | Out of scope -- persona speaks one language |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SESS-01 | Phase 1 | Complete |
| SESS-02 | Phase 1 | Complete |
| SESS-03 | Phase 1 | Complete |
| SESS-04 | Phase 1 | Complete |
| SESS-05 | Phase 1 | Complete |
| SESS-06 | Phase 1 | Complete |
| SESS-07 | Phase 1 | Complete |
| SESS-08 | Phase 1 | Complete |
| SESS-09 | Phase 1 | Complete |
| AUDIO-01 | Phase 2 | Pending |
| AUDIO-02 | Phase 2 | Pending |
| AUDIO-03 | Phase 2 | Pending |
| AUDIO-04 | Phase 2 | Pending |
| VOICE-01 | Phase 2 | Pending |
| VOICE-02 | Phase 5 | Pending |
| VOICE-03 | Phase 5 | Pending |
| VOICE-04 | Phase 5 | Pending |
| TRNS-01 | Phase 2 | Pending |
| TRNS-02 | Phase 3 | Pending |
| TRNS-03 | Phase 3 | Pending |
| SYNC-01 | Phase 3 | Pending |
| SYNC-02 | Phase 3 | Pending |
| SYNC-03 | Phase 3 | Pending |
| FUNC-01 | Phase 4 | Pending |
| FUNC-02 | Phase 4 | Pending |
| FUNC-03 | Phase 4 | Pending |
| FUNC-04 | Phase 4 | Pending |
| GOAL-01 | Phase 4 | Pending |
| GOAL-02 | Phase 4 | Pending |
| GOAL-03 | Phase 4 | Pending |
| GOAL-04 | Phase 4 | Pending |
| GOAL-05 | Phase 4 | Pending |
| PKG-01 | Phase 1 | Complete |
| PKG-02 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 34 total
- Mapped to phases: 34
- Unmapped: 0

---
*Requirements defined: 2026-02-05*
*Last updated: 2026-02-05 after roadmap creation*
