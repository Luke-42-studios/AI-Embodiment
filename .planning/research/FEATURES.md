# Feature Landscape

**Domain:** Unity AI Character / Conversation SDK
**Researched:** 2026-02-05
**Overall Confidence:** MEDIUM (based on training data through May 2025; WebSearch/WebFetch unavailable for live verification)

## Competitor Landscape Context

The feature analysis below is informed by the following known products in this space. Confidence on specific competitor features is MEDIUM since I could not verify against live documentation.

| Product | Type | Key Differentiator |
|---------|------|-------------------|
| Convai | Full-stack AI NPC platform (Unity/Unreal SDK) | Voice + lip sync + actions + memory + knowledge base |
| Inworld AI | Full-stack AI NPC platform (Unity/Unreal SDK) | Goals + emotions + relationships + safety + narrative |
| Charisma.ai | Narrative-focused AI dialogue | Branching storylines, playwright-style authoring |
| Replica Studios | Voice AI for games | High-quality voices, emotion control, lip sync data |
| NVIDIA ACE | AI character toolkit | Audio2Face lip sync, Nemotron LLM, Riva ASR/TTS |
| ReadyPlayerMe + AI | Avatar + AI integration | Cross-platform avatars with AI conversation |

**What AI Embodiment is NOT:** It is not a full-stack NPC platform. It is a focused UPM package: Gemini Live conversation pipeline with synchronized voice/text/animation events. This distinction matters for feature scoping -- many "table stakes" features of full NPC platforms are out of scope for a conversation SDK.

---

## Table Stakes

Features developers expect from an AI character conversation SDK. Missing any of these makes the library feel incomplete or unusable for real projects.

### Voice & Audio Pipeline

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Microphone capture to AI** | Core input mechanism for voice conversation | Medium | Unity Microphone API at 16kHz PCM. Must handle permissions, device selection, silence detection |
| **AI audio response playback** | Users hear the character speak | Medium | Playback through AudioSource. Must handle streaming chunks, sample rate conversion (24kHz Chirp vs Gemini native) |
| **Text-to-speech (at least one path)** | Characters must actually speak | Medium | Already planned: Gemini native audio OR Chirp 3 HD. At minimum one working path |
| **Speech-to-text transcription** | Devs need text of what user said for UI/logs | Low | Firebase AI SDK already supports `InputTranscription` and `OutputTranscription` on LiveSessionContent |
| **Voice selection per character** | Different characters need different voices | Low | Already planned via PersonaConfig ScriptableObject. Map to Gemini voice names (Puck, Kore, Aoede, Charon, Fenrir) or Chirp voices |
| **Audio streaming (not wait-for-complete)** | Latency is the enemy of immersion; users expect sub-second response feel | High | Must buffer and play audio chunks as they arrive, not wait for full response. PacketAssembler handles this |

### Conversation Core

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **System instruction / persona prompt** | Characters need personality, not generic AI | Low | ScriptableObject config generates system instruction. Already planned |
| **Bidirectional real-time streaming** | Conversation, not request/response | Medium | Gemini Live via Firebase SDK handles WebSocket protocol. Wrapper needed for Unity lifecycle |
| **Turn management** | Know when AI is speaking, when user can speak | Medium | `TurnComplete` and `Interrupted` flags exist in LiveSessionContent. Expose cleanly |
| **Interruption handling** | Users expect to be able to cut the AI off mid-sentence | Medium | Gemini Live supports interruption natively. Must stop audio playback immediately and handle `Interrupted` flag |
| **Text fallback** | Some contexts need text instead of or alongside voice | Low | Support text-only mode for accessibility, low-bandwidth, or text-chat UIs |

### Unity Integration

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **MonoBehaviour component** | Unity devs expect drag-and-drop components | Low | PersonaSession MonoBehaviour. Attach to GameObject, assign config, call Connect() |
| **ScriptableObject configuration** | Inspector-editable, no code needed for basic setup | Low | PersonaConfig SO with personality, voice, model selection. Already planned |
| **AudioSource integration** | Standard Unity audio pipeline for spatialization, mixing, effects | Low | Output through dev-assigned AudioSource. Already planned |
| **Main thread marshalling** | Firebase callbacks arrive on background threads; Unity API is main-thread-only | Medium | Critical. Every AudioSource, Transform, or component access must be on main thread |
| **Session lifecycle management** | Connect, disconnect, reconnect, dispose cleanly | Medium | Handle Unity scene transitions, OnDestroy, application quit, network drops |
| **Error events / callbacks** | Devs must know when things fail | Low | Expose connection failures, API errors, safety blocks as C# events |

### Function Calling

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Declare callable functions** | AI needs to trigger game actions (open door, give item, play animation) | Low | Firebase SDK has FunctionDeclaration + Tool. Wrap with dev-friendly registration API |
| **Handler registration** | Devs register C# callbacks for each function | Low | C# delegates/Action<> handlers. Already planned |
| **Function response round-trip** | AI sends function call, game executes, result sent back to AI | Medium | Full lifecycle: LiveSessionToolCall -> execute handler -> SendAsync FunctionResponsePart |

### Content Safety

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Configurable safety settings** | Games have varied content needs (E-rated vs M-rated) | Low | Firebase SDK SafetySetting with HarmCategory + HarmBlockThreshold. Expose in PersonaConfig |
| **Safety event notifications** | Devs need to know when content was blocked | Low | Surface SafetyRating from responses. Fire event when content is blocked |

---

## Differentiators

Features that set AI Embodiment apart from competitors. Not universally expected, but provide competitive advantage. Prioritize based on the project's unique strengths.

### Synchronization (PRIMARY DIFFERENTIATOR)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **PacketAssembler (text + audio + emote timing sync)** | No competitor offers this at the SDK level. Convai/Inworld handle sync internally but don't expose a composable sync primitive | High | This is the original Persona library's strongest innovation. Unified packets with timing data so devs can coordinate text display, audio playback, and animation triggers frame-accurately |
| **Emote/animation function calling with timing** | AI can call emote("wave") and the timing is synchronized with the speech | Medium | Built-in example function that shows the pattern. Most competitors require custom integration for animation timing |
| **Streaming chunk coordination** | Text chunks, audio chunks, and function calls arrive asynchronously but are presented in sync | High | The hard problem. Most devs get this wrong (audio plays while text shows different content, or animations fire too early/late) |

### Architecture Openness (SECONDARY DIFFERENTIATOR)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Headless library (no UI)** | Devs build their own UI. Convai/Inworld ship opinionated UI components that clash with game aesthetics | N/A | This is an architectural choice, not a feature to build. But it is a selling point |
| **Direct Gemini access (not proxied)** | No vendor lock-in to a middleman platform. Direct Firebase AI connection | N/A | Convai/Inworld route through their own servers, adding latency and cost. AI Embodiment goes direct |
| **C# delegates (not visual scripting)** | Composable, type-safe, testable. Power users can build complex systems | Low | UnityEvents are popular but limiting. Delegates are more flexible for programmatic use |
| **Bring-your-own everything** | Dev provides their own AudioSource, Animator, UI. SDK just provides the AI conversation pipe | N/A | Reduces coupling, increases flexibility |

### Persona System (TERTIARY DIFFERENTIATOR)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Template variable system** | System prompts with `{player_name}`, `{location}`, `{time_of_day}` placeholders filled at runtime | Low | Original Persona library had this. Simple string replacement but very useful for dynamic context |
| **Archetype + traits + backstory config** | Structured personality definition, not just a raw prompt string | Low | ScriptableObject fields that compose into a system instruction. Better DX than writing prompts manually |
| **Speech pattern control** | Configure how the character talks (formal, casual, archaic, etc.) | Low | Part of system instruction generation. "Speak in short sentences" vs "Use flowery language" |

### Advanced Voice

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Dual voice backend (Gemini native + Chirp HD)** | Gemini native is fast but limited voices; Chirp HD has 30+ voices with better quality | Medium | This flexibility is uncommon. Most SDKs lock you to one TTS provider |
| **Per-persona voice backend selection** | One character uses Gemini native (Puck), another uses Chirp HD (custom voice) | Low | Config-level decision, not runtime. Clean separation |

### Developer Experience

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Sample scene with working pipeline** | Dev opens sample, hits play, character talks. Proof it works in under 5 minutes | Medium | Critical for adoption. Most game devs evaluate SDKs in the first 10 minutes |
| **Minimal dependency footprint** | Only requires Firebase AI Logic (which dev may already have) | N/A | Convai/Inworld pull in many dependencies. Lighter is better |

---

## Anti-Features

Features to deliberately NOT build. Either because they are traps (look valuable but cause problems), or because they violate the library's architectural principles.

### UI Components

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Chat window / text input UI** | Every game has different art direction. Shipped UI always looks wrong and gets ripped out | Provide text events that devs wire to their own UI. Include a minimal debug overlay in the sample scene only |
| **Character name plates / speech bubbles** | Same problem as chat UI. Opinionated visuals clash with game aesthetics | Document the pattern: subscribe to text events, create your own world-space canvas |
| **Settings/options menu** | Not the SDK's job. Devs have their own settings systems | Expose all settings programmatically. ScriptableObject for design-time, API for runtime |

### Avatar / Visual Systems

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Built-in character model / avatar** | Locks devs into a specific art style. Immediately creates "this doesn't fit my game" rejection | Provide animation event hooks. Let devs connect to their own Animator, Spine, Live2D, or custom systems |
| **Lip sync rendering** | Requires tight coupling to specific mesh/blend shape setup. Every character rig is different | If lip sync is needed later, expose phoneme/viseme timing data and let devs drive their own blend shapes. Consider providing an optional component, not a core feature |
| **Facial expression system** | Same coupling problem as lip sync. Blend shapes vary wildly between character rigs | Expose emotion data via function calls (emote("happy")). Let devs interpret for their rig |

### Platform / Infrastructure

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Auth system** | Firebase Auth is its own domain. SDK should not manage user identity | Require Firebase Auth to be set up separately. The SDK picks up auth tokens automatically via reflection (already how the Firebase AI SDK works) |
| **Analytics / telemetry** | Privacy concerns, GDPR, trust erosion. Game devs are suspicious of SDKs that phone home | Zero telemetry. If devs want analytics, they add Firebase Analytics themselves |
| **Cloud save / persistence backend** | Scope creep. Conversation memory is a feature (below), but cloud save of that memory is infrastructure | Expose conversation state as serializable data. Let devs persist with their own backend (PlayerPrefs, Firestore, SQLite) |
| **Multiplayer / networking** | Massively increases complexity. Each connected player needs their own Gemini session and the coordination is non-trivial | Design the single-player API cleanly. Multiplayer can be layered on top (one PersonaSession per player-NPC pair) without being built in |

### Conversation / AI Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Custom LLM / model hosting** | You are a Gemini SDK, not a model-agnostic framework. Supporting Ollama, OpenAI, Claude adds massive surface area | Firebase AI Logic only. If devs want other models, they use a different SDK. Be excellent at one thing |
| **RAG / knowledge base ingestion** | Complex infrastructure (vector DB, embeddings, chunking). Out of scope for a conversation SDK | Use Gemini's built-in context window (1M+ tokens) and function calling for game-state queries. If needed later, provide hooks for devs to inject context, not a full RAG pipeline |
| **NPC navigation / pathfinding** | Gameplay logic, not conversation | AI can call functions like move_to("tavern"), but the SDK does not implement navigation. Devs use Unity NavMesh |
| **Quest / objective management** | Game design system, not conversation infrastructure | AI can call functions like start_quest("fetch_sword"), but tracking quest state is the game's job |
| **Emotion detection from voice** | Requires specialized ML models and adds significant latency. Gemini may eventually support this natively | If Gemini adds sentiment/emotion analysis, surface it. Do not build a separate emotion detection pipeline |

---

## Future-Phase Features (NOT v1, but designed for)

Features that are out of scope for v1 but should influence architecture decisions now so they can be added later without rewrites.

| Feature | Why Defer | Architecture Consideration | Complexity (when added) |
|---------|-----------|---------------------------|------------------------|
| **Persistent conversation memory** | Requires storage decisions, summarization strategy, token budget management | Make conversation history exportable/importable. Design PacketAssembler to emit serializable state | Medium |
| **Custom voice cloning (Instant Custom Voice)** | Chirp 3 HD supports it but adds complexity around voice model management and API differences | Voice backend interface should be abstract enough to add a "custom voice" backend alongside preset voices | Medium |
| **Context injection hooks** | Devs want to feed game state to the AI ("player is in the tavern, health is 50%") | System instruction should be composable (base prompt + dynamic context sections). Template variables are step one | Low |
| **Multi-turn function calling chains** | AI calls function A, gets result, calls function B based on result, etc. | Function handler registration must support async handlers that return results. Already planned with C# delegates | Medium |
| **Proactive AI speech** | Character speaks without being spoken to (ambient dialogue, reactions to game events) | Session must support "inject context and request response" without user audio input. `SendAsync` with text already supports this | Low |
| **Voice activity detection (VAD)** | Detect when user starts/stops speaking for cleaner turn management | AudioCapture pipeline should have a VAD interface point. Can start with simple energy threshold, upgrade to WebRTC VAD later | Medium |
| **Streaming text display (typewriter effect)** | Show text as it arrives, character by character | PacketAssembler already chunks text. Expose text chunks with timing for downstream typewriter rendering | Low |

---

## Feature Dependencies

```
PersonaConfig (ScriptableObject)
  |
  v
PersonaSession (MonoBehaviour)
  |
  +-- AudioCapture (Microphone -> PCM)
  |     |
  |     v
  +-- LiveSession (Firebase AI SDK WebSocket)
  |     |
  |     +-- Function Calling (declarations + handlers)
  |     |     |
  |     |     v
  |     |   Emote/Animation (built-in example function)
  |     |
  |     +-- Safety Settings (per-session config)
  |     |
  |     v
  +-- PacketAssembler (synchronizes response streams)
  |     |
  |     +-- Text chunks (with timing)
  |     +-- Audio chunks (PCM data)
  |     +-- Function call events (with timing)
  |     |
  |     v
  +-- AudioPlayback (AudioSource output)
  |
  +-- Events / Callbacks
        +-- OnTextReceived
        +-- OnAudioChunkReady
        +-- OnFunctionCalled
        +-- OnTurnComplete
        +-- OnError
        +-- OnConnectionStateChanged
```

**Critical path (must be built in order):**
1. PersonaConfig ScriptableObject (everything depends on configuration)
2. LiveSession wrapper (connection to Gemini, send/receive)
3. AudioCapture (microphone input pipeline)
4. AudioPlayback (AudioSource output pipeline)
5. PacketAssembler (synchronization -- requires 2, 3, 4 working)
6. Function calling system (requires 2 working)
7. Emote/animation example (requires 6 working)
8. Sample scene (requires all above working)

**Parallel work possible:**
- AudioCapture (3) and AudioPlayback (4) can be built in parallel
- Function calling (6) can be built alongside audio pipeline (3, 4)
- PersonaConfig (1) can be built first and independently

---

## MVP Recommendation

For MVP (v1.0), prioritize the complete conversation loop. A developer should be able to:

1. Create a PersonaConfig in the Inspector
2. Add PersonaSession to a GameObject
3. Hit Play and talk to the character
4. Hear the character respond with synchronized voice
5. See function calls fire (emote example)

**MVP features (in priority order):**

1. PersonaConfig ScriptableObject (personality, voice, model)
2. PersonaSession MonoBehaviour (lifecycle, connection management)
3. LiveSession wrapper (Gemini Live bidirectional streaming)
4. AudioCapture (microphone -> PCM -> Gemini)
5. AudioPlayback (Gemini/Chirp audio -> AudioSource)
6. PacketAssembler (text + audio + event synchronization)
7. Function calling with C# delegate handlers
8. Built-in emote function as example
9. System instruction generation from persona config
10. Error handling and connection state management
11. Sample scene demonstrating full pipeline

**Defer to v1.1 or v2:**
- Persistent memory / conversation history export
- Custom voice cloning
- Template variable system (nice to have for v1, not blocking)
- Voice activity detection (use Gemini's built-in turn detection for v1)
- Chirp 3 HD TTS path (if Gemini native audio is sufficient for launch, Chirp can follow)
- Content moderation layer beyond Firebase's built-in safety settings

---

## Confidence Notes

| Area | Confidence | Reason |
|------|------------|--------|
| Table stakes features | HIGH | Verified against Firebase AI SDK source code in the project. Core audio/conversation/Unity integration features are self-evident requirements |
| Competitor feature comparison | MEDIUM | Based on training data (Convai, Inworld, NVIDIA ACE docs from pre-May 2025). Could not verify current feature sets via WebSearch/WebFetch |
| PacketAssembler as differentiator | HIGH | Verified: no competitor exposes a composable sync primitive at the SDK level. This is genuinely novel based on known products |
| Anti-features list | HIGH | Architectural principles are clear from PROJECT.md ("headless library," UPM package, Firebase-only) |
| Future-phase features | MEDIUM | Architecture considerations are sound but specific API designs will need validation during implementation |
| Feature dependencies | HIGH | Derived directly from Firebase AI SDK source code analysis and standard Unity patterns |

## Sources

- Firebase AI Logic SDK source code: `/home/cachy/workspaces/projects/games/AI-Embodiment/Assets/Firebase/FirebaseAI/` (all .cs files analyzed)
- Project definition: `/home/cachy/workspaces/projects/games/AI-Embodiment/.planning/PROJECT.md`
- Codebase analysis: `/home/cachy/workspaces/projects/games/AI-Embodiment/.planning/codebase/ARCHITECTURE.md`, `INTEGRATIONS.md`, `CONCERNS.md`
- Competitor knowledge (Convai, Inworld AI, Charisma.ai, Replica Studios, NVIDIA ACE): Training data through May 2025 -- MEDIUM confidence, not verified against current docs

---

*Feature landscape analysis: 2026-02-05*
