# Roadmap: AI Embodiment v1.0 Livestream Experience

## Overview

Build a livestream sample scene where Aya hosts an intimate art stream, interacts with simulated chat bot personas and one real user via push-to-talk, and drives a narrative arc toward a cinematic movie clip reveal. The work layers from infrastructure (REST client, UI shell, data model) through the chat bot system, narrative director with user interaction, scene transitions, and finally integration of the full 10-minute experience loop. All new code lives in the sample scene layer -- the package runtime requires zero modifications.

## Milestones

- v1 MVP (Phases 1-6) -- shipped 2026-02-05
- v0.8 WebSocket Migration (Phases 7-11.2) -- shipped 2026-02-17
- v1.0 Livestream Experience (Phases 12-16) -- in progress

## Phases

**Phase Numbering:**
- Integer phases (12, 13, ...): Planned milestone work
- Decimal phases (12.1, 12.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 12: Foundation & Data Model** - REST client, UI shell, ChatBotConfig ScriptableObject, and migrated character data
- [ ] **Phase 13: Chat Bot System** - Scripted and dynamic chat bots with burst timing, personality, and tracked messages
- [ ] **Phase 14: Narrative Director & User Interaction** - Beat/scene structure, dual-queue orchestration, push-to-talk finish-first, and narrative steering
- [ ] **Phase 15: Scene Transition & Animation** - Animation function calls, additive movie clip loading, and pre-load strategy
- [ ] **Phase 16: Integration & Experience Loop** - Full livestream scene wiring, cross-system context injection, and end-to-end experience

## Phase Details

### Phase 12: Foundation & Data Model
**Goal**: Developers have the building blocks every other system depends on -- a REST client for structured Gemini output, a livestream UI shell with chat feed, and ChatBotConfig ScriptableObjects populated from migrated nevatars data
**Depends on**: Nothing (first phase of milestone; builds on existing package runtime)
**Requirements**: INF-01, INF-02, BOT-01, MIG-01
**Success Criteria** (what must be TRUE):
  1. GeminiTextClient can send a prompt to gemini-2.5-flash and receive a structured JSON response matching a provided schema (verified with a test call returning an array of bot message objects)
  2. Livestream UI shell renders in Play Mode with a scrollable ListView chat feed, an Aya transcript panel, a push-to-talk indicator area, and stream status indicators (LIVE badge, viewer count, duration timer)
  3. ChatBotConfig ScriptableObjects exist for all migrated nevatars characters (names, colors, personalities preserved) and are editable in the Inspector
  4. Chat messages can be programmatically added to the ListView feed and display with correct bot name, color, and timestamp
**Plans**: TBD

Plans:
- [ ] 12-01: GeminiTextClient REST wrapper with structured output
- [ ] 12-02: LivestreamUI shell (UXML/USS layout, ListView chat feed, transcript panel, stream status)
- [ ] 12-03: ChatBotConfig ScriptableObject and nevatars character migration

### Phase 13: Chat Bot System
**Goal**: Chat bots post messages in the livestream chat with organic timing, per-bot personality, and optional dynamic responses to user input -- creating the illusion of a small live audience
**Depends on**: Phase 12 (ChatBotConfig, GeminiTextClient, LivestreamUI)
**Requirements**: BOT-02, BOT-03, BOT-04, BOT-05, BOT-06, MIG-03
**Success Criteria** (what must be TRUE):
  1. Multiple bot personas post scripted messages in the chat feed with randomized bot count per burst (1-4), shuffled message order, message alternatives, and configurable delays (0.8-3.0s) -- producing a burst-and-lull pattern that feels organic, not metronomic
  2. Each bot has visually distinct personality expressed through typing cadence, message length, capitalization style, and emoji usage (observable difference between e.g. a dad bot and a teenage fan bot)
  3. When the user speaks via push-to-talk, bots generate dynamic responses via Gemini structured output that react to what the user said (not just scripted lines)
  4. TrackedChatMessage system tracks which messages Aya has and has not responded to, preventing Aya from acknowledging the same bot message twice
  5. Migrated message pools from nevatars response patterns provide the scripted dialogue content for each bot
**Plans**: TBD

Plans:
- [ ] 13-01: ChatBotManager with scripted message scheduling and burst timing
- [ ] 13-02: Dynamic Gemini REST responses and TrackedChatMessage system
- [ ] 13-03: Nevatars response pattern migration and per-bot personality tuning

### Phase 14: Narrative Director & User Interaction
**Goal**: Aya drives a time-based narrative arc through beat/scene structure, responds to user push-to-talk with finish-first priority (completing her current thought before addressing the user), and the dual-queue system orchestrates chat scenes and Aya scenes in parallel
**Depends on**: Phase 12 (LivestreamUI), Phase 13 (ChatBotManager for chat burst scenes)
**Requirements**: LIVE-02, LIVE-03, LIVE-04, NAR-01, NAR-02, NAR-03, NAR-06, USR-01, USR-02, USR-03, MIG-02
**Success Criteria** (what must be TRUE):
  1. Aya progresses through a multi-beat narrative arc (warm-up, art process, characters, emotional build, reveal) driven by time and goal escalation -- observable as distinct topic shifts over a 10-minute session
  2. The dual-queue system runs chat scenes (ChatBurst, UserChoice) in parallel with Aya scenes (AyaDialogue, AyChecksChat, AyaAction) -- chat activity does not block Aya and Aya does not block chat
  3. When the user presses push-to-talk while Aya is speaking, a visual acknowledgment appears within 500ms ("Aya noticed you"), and Aya completes her current response before addressing the user's input
  4. User can review their transcribed speech and approve/edit before it is sent to Aya (QueuedResponse pattern)
  5. Scene transitions fire on conditional triggers (TimedOut, QuestionsAnswered) advancing the narrative from one beat to the next without manual intervention
**Plans**: TBD

Plans:
- [ ] 14-01: NarrativeBeat/Scene data model and nevatars beat migration
- [ ] 14-02: NarrativeDirector with time-based goal lifecycle and SendText steering
- [ ] 14-03: Dual-queue system and scene type execution (AyaDialogue, ChatBurst, AyaAction, AyaChecksChat, UserChoice)
- [ ] 14-04: Push-to-talk finish-first controller with visual acknowledgment and transcript approval

### Phase 15: Scene Transition & Animation
**Goal**: Aya can trigger pre-authored animations via function calls, and the narrative climax triggers an additive scene load for the Unity-rendered movie clip -- all without destroying the livestream WebSocket or chat state
**Depends on**: Phase 14 (NarrativeDirector reaching reveal goal triggers scene load)
**Requirements**: ANI-01, ANI-02, ANI-03
**Success Criteria** (what must be TRUE):
  1. Aya triggers animation function calls (wave, spin, draw gestures) naturally during conversation, and the registered handlers fire (verified via console logs or placeholder animations)
  2. When the narrative reaches the reveal goal, additive scene loading activates the pre-loaded movie clip scene while the livestream scene remains loaded (PersonaSession WebSocket stays connected, chat history preserved)
  3. Movie clip scene is pre-loaded with allowSceneActivation = false early in the session, so activation at reveal time is near-instant (no loading stall)
**Plans**: TBD

Plans:
- [ ] 15-01: Animation function call registration and handler wiring
- [ ] 15-02: SceneTransition handler with additive loading, pre-load strategy, and camera/AudioListener management

### Phase 16: Integration & Experience Loop
**Goal**: The complete livestream sample scene runs as a cohesive 10-minute experience -- Aya hosts, bots chat, user talks, narrative builds, and the movie clip reveals -- with cross-system context injection ensuring coherence
**Depends on**: Phases 12-15 (all components)
**Requirements**: LIVE-01, NAR-04, NAR-05
**Success Criteria** (what must be TRUE):
  1. A developer can open the LivestreamSample scene, enter Play Mode, and experience the full livestream loop from Aya's greeting through narrative progression to movie clip reveal without any manual intervention beyond push-to-talk
  2. Chat bots act as narrative catalysts -- bot questions visibly steer Aya toward the next narrative beat (e.g., "omg are you gonna show us the thing?" nudges Aya toward the reveal)
  3. User push-to-talk questions about upcoming content accelerate the narrative arc (asking about the movie clip moves Aya closer to the reveal faster than waiting)
  4. Cross-system coherence is maintained -- Aya acknowledges bot messages she has seen, bots react to what Aya actually said, and no bot asserts facts Aya has not mentioned
**Plans**: TBD

Plans:
- [ ] 16-01: LivestreamController orchestrator wiring all subsystems
- [ ] 16-02: Cross-system context injection (bot messages to Aya, director notes, shared state) and narrative catalyst tuning
- [ ] 16-03: End-to-end experience validation and polish

## Progress

**Execution Order:**
Phases execute in numeric order: 12 -> 13 -> 14 -> 15 -> 16

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 12. Foundation & Data Model | 0/3 | Not started | - |
| 13. Chat Bot System | 0/3 | Not started | - |
| 14. Narrative Director & User Interaction | 0/4 | Not started | - |
| 15. Scene Transition & Animation | 0/2 | Not started | - |
| 16. Integration & Experience Loop | 0/3 | Not started | - |
