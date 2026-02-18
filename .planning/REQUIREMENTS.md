# Requirements: AI Embodiment

**Defined:** 2026-02-17
**Core Value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.

## v1.0 Requirements

Requirements for the Livestream Experience milestone. Builds on existing v0.8 package infrastructure.

### Livestream Scene

- [x] **LIVE-01**: Livestream sample scene with Aya as AI host, chat feed, and user push-to-talk
- [x] **LIVE-02**: Beat/scene narrative structure (ordered scenes per beat with conditional transitions)
- [x] **LIVE-03**: Dual-queue system -- chat scenes and Aya scenes run in parallel (chat non-blocking, Aya sequential)
- [x] **LIVE-04**: Scene types: AyaDialogue, ChatBurst, AyaChecksChat, AyaAction, UserChoice

### Chat Bot System

- [x] **BOT-01**: ChatBotConfig ScriptableObject (name, personality, color, scripted message pool, typing speed)
- [x] **BOT-02**: Configurable number of bot personas per stream
- [x] **BOT-03**: Chat burst system with randomized bot count, shuffled order, message alternatives, configurable delays (0.8-3.0s)
- [x] **BOT-04**: Dynamic Gemini responses to user input via REST structured output (gemini-2.5-flash)
- [x] **BOT-05**: Per-bot personality in typing cadence and speech style
- [x] **BOT-06**: TrackedChatMessage system (tracks which bot messages Aya has responded to)

### Narrative & Goals

- [x] **NAR-01**: Time-based narrative arc (warm-up -> art process -> characters -> emotional build -> reveal)
- [x] **NAR-02**: SendText director notes for mid-session Aya steering
- [x] **NAR-03**: Goal chain with escalating urgency (multiple mini-goals, LOW -> HIGH)
- [x] **NAR-04**: Chat bots as narrative catalysts (bot questions help steer Aya toward the goal)
- [x] **NAR-05**: User questions can accelerate the narrative arc
- [x] **NAR-06**: Conditional scene transitions (TimedOut, PatternMatched, QuestionsAnswered)

### User Interaction

- [x] **USR-01**: Push-to-talk with finish-first priority (Aya completes current response before addressing user)
- [x] **USR-02**: Visual acknowledgment within 500ms when user wants to speak while Aya is busy
- [x] **USR-03**: Transcript review and approval before sending (QueuedResponse pattern)

### Animation & Scene

- [x] **ANI-01**: Animation function calls (wave, spin, draw gestures) registered via existing function calling system
- [x] **ANI-02**: Goal-triggered additive scene loading for Unity-rendered movie clip
- [x] **ANI-03**: Pre-load movie scene with allowSceneActivation = false

### Audio2Animation Pipeline

- [x] **A2A-01**: Audio2Animation class accepts audio chunks and emits BlendshapeAnimationData frame packets via event/callback
- [x] **A2A-02**: Fake model streams frames from pre-recorded JSON synchronized to audio timing (not dumped all at once)
- [x] **A2A-03**: Sample app subscribes to Audio2Animation output and applies blendshape frames to SkinnedMeshRenderer face rig

### Infrastructure

- [x] **INF-01**: GeminiTextClient -- REST generateContent wrapper for structured output (chat bot responses)
- [x] **INF-02**: Livestream UI -- chat feed (ListView for performance), push-to-talk indicator, Aya transcript, stream status

### Content Migration

- [x] **MIG-01**: Migrate StreamingCharacter ScriptableObjects from nevatars to ChatBotConfig format (preserve names, colors, personalities)
- [x] **MIG-02**: Migrate NarrativeBeat/Scene assets from nevatars (preserve beat structure, scene ordering, scripted messages, cue timing)
- [x] **MIG-03**: Migrate response patterns and message alternatives from nevatars (preserve bot dialogue pools)

## v2.0 Requirements (Deferred)

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
| Real streaming platform integration (Twitch/YouTube) | Simulated livestream experience, not real broadcast |
| Multiplayer networking | Single user interacting with AI personas |
| Actual animation authoring | Function calls fire -- another developer hooks in animations |
| Runtime voice switching mid-session | Voice is set per-persona at connect time |
| Mobile-specific optimizations | Desktop-first |
| ElevenLabs TTS implementation | ITTSProvider interface enables it, only Chirp ships |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| LIVE-01 | Phase 16 | Complete |
| LIVE-02 | Phase 14 | Complete |
| LIVE-03 | Phase 14 | Complete |
| LIVE-04 | Phase 14 | Complete |
| BOT-01 | Phase 12 | Complete |
| BOT-02 | Phase 13 | Complete |
| BOT-03 | Phase 13 | Complete |
| BOT-04 | Phase 13 | Complete |
| BOT-05 | Phase 13 | Complete |
| BOT-06 | Phase 13 | Complete |
| NAR-01 | Phase 14 | Complete |
| NAR-02 | Phase 14 | Complete |
| NAR-03 | Phase 14 | Complete |
| NAR-04 | Phase 16 | Complete |
| NAR-05 | Phase 16 | Complete |
| NAR-06 | Phase 14 | Complete |
| USR-01 | Phase 14 | Complete |
| USR-02 | Phase 14 | Complete |
| USR-03 | Phase 14 | Complete |
| ANI-01 | Phase 15 | Complete |
| ANI-02 | Phase 15 | Complete |
| ANI-03 | Phase 15 | Complete |
| A2A-01 | Phase 15.1 | Complete |
| A2A-02 | Phase 15.1 | Complete |
| A2A-03 | Phase 15.1 | Complete |
| INF-01 | Phase 12 | Complete |
| INF-02 | Phase 12 | Complete |
| MIG-01 | Phase 12 | Complete |
| MIG-02 | Phase 14 | Complete |
| MIG-03 | Phase 13 | Complete |

**Coverage:**
- v1.0 requirements: 30 total
- Mapped to phases: 30
- Unmapped: 0

---
*Requirements defined: 2026-02-17*
*Last updated: 2026-02-17 after roadmap creation*
