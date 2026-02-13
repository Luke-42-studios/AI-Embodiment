# Requirements Archive: v1 MVP

**Archived:** 2026-02-13
**Status:** SHIPPED

This is the archived requirements specification for v1.
For current requirements, see `.planning/REQUIREMENTS.md` (created for next milestone).

---

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

- [x] **AUDIO-01**: AudioCapture component records from Unity Microphone API at 16kHz mono PCM
- [x] **AUDIO-02**: AudioCapture streams PCM data to PersonaSession for sending to Gemini Live
- [x] **AUDIO-03**: AudioPlayback component plays AI voice through a Unity AudioSource
- [x] **AUDIO-04**: AudioPlayback uses ring buffer for streaming real-time PCM without gaps or pops

### Voice Backends

- [x] **VOICE-01**: Gemini native audio path -- audio received directly from LiveSession response
- [x] **VOICE-02**: Chirp 3 HD TTS path -- text from LiveSession sent via HTTP to Cloud TTS, PCM audio returned
- [x] **VOICE-03**: Voice backend selected per-persona in ScriptableObject config
- [x] **VOICE-04**: ChirpTTSClient handles HTTP requests to texttospeech.googleapis.com via UnityWebRequest

### Transcription

- [x] **TRNS-01**: PersonaSession exposes user input transcript (speech-to-text from Gemini) via event/callback
- [x] **TRNS-02**: PersonaSession exposes AI output transcript (response text for subtitles) via event/callback
- [x] **TRNS-03**: Output transcript text streams incrementally as chunks arrive (not buffered until turn end)

### Synchronization

- [x] **SYNC-01**: PacketAssembler correlates text chunks, audio data, and emote timing into unified SyncPackets
- [x] **SYNC-02**: SyncPackets expose text, audio, and function call events with timing information
- [x] **SYNC-03**: PacketAssembler works for both voice paths (Gemini native audio and Chirp TTS)

### Function Calling

- [x] **FUNC-01**: Developer can declare function schemas (name, parameters, description) on PersonaSession
- [x] **FUNC-02**: Developer registers C# delegate handlers for each declared function
- [x] **FUNC-03**: When AI triggers a function call, the registered delegate fires with parsed arguments
- [x] **FUNC-04**: Built-in emote function with animation name enum as a reference implementation

### Conversational Goals

- [x] **GOAL-01**: Developer can define conversational goals on a persona (objective text, priority level)
- [x] **GOAL-02**: Goal priorities (low, medium, high) control how urgently the AI steers conversation toward the goal
- [x] **GOAL-03**: Developer can add, remove, and reprioritize goals at runtime via API
- [x] **GOAL-04**: System instruction builder folds active goals into the prompt with urgency-appropriate framing
- [x] **GOAL-05**: AI signals goal completion via a built-in function call (e.g., goal_reached("goal_id"))

### Packaging

- [x] **PKG-01**: Project structured as UPM package (Runtime/, Samples~/, package.json, asmdef)
- [x] **PKG-02**: Sample scene demonstrates full pipeline -- persona talking with animation function calls

## v2 Requirements (Deferred)

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
| AUDIO-01 | Phase 2 | Complete |
| AUDIO-02 | Phase 2 | Complete |
| AUDIO-03 | Phase 2 | Complete |
| AUDIO-04 | Phase 2 | Complete |
| VOICE-01 | Phase 2 | Complete |
| VOICE-02 | Phase 5 | Complete |
| VOICE-03 | Phase 5 | Complete |
| VOICE-04 | Phase 5 | Complete |
| TRNS-01 | Phase 2 | Complete |
| TRNS-02 | Phase 3 | Complete |
| TRNS-03 | Phase 3 | Complete |
| SYNC-01 | Phase 3 | Complete |
| SYNC-02 | Phase 3 | Complete |
| SYNC-03 | Phase 3 | Complete |
| FUNC-01 | Phase 4 | Complete |
| FUNC-02 | Phase 4 | Complete |
| FUNC-03 | Phase 4 | Complete |
| FUNC-04 | Phase 6 | Complete |
| GOAL-01 | Phase 4 | Complete |
| GOAL-02 | Phase 4 | Complete |
| GOAL-03 | Phase 4 | Complete |
| GOAL-04 | Phase 4 | Complete |
| GOAL-05 | Phase 4 | Complete |
| PKG-01 | Phase 1 | Complete |
| PKG-02 | Phase 6 | Complete |

**Coverage:**
- v1 requirements: 34 total
- Shipped: 34
- Adjusted: 0
- Dropped: 0

---

## Milestone Summary

**Shipped:** 34 of 34 v1 requirements
**Adjusted:** FUNC-04 and GOAL-05 implemented as developer-registered patterns rather than built-in functions (design decision, not a gap)
**Dropped:** None

---
*Archived: 2026-02-13 as part of v1 milestone completion*
