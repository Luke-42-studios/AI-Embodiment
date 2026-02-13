# AI Embodiment

## What This Is

A Unity UPM package that lets game developers add AI-powered characters to their games. Developers create persona configurations as ScriptableObjects, attach a PersonaSession component to a GameObject, and get real-time AI conversation with synchronized voice, text, and animation events. Built on Firebase AI Logic for Gemini Live with dual voice backends: Gemini native audio and Chirp 3 HD TTS.

## Core Value

Developers can drop an AI character into their Unity scene and have it talking — with synchronized voice, text, and animation events — in minutes, not weeks.

## Requirements

### Validated

- Developer can create a PersonaConfig ScriptableObject with personality, voice settings, and model selection — v1
- Developer can add a PersonaSession MonoBehaviour to a GameObject and assign a PersonaConfig — v1
- PersonaSession connects to Gemini Live via Firebase AI Logic and streams bidirectional audio/text — v1
- Audio capture from user's microphone via Unity Microphone API (16kHz PCM) — v1
- Audio playback of AI responses through a standard Unity AudioSource component — v1
- Two voice paths: Gemini native audio and Chirp 3 HD TTS — v1
- Voice backend selected per-persona in the ScriptableObject config — v1
- Chirp TTS client ported to Unity via HTTP to Cloud TTS API — v1
- PacketAssembler synchronizes text chunks, audio data, and emote timing into unified packets — v1
- Function calling system with C# delegates for AI-triggered game actions — v1
- System instruction generation from persona config (archetype, traits, backstory, speech patterns) — v1
- Conversational goals with priority-based urgency steering — v1
- Distributed as UPM package with sample scene — v1

### Active

(No active requirements — next milestone not started)

### Out of Scope

- Runtime voice switching mid-session — voice is set per-persona at connect time
- OAuth/service account auth in the package — devs configure Firebase project auth separately
- Persistent conversation memory/SQLite — defer to future version
- Custom voice cloning (Instant Custom Voice) — defer to future version, Chirp preset voices only for v1
- Platform-specific native audio plugins — Unity Microphone API is sufficient for v1
- Visual UI components (chat window, text input) — this is a headless library, devs build their own UI
- Mobile-specific optimizations — desktop-first, mobile tested but not optimized

## Context

- **Shipped v1:** 3,542 lines of C#/UXML/USS across 6 phases, 17 plans
- **Rebuilt from**: The Persona C++ library at `/home/cachy/workspaces/projects/persona`
- **Firebase AI Logic SDK**: Provides `LiveGenerativeModel.ConnectAsync()` for Gemini Live bidirectional streaming
- **Target Unity version**: 6000.3.7f1 (Unity 6)
- **Audio formats**: Capture at 16kHz mono PCM (Gemini input), playback at 24kHz mono PCM (Chirp output) or Gemini native audio format
- **Known issues**: Firebase VertexAI backend bug (using GoogleAI backend), Gemini audio sample rate assumed 24kHz (unverified), scene file wiring gaps on disk

## Constraints

- **Tech stack**: Unity 6 / C# — must feel native to Unity developers (ScriptableObjects, MonoBehaviours, AudioSource)
- **Dependency**: Firebase AI Logic Unity SDK must be installed separately by the developer
- **API keys**: Gemini API key via Firebase project config, Chirp TTS API key as a separate developer-provided credential
- **Threading**: Unity main thread constraint — Firebase callbacks and HTTP responses must marshal back to main thread for AudioSource/component access
- **Package format**: UPM (Unity Package Manager) — installable via git URL or local path

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| UPM package, not Asset Store asset | Clean dependency management, proper versioning, devs can update via git | Good |
| Firebase AI Logic for Gemini Live | Already imported in project, Google-supported SDK, handles WebSocket protocol | Good |
| ScriptableObjects for persona config | Unity-native, Inspector-editable, no custom editor needed for basic use | Good |
| C# delegates for function handlers | More flexible than UnityEvents, devs write typed handlers, composable | Good |
| Separate Chirp TTS client (not Firebase) | Firebase AI Logic doesn't include TTS — need direct HTTP to Cloud TTS API | Good |
| Unity Microphone API for audio capture | Cross-platform, sufficient quality, no native plugin complexity for v1 | Good |
| AudioSource for playback | Devs can spatialize, apply effects, mix — standard Unity audio pipeline | Good |
| Per-persona voice config (not runtime toggle) | Simpler architecture, voice backend set at session creation time | Good |
| Lock-free SPSC ring buffer for audio | Zero allocations in OnAudioFilterRead, absorbs network jitter | Good |
| Outer while loop for ReceiveAsync | Solves single-turn trap in Gemini Live receive loop | Good |
| async void for MonoBehaviour entry points | Connect/SendText/Disconnect as async entry points with full try-catch | Good |
| MiniJSON for Chirp TTS serialization | Already available via Firebase SDK (Google.MiniJson.dll) | Good |
| ISyncDriver interface for timing control | Extension point for Chirp TTS timing, face animation, future drivers | Good |
| Mid-session goal update via role system | Avoids reconnect; fallback to disconnect/reconnect if rejected | Pending — needs runtime verification |

---
*Last updated: 2026-02-13 after v1 milestone*
