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

- [x] **Phase 12: Foundation & Data Model** - REST client, UI shell, ChatBotConfig ScriptableObject, and migrated character data
- [x] **Phase 13: Chat Bot System** - Scripted and dynamic chat bots with burst timing, personality, and tracked messages
- [x] **Phase 14: Narrative Director & User Interaction** - Beat/scene structure, dual-queue orchestration, push-to-talk finish-first, and narrative steering
- [x] **Phase 15: Scene Transition & Animation** - Animation function calls, clean scene transition to movie clip, toast UI feedback
- [ ] **Phase 15.1: Audio2Animation Pipeline** - Audio-to-animation pipeline in package runtime with fake model streaming pre-recorded blendshape data (INSERTED)
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
**Plans:** 3 plans

Plans:
- [x] 12-01-PLAN.md -- GeminiTextClient REST wrapper with structured output
- [x] 12-02-PLAN.md -- ChatBotConfig ScriptableObject, ChatMessage data model, and character migration
- [x] 12-03-PLAN.md -- LivestreamUI shell (UXML/USS layout, ListView chat feed, transcript panel, stream status)

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
**Plans:** 3 plans

Plans:
- [x] 13-01-PLAN.md -- ChatBotManager with scripted burst loop, TrackedChatMessage, and per-bot personality transforms
- [x] 13-02-PLAN.md -- Dynamic Gemini REST responses triggered by user push-to-talk speech
- [x] 13-03-PLAN.md -- Nevatars response pattern migration (chatBurstMessages to messageAlternatives)

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
**Plans:** 4 plans

Plans:
- [x] 14-01-PLAN.md -- NarrativeBeatConfig ScriptableObject data model and 3 beat assets (warm-up, art process, characters)
- [x] 14-02-PLAN.md -- NarrativeDirector with time-based beat lifecycle, SendText steering, and ChatBotManager pacing integration
- [x] 14-03-PLAN.md -- Scene-type execution (AyaDialogue, AyaChecksChat, AyaAction) with conditional transitions
- [x] 14-04-PLAN.md -- PushToTalkController with finish-first state machine, transcript overlay, and visual acknowledgment

### Phase 15: Scene Transition & Animation
**Goal**: Aya can trigger pre-authored animations via function calls during conversation, and the narrative climax triggers a clean scene exit to the movie clip -- with visible toast feedback for animation triggers and explicit WebSocket disconnect before scene unload
**Depends on**: Phase 14 (NarrativeDirector.OnAllBeatsComplete triggers scene transition)
**Requirements**: ANI-01, ANI-02, ANI-03
**Success Criteria** (what must be TRUE):
  1. Aya triggers animation function calls (wave, point, laugh, think, nod) naturally during conversation, and the registered handlers fire (verified via Debug.Log in console AND toast message in livestream UI)
  2. AnimationConfig ScriptableObject defines available animations in the Inspector -- developers add/remove animations without code changes, and the play_animation function uses an enum parameter constraining Gemini to valid animation names
  3. When the narrative reaches all-beats-complete, PersonaSession disconnects cleanly and the movie clip scene loads via SceneManager.LoadSceneAsync with LoadSceneMode.Single (livestream scene fully unloads, instant cut transition)
**Plans:** 2 plans

Plans:
- [x] 15-01-PLAN.md -- AnimationConfig ScriptableObject, data-driven play_animation function registration, toast UI feedback
- [x] 15-02-PLAN.md -- SceneTransitionHandler with clean scene exit, explicit Disconnect, and Build Settings validation

### Phase 15.1: Audio2Animation Pipeline (INSERTED)
**Goal**: Audio chunks from TTS feed into a new Audio2Animation class in the package runtime that produces streaming BlendshapeAnimationData packets -- for now faked by streaming pre-recorded JSON animation data (animDemo1.json format) instead of real model inference. The sample application subscribes to these animation packets and applies them to a face rig via the existing BlendshapeAnimationConverter infrastructure.
**Depends on**: Phase 15 (existing BlendshapeAnimationData, BlendshapeAnimationConverter in package runtime)
**Requirements**: A2A-01, A2A-02, A2A-03
**Success Criteria** (what must be TRUE):
  1. Audio2Animation class in the package runtime (com.google.ai-embodiment) accepts audio chunks and emits BlendshapeAnimationData frame packets via an event/callback
  2. Fake model implementation streams frames from pre-recorded JSON files (SampleData/animDemo*.json) synchronized to audio timing, not dumped all at once
  3. Sample application subscribes to Audio2Animation output and applies blendshape frames to a SkinnedMeshRenderer face rig in real-time
  4. The pipeline integrates as a SyncDriver concept between TTS output and face animation -- pluggable so a real model can replace the fake later
**Plans:** 2 plans

Plans:
- [ ] 15.1-01-PLAN.md -- IAnimationModel interface, Audio2Animation orchestrator, FakeAnimationModel with time-accumulator frame streaming
- [ ] 15.1-02-PLAN.md -- FaceAnimationPlayer MonoBehaviour wiring pipeline to SkinnedMeshRenderer face rig

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
Phases execute in numeric order: 12 -> 13 -> 14 -> 15 -> 15.1 -> 16

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 12. Foundation & Data Model | 3/3 | Complete | 2026-02-17 |
| 13. Chat Bot System | 3/3 | Complete | 2026-02-17 |
| 14. Narrative Director & User Interaction | 4/4 | Complete | 2026-02-17 |
| 15. Scene Transition & Animation | 2/2 | Complete | 2026-02-17 |
| 15.1. Audio2Animation Pipeline | 0/2 | Planned | - |
| 16. Integration & Experience Loop | 0/3 | Not started | - |
