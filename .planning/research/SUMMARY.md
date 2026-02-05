# Project Research Summary

**Project:** AI Embodiment (Unity UPM Package)
**Domain:** Real-time AI character conversation SDK for Unity
**Researched:** 2026-02-05
**Confidence:** HIGH

## Executive Summary

AI Embodiment is a Unity UPM package that wraps Firebase AI Logic SDK (Gemini Live) into a developer-friendly conversation pipeline for AI-powered game characters. The project has a significant advantage: the Firebase AI Logic SDK 13.7.0 source code is already in the project, giving full visibility into the WebSocket protocol, audio format, and function calling wire format. The recommended approach is a five-stage pipeline architecture (Capture, Transport, Processing, Assembly, Presentation) built as a headless library with no UI opinions -- developers bring their own AudioSource, Animator, and UI. The dual voice backend (Gemini native for low latency, Chirp 3 HD for voice variety) is a genuine differentiator over competitors like Convai and Inworld, who lock developers into proprietary voice pipelines.

The core technical challenge is threading: Firebase's async WebSocket receive loop runs on a background thread while every Unity API call must happen on the main thread. This is not optional or deferrable -- it must be solved in the first phase with a MainThreadDispatcher pattern using ConcurrentQueue. The second hardest problem is streaming audio playback: Unity's AudioClip was not designed for append-style streaming, so a ring-buffer approach with write-ahead watermarking is needed to avoid pops, silence gaps, and race conditions. Both problems are well-understood with established patterns, but getting them wrong causes architectural collapse.

The primary risks are: (1) the Firebase AI Logic SDK is in Public Preview and can break without notice -- the package must wrap all Firebase types in package-owned types at the boundary; (2) the ReceiveAsync API closes on TurnComplete (single-turn trap) requiring an outer re-call loop; (3) audio sample rate mismatches between input (16kHz) and output (24kHz) will produce chipmunk/demon voice if not handled explicitly. All three risks have known mitigations detailed in this research. The PacketAssembler -- which synchronizes text, audio, and animation events into coherent packets -- is the project's most novel contribution and its primary differentiator against all known competitors.

## Key Findings

### Recommended Stack

The stack is almost entirely determined by what is already in the project. Unity 6 (6000.3.7f1), C# 9.0, .NET Standard 2.1 form the runtime foundation. Firebase AI Logic SDK 13.7.0 provides the Gemini Live bidirectional streaming session. Google Cloud TTS REST API provides Chirp 3 HD voice synthesis as a separate HTTP call. All audio flows through Unity's built-in Microphone, AudioSource, and AudioClip APIs. No third-party audio middleware, async libraries, or WebSocket libraries should be added -- everything needed is already present.

**Core technologies:**
- **Firebase AI Logic SDK 13.7.0:** Gemini Live bidirectional streaming with function calling -- already imported as source, provides full protocol visibility
- **Unity 6 Audio APIs (Microphone, AudioSource, AudioClip):** Cross-platform audio capture and playback -- standard, stable, no external dependencies
- **Google Cloud TTS REST API (Chirp 3 HD):** High-quality voice synthesis with 30+ voices -- called via UnityWebRequest, same Google Cloud project billing
- **System.Threading.Channels / ConcurrentQueue:** Thread-safe handoff between WebSocket background thread and Unity main thread -- built into .NET Standard 2.1
- **ScriptableObject (PersonaConfig):** Inspector-editable persona configuration -- Unity-native, serializable, zero custom editor needed for basic use

**What NOT to use:** UniTask (forces dependency on package consumers), System.Net.Http.HttpClient (breaks on IL2CPP/mobile), third-party WebSocket libraries (Firebase SDK already manages its own), FMOD/Wwise (overkill, adds user-facing dependency), Firebase Auth SDK (not needed for v1).

See [STACK.md](./STACK.md) for full API reference, code samples, and version pinning details.

### Expected Features

The feature landscape was analyzed against six competitors (Convai, Inworld AI, Charisma.ai, Replica Studios, NVIDIA ACE, ReadyPlayerMe). AI Embodiment is NOT a full NPC platform -- it is a focused conversation pipeline SDK. This distinction is critical for feature scoping.

**Must have (table stakes):**
- Microphone capture to Gemini at 16kHz PCM with permission handling
- AI audio response playback through developer-assigned AudioSource
- At least one working TTS path (Gemini native audio)
- Speech-to-text transcription for UI/logs (SDK already supports this)
- System instruction / persona prompt via ScriptableObject
- Bidirectional real-time streaming (not request/response)
- Turn management (TurnComplete, Interrupted flags)
- Interruption handling (stop audio playback on user interrupt)
- MonoBehaviour component with drag-and-drop setup
- Function calling with C# delegate handler registration
- Session lifecycle management (connect, disconnect, reconnect, dispose)
- Error events and connection state callbacks

**Should have (differentiators):**
- PacketAssembler (text + audio + emote timing sync) -- no competitor offers this as a composable primitive
- Dual voice backend (Gemini native + Chirp 3 HD) -- uncommon flexibility
- Headless library architecture (no UI shipped) -- avoids the "this doesn't fit my game" rejection
- Template variable system in persona prompts ({player_name}, {location})
- Sample scene proving full pipeline in under 5 minutes

**Defer (v2+):**
- Persistent conversation memory / history export
- Custom voice cloning (Instant Custom Voice)
- Voice activity detection (use Gemini's built-in turn detection for v1)
- Lip sync / viseme data (expose hooks, do not implement)
- Multiplayer support (design single-player cleanly first)

See [FEATURES.md](./FEATURES.md) for complete feature tables, anti-features list, and dependency graph.

### Architecture Approach

The architecture is a five-stage pipeline: Capture (Microphone -> PCM), Transport (LiveSession WebSocket), Processing (PacketAssembler correlates chunks), Assembly (PersonaPacket structs), and Presentation (AudioPlayback + FunctionCallHandler). Each stage has a single owner component with well-defined inputs and outputs. The threading boundary is explicit: everything before the MainThreadDispatcher can run on background threads; everything after it runs on the main thread. Two data paths exist: Path 1 (Gemini Native Audio) where audio arrives inline with the WebSocket stream, and Path 2 (Chirp 3 HD TTS) where text is extracted from the stream and sent to a separate HTTP endpoint for synthesis.

**Major components:**
1. **PersonaSession** (MonoBehaviour) -- lifecycle owner, connects LiveSession, owns the receive loop, exposes C# events
2. **AudioCapture** -- reads Unity Microphone into PCM chunks, sends to LiveSession at 100ms intervals
3. **AudioPlayback** -- receives PCM audio, manages ring-buffer AudioClip, streams to AudioSource
4. **PacketAssembler** -- correlates text/audio/function-call chunks into ordered PersonaPacket structs with timing
5. **FunctionCallHandler** -- registry of C# delegates keyed by function name, dispatches on main thread, returns responses
6. **MainThreadDispatcher** -- ConcurrentQueue-based marshaling from background WebSocket thread to Unity Update()
7. **ChirpTTSClient** -- HTTP POST to Cloud TTS, returns PCM audio from text
8. **PersonaConfig** (ScriptableObject) -- personality, voice, model, tools, safety settings
9. **SystemInstructionBuilder** -- pure function composing PersonaConfig fields into a Gemini system instruction

See [ARCHITECTURE.md](./ARCHITECTURE.md) for component boundaries, data flow diagrams, threading model, and build order.

### Critical Pitfalls

18 pitfalls were identified (7 critical, 7 moderate, 4 minor). The top 5 that must inform roadmap design:

1. **Threading: Firebase async vs Unity main thread** -- ReceiveAsync runs on IO thread pool; touching any Unity API from there causes crashes or silent corruption. Solve with MainThreadDispatcher + ConcurrentQueue in the very first phase. Every subsequent feature depends on this.

2. **ReceiveAsync single-turn trap** -- The SDK's ReceiveAsync() breaks on TurnComplete. Developers must wrap it in an outer while loop to sustain multi-turn conversation. Failure looks like "session dies after first response."

3. **Audio sample rate mismatch (16kHz in, 24kHz out)** -- Input to Gemini is 16kHz PCM; output from Gemini/Chirp is 24kHz. Creating AudioClip at the wrong rate produces chipmunk or demon voice. Must create separate AudioClip configs for capture vs playback.

4. **Streaming AudioClip.SetData race condition** -- AudioSource reads while SetData writes. Without a write-ahead watermark (200-400ms buffer before starting playback), audio has pops, silence gaps, and garbled chunks. This is the hardest technical problem in the project.

5. **WebSocket lifetime vs Unity lifecycle mismatch** -- Scene transitions, OnDestroy, and application quit can leak WebSocket connections or crash background threads. Must use CancellationTokenSource tied to MonoBehaviour lifecycle from day one.

See [PITFALLS.md](./PITFALLS.md) for all 18 pitfalls with source code references, warning signs, and prevention strategies.

## Implications for Roadmap

Based on combined research, the project naturally decomposes into 6 phases ordered by dependency. Each phase is independently testable before moving to the next.

### Phase 1: Foundation and Core Session

**Rationale:** Everything depends on correct threading and session management. The MainThreadDispatcher, PersonaConfig, and LiveSession wrapper must exist before any feature can work. This phase has the highest pitfall density (5 critical pitfalls apply here).
**Delivers:** Working bidirectional connection to Gemini Live. Text can be sent and received. Session connects, disconnects, and survives scene transitions cleanly.
**Addresses:** PersonaConfig ScriptableObject, PersonaSession MonoBehaviour, session lifecycle management, error events, connection state
**Avoids:** Threading pitfall (#1), ReceiveAsync single-turn trap (#2), concurrent enumeration (#3), WebSocket lifecycle mismatch (#7), SetupComplete timing (#13), Dispose pattern (#18)
**Components:** MainThreadDispatcher, PersonaConfig, SessionState, PersonaPacket, PersonaSession (connect/receive), SystemInstructionBuilder

### Phase 2: Audio Pipeline

**Rationale:** Audio capture and playback are the two halves of voice conversation. They can be built in parallel with each other but require Phase 1's session management to integrate. This phase contains the hardest technical problem (streaming playback).
**Delivers:** User speaks into microphone, audio reaches Gemini. Gemini's native audio response plays through AudioSource. Full voice conversation loop working.
**Addresses:** Microphone capture, AI audio response playback, audio streaming, voice selection per character
**Avoids:** Sample rate mismatch (#4), streaming AudioClip race condition (#5), base64 encoding overhead (#6), microphone platform differences (#10), ring buffer overflow (#16)
**Components:** AudioCapture, AudioPlayback, AudioConverter (Internal)

### Phase 3: PacketAssembler and Synchronization

**Rationale:** With audio working, the PacketAssembler can be built to correlate text, audio, and events. This is the project's primary differentiator and depends on both the session receive loop (Phase 1) and audio playback (Phase 2) being functional.
**Delivers:** Synchronized text + audio + event packets. Subtitles match speech timing. Foundation for animation sync.
**Addresses:** PacketAssembler (text + audio + emote timing sync), streaming chunk coordination, turn management, interruption handling, speech-to-text transcription
**Avoids:** Text/audio synchronization drift (#11)
**Components:** PacketAssembler, SentenceBoundaryDetector (Internal)

### Phase 4: Function Calling

**Rationale:** Function calling is architecturally independent from audio but depends on the session receive loop (Phase 1). It can technically be built alongside Phase 2-3, but testing it properly requires the full conversation loop. Building it after PacketAssembler means function call timing can be synchronized with speech.
**Delivers:** AI can trigger game actions. Developers register C# delegates. Built-in emote function demonstrates the pattern.
**Addresses:** Declare callable functions, handler registration, function response round-trip, emote/animation with timing, configurable safety settings
**Avoids:** Function call response timing (#9)
**Components:** FunctionCallHandler, built-in emote function example

### Phase 5: Chirp 3 HD Voice Backend

**Rationale:** The Gemini native audio path (Phase 2) should be working and stable before adding the alternative Chirp TTS path. Chirp adds HTTP latency and sentence-chunking complexity that is best layered on top of a proven audio pipeline.
**Delivers:** 30+ high-quality voices via Chirp 3 HD. Per-persona voice backend selection (Gemini native vs Chirp).
**Addresses:** Dual voice backend, per-persona voice backend selection, Chirp TTS integration
**Avoids:** Chirp TTS latency addition (#12), sensitive data in ScriptableObject (#14)
**Components:** ChirpTTSClient, VoiceBackendRouter

### Phase 6: UPM Packaging, Editor Tools, and Samples

**Rationale:** Packaging is last because the runtime API must be stable before creating assembly definitions, editor inspectors, and sample scenes. This phase is about developer experience, not functionality.
**Delivers:** Installable UPM package via git URL. Sample scene that proves the full pipeline in under 5 minutes. Custom inspectors for PersonaConfig.
**Addresses:** Sample scene, minimal dependency footprint, MonoBehaviour drag-and-drop setup, UPM distribution
**Avoids:** Firebase dependency hell (#8), asmdef structure issues (#17), AudioSource 3D spatial gotchas (#15)
**Components:** package.json, assembly definitions, PersonaConfigEditor, PersonaSessionEditor, sample scenes, documentation

### Phase Ordering Rationale

- **Phase 1 before all else:** 5 of 7 critical pitfalls apply to the session/threading layer. Getting this wrong cascades into every subsequent phase.
- **Phase 2 immediately after:** Voice conversation is the core product. Without working audio, nothing can be tested end-to-end.
- **Phase 3 after audio:** PacketAssembler needs real audio data to test synchronization. It cannot be validated without a working audio pipeline.
- **Phase 4 after sync:** Function calls need timing correlation with speech, which depends on PacketAssembler. Building function calling earlier is possible but testing is limited.
- **Phase 5 is additive:** Chirp 3 HD is an alternative voice path layered on top of a working Gemini native path. It does not block MVP.
- **Phase 6 is polish:** UPM packaging wraps stable code. Doing it earlier means repackaging after every API change.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Audio Pipeline):** The streaming AudioClip playback pattern (ring buffer vs OnAudioFilterRead vs double-buffer) needs prototyping to choose the right approach. The Gemini output audio sample rate (24kHz) has MEDIUM confidence and must be verified with actual API responses.
- **Phase 3 (PacketAssembler):** Text/audio synchronization is a design problem, not just implementation. The SDK explicitly states transcriptions are "independent to the Content" with no ordering guarantees. Needs careful protocol analysis.
- **Phase 5 (Chirp TTS):** Chirp 3 HD voice name format, available voices, and exact API request/response format need verification against current Google Cloud docs. Training data may be stale.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Foundation):** ConcurrentQueue dispatcher, ScriptableObject config, and CancellationToken lifecycle are well-documented Unity patterns.
- **Phase 4 (Function Calling):** The Firebase SDK's function calling API is fully documented in the source code. C# delegate registration is straightforward.
- **Phase 6 (UPM Packaging):** UPM package layout is a stable convention since Unity 2019. Assembly definitions are well-documented.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Primary source was actual SDK source code in the project. All API details confirmed from code. Only Chirp 3 HD API format and Gemini output sample rate are MEDIUM. |
| Features | HIGH (table stakes), MEDIUM (competitors) | Table stakes verified against SDK capabilities. Competitor feature comparison based on training data through May 2025, not live docs. |
| Architecture | HIGH | Five-stage pipeline derived from SDK source code analysis and Unity threading constraints. Build order follows actual dependency graph. |
| Pitfalls | HIGH (SDK-derived), MEDIUM (platform/audio) | 10 of 18 pitfalls verified directly against SDK source code with line numbers. Platform-specific audio and UPM pitfalls based on training data. |

**Overall confidence:** HIGH

### Gaps to Address

- **Gemini Live output audio sample rate:** Assumed 24kHz based on training data. Must be verified by inspecting actual response audio during first integration test in Phase 2.
- **Chirp 3 HD voice name format:** Assumed `{lang}-{region}-Chirp3-HD-{VoiceName}` pattern. Must be verified against live Google Cloud TTS docs before Phase 5.
- **Firebase AI Logic SDK VertexAI bug:** Source inspection revealed ConnectAsync hardcodes VertexAI-style model path regardless of backend. Recommendation to use VertexAI backend needs verification -- may be fixed in newer SDK versions.
- **Firebase SDK update cadence:** SDK is 13.7.0. A newer version may be available. Check before Phase 1 implementation begins.
- **Competitor feature sets:** Convai, Inworld AI, and others may have updated their offerings since May 2025. Re-verify if competitive positioning matters for marketing.
- **ResponseModality.Audio output format:** Whether audio response includes a sample rate header or must be assumed -- affects AudioClip creation in Phase 2.

## Sources

### Primary (HIGH confidence)
- Firebase AI Logic SDK 13.7.0 source code: `Assets/Firebase/FirebaseAI/` -- LiveSession.cs, LiveGenerativeModel.cs, LiveSessionResponse.cs, FunctionCalling.cs, ModelContent.cs, LiveGenerationConfig.cs, ResponseModality.cs
- Unity project configuration: `Packages/manifest.json`, `Assembly-CSharp.csproj`, `ProjectSettings/ProjectSettings.asset`
- Firebase version manifest: `Assets/Firebase/Editor/FirebaseAI_version-13.7.0_manifest.txt`
- Project specification: `.planning/PROJECT.md`
- Codebase analysis: `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/INTEGRATIONS.md`, `.planning/codebase/CONCERNS.md`

### Secondary (MEDIUM confidence)
- Unity Microphone/AudioSource/AudioClip API behavior -- stable APIs, unlikely changed in Unity 6
- UPM package layout conventions -- stable since Unity 2019
- Gemini Live protocol details (sample rates, turn semantics) -- inferred from SDK source + training data
- Competitor feature sets (Convai, Inworld AI, Charisma.ai, Replica Studios, NVIDIA ACE) -- training data through May 2025

### Tertiary (LOW confidence)
- Chirp 3 HD TTS latency characteristics -- training data only, must validate with actual API calls
- Chirp 3 HD voice name format and available voices -- may have been updated since training cutoff

---
*Research completed: 2026-02-05*
*Ready for roadmap: yes*
