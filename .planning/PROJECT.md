# AI Embodiment

## What This Is

A Unity UPM package that lets game developers add AI-powered characters to their games. Developers create persona configurations as ScriptableObjects, attach a PersonaSession component to a GameObject, and get real-time AI conversation with synchronized voice, text, and animation events. Built on Firebase AI Logic for Gemini Live and a ported Chirp 3 HD TTS client for custom voices.

## Core Value

Developers can drop an AI character into their Unity scene and have it talking — with synchronized voice, text, and animation events — in minutes, not weeks.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Developer can create a PersonaConfig ScriptableObject with personality, voice settings, and model selection
- [ ] Developer can add a PersonaSession MonoBehaviour to a GameObject and assign a PersonaConfig
- [ ] PersonaSession connects to Gemini Live via Firebase AI Logic and streams bidirectional audio/text
- [ ] Audio capture from user's microphone via Unity Microphone API (16kHz PCM)
- [ ] Audio playback of AI responses through a standard Unity AudioSource component
- [ ] Two voice paths: Gemini native audio (Puck, Kore, Aoede, Charon, Fenrir) and Chirp 3 HD TTS
- [ ] Voice backend selected per-persona in the ScriptableObject config
- [ ] Chirp TTS client ported to Unity — HTTP to Cloud TTS API, returns PCM audio
- [ ] PacketAssembler synchronizes text chunks, audio data, and emote timing into unified packets
- [ ] Function calling system with C# delegates — devs register handlers for AI-triggered functions
- [ ] Emote/animation function as built-in example (AI calls emote("wave"), dev's handler fires)
- [ ] System instruction generation from persona config (archetype, traits, backstory, speech patterns)
- [ ] Distributed as UPM package — devs install Firebase AI Logic separately, then add this package
- [ ] Sample scene demonstrating full pipeline: persona talking with animations triggered by function calls

### Out of Scope

- Runtime voice switching mid-session — voice is set per-persona at connect time
- OAuth/service account auth in the package — devs configure Firebase project auth separately
- Persistent conversation memory/SQLite — defer to future version
- Custom voice cloning (Instant Custom Voice) — defer to future version, Chirp preset voices only for v1
- Platform-specific native audio plugins — Unity Microphone API is sufficient for v1
- Visual UI components (chat window, text input) — this is a headless library, devs build their own UI
- Mobile-specific optimizations — desktop-first, mobile tested but not optimized

## Context

- **Rebuilding from**: The Persona C++ library at `/home/cachy/workspaces/projects/persona` — a C library with WebSocket Gemini Live integration, Chirp TTS, packet assembler, persona config, and function calling
- **Existing code**: This Unity project already has Firebase AI Logic SDK imported (`Assets/Firebase/FirebaseAI/`) including `LiveGenerativeModel`, `LiveSession`, and `LiveSessionResponse` classes that handle the Gemini Live WebSocket protocol
- **Firebase AI Logic SDK**: Provides `LiveGenerativeModel.ConnectAsync()` which returns a `LiveSession` for bidirectional streaming. Supports text and audio response modalities, function calling via `Tool` declarations, and safety settings
- **Target Unity version**: 6000.3.7f1 (Unity 6)
- **Audio formats**: Capture at 16kHz mono PCM (Gemini input), playback at 24kHz mono PCM (Chirp output) or Gemini native audio format
- **Original Persona pipeline**: Microphone → PCM → Gemini Live (WebSocket) → text/audio response → PacketAssembler (sync text + audio + emotes) → AudioSource playback + animation events

## Constraints

- **Tech stack**: Unity 6 / C# — must feel native to Unity developers (ScriptableObjects, MonoBehaviours, AudioSource)
- **Dependency**: Firebase AI Logic Unity SDK must be installed separately by the developer
- **API keys**: Gemini API key via Firebase project config, Chirp TTS API key as a separate developer-provided credential
- **Threading**: Unity main thread constraint — Firebase callbacks and HTTP responses must marshal back to main thread for AudioSource/component access
- **Package format**: UPM (Unity Package Manager) — installable via git URL or local path

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| UPM package, not Asset Store asset | Clean dependency management, proper versioning, devs can update via git | — Pending |
| Firebase AI Logic for Gemini Live | Already imported in project, Google-supported SDK, handles WebSocket protocol | — Pending |
| ScriptableObjects for persona config | Unity-native, Inspector-editable, no custom editor needed for basic use | — Pending |
| C# delegates for function handlers | More flexible than UnityEvents, devs write typed handlers, composable | — Pending |
| Separate Chirp TTS client (not Firebase) | Firebase AI Logic doesn't include TTS — need direct HTTP to Cloud TTS API | — Pending |
| Unity Microphone API for audio capture | Cross-platform, sufficient quality, no native plugin complexity for v1 | — Pending |
| AudioSource for playback | Devs can spatialize, apply effects, mix — standard Unity audio pipeline | — Pending |
| Per-persona voice config (not runtime toggle) | Simpler architecture, voice backend set at session creation time | — Pending |

---
*Last updated: 2026-02-05 after initialization*
