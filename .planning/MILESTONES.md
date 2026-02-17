# Project Milestones: AI Embodiment

## v0.8 WebSocket Migration (Shipped: 2026-02-17)

**Delivered:** Replaced Firebase AI Logic SDK with direct WebSocket transport to Gemini Live, added audio-only model support, ITTSProvider abstraction, push-to-talk sample scene, and OAuth2 bearer token auth for custom voice cloning.

**Phases completed:** 7-11.2 (14 plans total)

**Key accomplishments:**

- Built GeminiLiveClient WebSocket client with ConcurrentQueue event infrastructure, receive loop, and PCM audio conversion for all Gemini Live server message types
- Deleted Firebase SDK (149 files), created AIEmbodimentSettings, and rewired PersonaSession with main-thread event bridge
- Created ITTSProvider interface abstraction with ChirpTTSClient as pluggable TTS backend and provider-agnostic routing
- Implemented FunctionDeclaration typed builder with dual-path output (native JSON and prompt-based) and complete function calling pipeline
- Verified end-to-end flow with 8 audio/streaming bug fixes (audioStreamEnd, mic suppression, ring buffer, watermark)
- Built 5-state push-to-talk QueuedResponseSample with audio buffering and UI Toolkit transcript layout
- Created GoogleServiceAccountAuth with JWT RS256 signing and dual-auth ChirpTTSClient for custom voice cloning

**Stats:**

- 106 files created/modified
- 4,296 lines of C# (runtime package)
- 7 phases, 14 plans, 73 commits
- 5 days from start to ship (2026-02-13 to 2026-02-17)

**Git range:** `feat(07-01)` to `docs(11.1)`

**Tech debt accepted:** RSA.ImportPkcs8PrivateKey may fail on Android IL2CPP (editor-only for now)

**What's next:** TBD

---

## v1 MVP (Shipped: 2026-02-13)

**Delivered:** Unity UPM package enabling developers to add AI-powered conversational characters with synchronized voice, text, and animation events via Gemini Live and Chirp 3 HD TTS.

**Phases completed:** 1-6 (17 plans total)

**Key accomplishments:**

- PersonaSession MonoBehaviour with multi-turn Gemini Live receive loop, solving the single-turn trap with thread-safe main thread dispatching
- End-to-end bidirectional audio pipeline with push-to-talk API, real-time voice playback via lock-free ring buffer, and barge-in interruption handling
- PacketAssembler correlating audio, transcription, and function call events into unified SyncPackets with sentence boundary detection
- Function calling dispatch pipeline with typed argument accessors, auto-response round-trip, and cancellation tracking
- Chirp 3 HD TTS alternative voice backend with 30+ voices, sentence-by-sentence and full-response synthesis modes
- Sample scene (AyaLiveStream) demonstrating full pipeline with dark chat UI, push-to-talk, emote functions, and goal injection

**Stats:**

- 209 files created/modified
- 3,542 lines of C#/UXML/USS
- 6 phases, 17 plans
- 1 day from start to ship (2026-02-05)

**Git range:** initial commit → `6a26b91`

**Tech debt accepted:** Scene file wiring gaps (Editor state not saved to disk), missing .meta files (auto-generated)

---
