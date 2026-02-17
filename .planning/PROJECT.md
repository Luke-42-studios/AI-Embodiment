# AI Embodiment

## What This Is

A Unity UPM package that lets game developers add AI-powered characters to their games. Developers create persona configurations as ScriptableObjects, attach a PersonaSession component to a GameObject, and get real-time AI conversation with synchronized voice, text, and animation events. Uses a direct WebSocket connection to Gemini Live with dual voice backends: Gemini native audio and custom TTS via an ITTSProvider interface (Chirp 3 HD with custom voice cloning via OAuth2 service account auth).

## Core Value

Developers can drop an AI character into their Unity scene and have it talking — with synchronized voice, text, and animation events — in minutes, not weeks.

## Current State

**Shipped:** v0.8 WebSocket Migration (2026-02-17)
**Previous:** v1 MVP (2026-02-13)

Two milestones shipped. The package provides:
- Direct WebSocket transport to Gemini Live (zero Firebase dependency)
- Audio-only model support (gemini-2.5-flash-native-audio)
- Dual transcription streams (inputTranscription + outputTranscription)
- ITTSProvider interface with ChirpTTSClient for custom voice cloning
- OAuth2 bearer token auth for Chirp Custom Voice (service account JWT)
- Push-to-talk QueuedResponse sample scene
- Function calling with dual-path (native + prompt-based)
- Conversational goals with priority steering

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
- PersonaSession connects to Gemini Live via direct WebSocket (replacing Firebase AI Logic) — v0.8
- API key provided via AIEmbodimentSettings ScriptableObject — v0.8
- Audio-only Gemini models supported (AUDIO response modality) — v0.8
- Dual transcription: inputTranscription (user STT) and outputTranscription (AI speech text) — v0.8
- ITTSProvider interface abstracting TTS backends — v0.8
- ChirpTTSClient implements ITTSProvider for custom voice cloning — v0.8
- Chirp TTS path works with outputTranscription from audio-only models — v0.8
- Firebase AI Logic SDK fully removed as dependency — v0.8
- Newtonsoft.Json replaces MiniJSON for serialization — v0.8
- All existing public API surface preserved (events, methods, components) — v0.8

### Active

(None — next milestone not yet defined)

### Out of Scope

- Runtime voice switching mid-session — voice is set per-persona at connect time
- Persistent conversation memory/SQLite — defer to future version
- Platform-specific native audio plugins — Unity Microphone API is sufficient
- Visual UI components (chat window, text input) — headless library, devs build their own UI
- Mobile-specific optimizations — desktop-first
- ElevenLabs TTS implementation — ITTSProvider interface enables it, but only ChirpTTSClient ships

## Context

- **Shipped v1:** 3,542 lines of C#/UXML/USS across 6 phases, 17 plans (Firebase AI Logic SDK)
- **Shipped v0.8:** 4,296 lines of C# runtime, 7 phases, 14 plans (direct WebSocket, zero Firebase)
- **Total codebase:** ~7,800 lines across runtime, editor, and samples
- **Reference implementation**: Persona Unity library at `/home/cachy/workspaces/projects/persona/unity/Persona`
- **Google 2.0-flash sunset**: Gemini 2.0 flash models deprecated; audio-only models (gemini-2.5-flash-native-audio) are the successor
- **Custom voice cloning**: Chirp 3 HD custom voices via voiceCloningKey and OAuth2 service account bearer tokens on v1beta1 endpoint
- **Target Unity version**: 6000.3.7f1 (Unity 6)
- **Audio formats**: Capture at 16kHz mono PCM, Gemini native output at 24kHz mono PCM
- **Sample scenes**: AyaLiveStream (standard) and QueuedResponseSample (push-to-talk with transcript approval)

## Constraints

- **Tech stack**: Unity 6 / C# — must feel native to Unity developers (ScriptableObjects, MonoBehaviours, AudioSource)
- **No Firebase dependency**: Package must work with just a Gemini API key, no Firebase project config
- **API keys**: Gemini API key in AIEmbodimentSettings, Chirp TTS via API key or service account
- **Threading**: Unity main thread constraint — WebSocket receive loop and HTTP responses must marshal to main thread
- **Package format**: UPM (Unity Package Manager) — installable via git URL or local path
- **JSON library**: Newtonsoft.Json (available in Unity 6 via com.unity.nuget.newtonsoft-json)
- **Public API**: v1 public surface (PersonaSession events/methods, PersonaConfig fields, component names) must be preserved

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| UPM package, not Asset Store asset | Clean dependency management, proper versioning, devs can update via git | Good |
| ~~Firebase AI Logic for Gemini Live~~ | ~~Already imported, Google-supported~~ | Superseded — removed in v0.8 |
| Direct WebSocket to Gemini Live | Zero dependency, full protocol control, matches Persona library pattern | Good |
| ScriptableObjects for persona config | Unity-native, Inspector-editable, no custom editor needed for basic use | Good |
| C# delegates for function handlers | More flexible than UnityEvents, devs write typed handlers, composable | Good |
| ITTSProvider interface for TTS backends | Enables Chirp now, ElevenLabs later, clean abstraction boundary | Good |
| Newtonsoft.Json for serialization | Unity 6 ships it, more capable than MiniJSON, standard .NET ecosystem | Good |
| Audio-only Gemini models | 2.0-flash sunset, audio modality is the future, lower latency | Good |
| API key in AIEmbodimentSettings | Simpler than Firebase project config, developer sets in Inspector | Good |
| Unity Microphone API for audio capture | Cross-platform, sufficient quality, no native plugin complexity | Good |
| AudioSource for playback | Devs can spatialize, apply effects, mix — standard Unity audio pipeline | Good |
| Lock-free SPSC ring buffer for audio | Zero allocations in OnAudioFilterRead, absorbs network jitter | Good |
| Per-persona voice config (not runtime toggle) | Simpler architecture, voice backend set at session creation time | Good |
| ISyncDriver interface for timing control | Extension point for TTS timing, face animation, future drivers | Good |
| Manual JWT RS256 signing | ~200 lines vs Google.Apis.Auth NuGet dependency, editor-only usage | Good |
| PersonaSession owns auth lifetime | Caller-owns pattern for GoogleServiceAccountAuth, clean dispose | Good |
| Prompt-based function calling as default | UseNativeFunctionCalling=false, more reliable than native toolCall | Good |
| 5-state QueuedResponse controller | Clean state machine for push-to-talk UX, decoupled from PersonaSession audio | Good |

---
*Last updated: 2026-02-17 after v0.8 milestone completion*
