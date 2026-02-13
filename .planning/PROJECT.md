# AI Embodiment

## What This Is

A Unity UPM package that lets game developers add AI-powered characters to their games. Developers create persona configurations as ScriptableObjects, attach a PersonaSession component to a GameObject, and get real-time AI conversation with synchronized voice, text, and animation events. Uses a direct WebSocket connection to Gemini Live with dual voice backends: Gemini native audio and custom TTS via an ITTSProvider interface (Chirp 3 HD, ElevenLabs, etc.).

## Core Value

Developers can drop an AI character into their Unity scene and have it talking — with synchronized voice, text, and animation events — in minutes, not weeks.

## Current Milestone: v0.8 WebSocket Migration

**Goal:** Replace Firebase AI Logic SDK with direct WebSocket client (from Persona Unity library), support audio-only Gemini models, and introduce ITTSProvider abstraction for custom voice cloning.

**Target features:**
- Direct WebSocket transport to Gemini Live (no Firebase dependency)
- Audio-only model support (gemini-2.5-flash-native-audio)
- Dual transcription streams (inputTranscription + outputTranscription)
- ITTSProvider interface with ChirpTTSClient implementation for custom voice cloning
- API key in PersonaConfig (no Firebase project config needed)
- Same public API surface (PersonaSession, PersonaConfig, events, methods)

## Requirements

### Validated

- Developer can create a PersonaConfig ScriptableObject with personality, voice settings, and model selection — v1
- Developer can add a PersonaSession MonoBehaviour to a GameObject and assign a PersonaConfig — v1
- Audio capture from user's microphone via Unity Microphone API (16kHz PCM) — v1
- Audio playback of AI responses through a standard Unity AudioSource component — v1
- Two voice paths: Gemini native audio and Chirp 3 HD TTS — v1
- Voice backend selected per-persona in the ScriptableObject config — v1
- PacketAssembler synchronizes text chunks, audio data, and emote timing into unified packets — v1
- Function calling system with C# delegates for AI-triggered game actions — v1
- System instruction generation from persona config (archetype, traits, backstory, speech patterns) — v1
- Conversational goals with priority-based urgency steering — v1
- Distributed as UPM package with sample scene — v1

### Active

- [ ] PersonaSession connects to Gemini Live via direct WebSocket (replacing Firebase AI Logic)
- [ ] API key provided via PersonaConfig ScriptableObject field
- [ ] Audio-only Gemini models supported (AUDIO response modality)
- [ ] Dual transcription: inputTranscription (user STT) and outputTranscription (AI speech text)
- [ ] ITTSProvider interface abstracting TTS backends
- [ ] ChirpTTSClient implements ITTSProvider for custom voice cloning
- [ ] Chirp TTS path works with outputTranscription from audio-only models
- [ ] Firebase AI Logic SDK fully removed as dependency
- [ ] Newtonsoft.Json replaces MiniJSON for serialization
- [ ] All existing public API surface preserved (events, methods, components)

### Out of Scope

- Runtime voice switching mid-session — voice is set per-persona at connect time
- Persistent conversation memory/SQLite — defer to future version
- Platform-specific native audio plugins — Unity Microphone API is sufficient
- Visual UI components (chat window, text input) — headless library, devs build their own UI
- Mobile-specific optimizations — desktop-first
- ElevenLabs TTS implementation — ITTSProvider interface enables it, but only ChirpTTSClient ships in v0.8

## Context

- **Shipped v1:** 3,542 lines of C#/UXML/USS across 6 phases, 17 plans (Firebase AI Logic SDK)
- **Reference implementation**: Persona Unity library at `/home/cachy/workspaces/projects/persona/unity/Persona` — direct WebSocket client, GeminiLiveClient (839 lines), audio-only modality, ring buffer playback, dual transcription
- **Google 2.0-flash sunset**: Gemini 2.0 flash models deprecated; audio-only models (gemini-2.5-flash-native-audio) are the successor
- **Transcription change**: Audio-only models provide `outputTranscription` (AI speech text) and `inputTranscription` (user STT) as separate fields, not inline text content
- **Custom voice cloning**: Chirp 3 HD custom voices allow persona-specific voice identity via voiceCloningKey
- **Target Unity version**: 6000.3.7f1 (Unity 6)
- **Audio formats**: Capture at 16kHz mono PCM, Gemini native output at 24kHz mono PCM

## Constraints

- **Tech stack**: Unity 6 / C# — must feel native to Unity developers (ScriptableObjects, MonoBehaviours, AudioSource)
- **No Firebase dependency**: Package must work with just a Gemini API key, no Firebase project config
- **API keys**: Gemini API key in PersonaConfig, Chirp TTS API key as separate credential
- **Threading**: Unity main thread constraint — WebSocket receive loop and HTTP responses must marshal to main thread
- **Package format**: UPM (Unity Package Manager) — installable via git URL or local path
- **JSON library**: Newtonsoft.Json (available in Unity 6 via com.unity.nuget.newtonsoft-json)
- **Public API**: v1 public surface (PersonaSession events/methods, PersonaConfig fields, component names) must be preserved

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| UPM package, not Asset Store asset | Clean dependency management, proper versioning, devs can update via git | Good |
| ~~Firebase AI Logic for Gemini Live~~ | ~~Already imported, Google-supported~~ | Superseded — removing in v0.8 |
| Direct WebSocket to Gemini Live | Zero dependency, full protocol control, matches Persona library pattern | — Pending |
| ScriptableObjects for persona config | Unity-native, Inspector-editable, no custom editor needed for basic use | Good |
| C# delegates for function handlers | More flexible than UnityEvents, devs write typed handlers, composable | Good |
| ITTSProvider interface for TTS backends | Enables Chirp now, ElevenLabs later, clean abstraction boundary | — Pending |
| Newtonsoft.Json for serialization | Unity 6 ships it, more capable than MiniJSON, standard .NET ecosystem | — Pending |
| Audio-only Gemini models | 2.0-flash sunset, audio modality is the future, lower latency | — Pending |
| API key in PersonaConfig | Simpler than Firebase project config, developer sets in Inspector | — Pending |
| Unity Microphone API for audio capture | Cross-platform, sufficient quality, no native plugin complexity | Good |
| AudioSource for playback | Devs can spatialize, apply effects, mix — standard Unity audio pipeline | Good |
| Lock-free SPSC ring buffer for audio | Zero allocations in OnAudioFilterRead, absorbs network jitter | Good |
| Per-persona voice config (not runtime toggle) | Simpler architecture, voice backend set at session creation time | Good |
| ISyncDriver interface for timing control | Extension point for TTS timing, face animation, future drivers | Good |

---
*Last updated: 2026-02-13 after v0.8 milestone start*
