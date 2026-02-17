# Project Research Summary

**Project:** AI Embodiment v1.0 Livestream Experience
**Domain:** Simulated AI Livestream / Interactive Narrative Experience (Unity)
**Researched:** 2026-02-17
**Confidence:** HIGH

## Executive Summary

The v1.0 Livestream Experience is a self-contained Unity sample scene where an AI character (Aya) hosts an intimate art stream, interacts with simulated chat bot personas and one real user via push-to-talk, and builds toward a cinematic movie clip reveal. The existing AI Embodiment package provides a strong foundation -- PersonaSession, ConversationalGoals, function calling, audio playback, and push-to-talk patterns are already built. The new work is a sample scene layer (~800-1,200 lines of C#) that orchestrates these existing systems alongside two genuinely new components: a chat bot system and a narrative director.

The central architectural decision is the **dual Gemini API path**: Aya uses the existing Gemini Live WebSocket for real-time audio conversation, while chat bots use a new lightweight REST client (`generateContent` with structured JSON output) for text-only responses. This split is non-negotiable -- the Live API does not support `response_schema`, and chat bots need guaranteed JSON structure. The REST client targets `gemini-2.5-flash` for cost and speed. All new code lives in the sample scene layer; the package runtime requires zero modifications.

The primary risks are experiential, not technical: (1) the "finish-first" push-to-talk pattern can make users feel ignored if there is no visual acknowledgment within 500ms, (2) chat bot timing that feels robotic rather than organic will break the livestream illusion, and (3) narrative goal steering can feel pushy if priorities escalate too aggressively. All three are solvable through careful UX design and prompt engineering, but they must be addressed in the initial implementation -- not retrofitted as polish. Scene loading for the movie clip must use additive mode to preserve the WebSocket connection and chat state.

## Key Findings

### Recommended Stack

The stack is almost entirely in place. Unity 6, C# 9, Newtonsoft.Json, Gemini Live WebSocket, and the Input System are already in the codebase. The only new technology is the Gemini REST `generateContent` endpoint for chat bot structured output, called via `UnityWebRequest` (the same async pattern already proven in `ChirpTTSClient`).

**Core technologies (already in use -- no changes):**
- **Unity 6 (6000.3.7f1):** Engine, scene management, UI Toolkit, audio pipeline
- **Gemini Live WebSocket (v1beta):** Aya's real-time audio conversation via PersonaSession
- **Newtonsoft.Json:** All JSON serialization for API communication

**New for v1.0:**
- **Gemini REST generateContent (gemini-2.5-flash):** Structured JSON output for chat bot responses. Single call returns array of bot messages with schema enforcement
- **UI Toolkit ListView:** Virtualized chat feed replacing current ScrollView. Element recycling via makeItem/bindItem handles 100+ messages without degradation
- **SceneManager.LoadSceneAsync (Additive):** Movie clip scene loaded without unloading livestream. Pre-loadable with `allowSceneActivation = false`
- **Timeline + PlayableDirector:** Unity-rendered cinematic for the movie clip (not pre-recorded video)

**Critical version note:** Do NOT use gemini-2.0-flash for REST calls -- it is being retired March 31, 2026. Use gemini-2.5-flash.

See [STACK.md](./STACK.md) for full API reference, structured output schema examples, and implementation patterns.

### Expected Features

**Must have (table stakes):**
- Scrolling chat feed with 2-3 bot personas (dad, friend, casual viewer) posting at irregular intervals
- Aya speaks proactively as stream host, driving conversation without waiting for user prompts
- Push-to-talk user interaction with finish-first priority (Aya completes her thought before responding)
- Time-based narrative progression through 5 phases: warm-up, art process, character stories, emotional build, movie clip reveal
- Goal-triggered movie clip reveal via `start_movie()` function call
- Stream status UI: LIVE badge, viewer count, duration timer, transcript panel
- Animation function calls: wave, spin, draw gestures triggered naturally by Aya
- All configuration via ScriptableObjects (PersonaConfig, ChatBotConfig, NarrativeConfig)

**Should have (differentiators):**
- Aya acknowledges user by name, creating parasocial intimacy
- Chat bots serve as narrative catalysts (asking questions that advance the story arc)
- Multi-phase goal escalation with user input acceleration
- Natural topic transitions guided by system instruction and director notes
- Small viewer count aesthetic (5-viewer stream, not broadcast scale)

**Defer (v2+):**
- Dynamic bot responses via Gemini REST (v1.x -- after core loop validated)
- Cross-session memory
- Art canvas visualization
- Multiple narrative arcs
- Audio ambiance (lo-fi background music, notification sounds)

**Anti-features (deliberately NOT building):**
- Real Twitch/YouTube integration -- this is a simulated experience, not a streaming product
- User typing in chat -- push-to-talk only; voice-first interaction
- Bot voice output (TTS) -- bots are text-only; Aya is the only voice
- Multiplayer -- single user experience with simulated audience
- Skip/fast-forward narrative -- the journey IS the experience

See [FEATURES.md](./FEATURES.md) for complete feature tables, anti-features rationale, chat bot behavior design, and narrative pacing specification.

### Architecture Approach

All new components live in the sample scene layer (`Samples~/LivestreamSample/`), consuming the package's existing public API surface. The package runtime (PersonaSession, GoalManager, FunctionRegistry, AudioPlayback, GeminiLiveClient) requires zero modifications. A `LivestreamController` MonoBehaviour orchestrates all new systems.

**Major components:**
1. **LivestreamController** -- Top-level orchestrator wiring PersonaSession, ChatBotManager, NarrativeDirector, LivestreamUI, and SceneTransition
2. **ChatBotManager** -- Manages bot personas, scripted message timers, and optional Gemini REST calls for dynamic responses. Injects significant bot messages into Aya's context via `SendText("[CHAT] botName: message")`
3. **NarrativeDirector** -- Time-based goal lifecycle manager. Pre-loads goals before `Connect()`, steers mid-session via `SendText("[DIRECTOR NOTE: ...]")` since the Live API cannot update system instructions mid-session
4. **GeminiTextClient** -- Lightweight REST utility (~100 lines) wrapping UnityWebRequest for `generateContent` with structured output
5. **LivestreamUI** -- UI Toolkit controller: ListView chat feed, Aya transcript panel, push-to-talk controls, stream status indicators
6. **SceneTransition** -- Function call handler for `start_movie()`. Additive scene loading with camera/AudioListener management

**Key architectural constraint:** Goals cannot be updated mid-session via the Live API. The NarrativeDirector works around this by (a) pre-loading the full narrative arc in the system instruction at connect time, and (b) injecting `[DIRECTOR NOTE: ...]` text messages for mid-session steering. This is verified in the codebase -- `SendGoalUpdate()` logs a warning about this limitation.

See [ARCHITECTURE.md](./ARCHITECTURE.md) for component boundaries, data flow diagrams, build order, and anti-patterns.

### Critical Pitfalls

1. **"Finish-first" makes users feel ignored** -- When Aya is mid-monologue and the user speaks, there is a dead zone with no feedback. Users will think the mic is broken. Prevention: show visual acknowledgment within 500ms ("Aya noticed your message"), cap wait time at 8-10 seconds, add a filler animation (glance at camera). This must be built into the initial push-to-talk implementation, not added later.

2. **Chat bots feel like a script, not a community** -- Metronomic timing, uniform message length, and no reactions to Aya's speech destroy the livestream illusion. Prevention: event-driven timing (react to Aya's transcript keywords), per-bot personality quirks in ScriptableObject config (typing speed, emoji usage, message length), burst-and-lull pattern with deliberate dead chat moments, and message imperfection (lowercase, abbreviations, occasional typos).

3. **Goal steering feels like a pushy salesperson** -- HIGH priority goals make Aya shoehorn the movie clip into every response. Prevention: start ALL goals at LOW, escalate slowly over minutes, use chat bots as narrative catalysts ("omg when are you gonna show us the thing??" is better than telling Aya to steer), rewrite urgency framing to give Aya permission to NOT steer, and use mini-goal chains instead of one big goal.

4. **Scene loading destroys livestream state** -- Using `LoadSceneMode.Single` for the movie clip kills the WebSocket, chat history, and bot state. Prevention: always use additive loading, pre-load with `allowSceneActivation = false`, disable livestream camera/AudioListener during clip, keep PersonaSession alive but muted during playback. This is a hard architectural requirement -- getting it wrong requires a rewrite.

5. **Multi-persona coherence collapse** -- Aya and chat bots are independent systems with no shared context. Bots may reference things Aya has not said. Prevention: inject significant bot messages into Aya's session via `SendText()`, give Aya a "chat awareness" system instruction section, limit what scripted bots can assert (reactions only, not new facts), and maintain a shared state object for current topic/activity.

See [PITFALLS.md](./PITFALLS.md) for all 7 critical pitfalls with research citations, warning signs, recovery strategies, and the "Looks Done But Isn't" verification checklist.

## Implications for Roadmap

Based on combined research, the project decomposes into 5 phases ordered by dependency. Each phase is independently testable.

### Phase 1: Foundation Components
**Rationale:** These components have zero dependencies on each other and can be built and tested in isolation. They are the building blocks everything else needs.
**Delivers:** ChatBotConfig ScriptableObject, GeminiTextClient REST utility, LivestreamUI shell (UXML/USS layout + ListView chat feed + transcript panel + stream status indicators)
**Addresses:** Stream atmosphere UI (LIVE badge, chat feed, transcript), configurable ScriptableObjects
**Avoids:** Pitfall #4 (structured output overhead) -- building the REST client correctly from the start with batched array responses

### Phase 2: Chat Bot System
**Rationale:** Depends on Phase 1 (ChatBotConfig + GeminiTextClient + LivestreamUI). The chat bot system is the most novel component and needs the most iteration time.
**Delivers:** ChatBotManager with scripted message scheduling, event-driven timing, per-bot personality differentiation, and optional dynamic responses
**Addresses:** Multiple bot personas, bot messages over time, bot personality differentiation, burst-and-lull timing
**Avoids:** Pitfall #2 (scripted-feeling bots) -- event-driven timing and personality quirks built in from the start. Pitfall #7 (uncanny timing) -- per-message typing delay, staggered reactions, dead chat moments

### Phase 3: Narrative Director + Push-to-Talk Integration
**Rationale:** Depends on PersonaSession's existing API (AddGoal, SendText, OnTurnComplete). The narrative director and push-to-talk finish-first logic are both about controlling Aya's conversation flow and should be designed together.
**Delivers:** NarrativeDirector (time-based goal lifecycle, SendText steering), finish-first push-to-talk controller with visual acknowledgment, Aya's livestream PersonaConfig
**Addresses:** Time-based narrative progression, push-to-talk with finish-first, Aya as proactive host, goal injection strategy
**Avoids:** Pitfall #1 (user feels ignored) -- visual acknowledgment and wait-time cap built in from the start. Pitfall #3 (pushy goal steering) -- mini-goal chains and slow escalation designed from the start. Pitfall #6 (coherence collapse) -- SendText injection for bot messages wired during integration

### Phase 4: Scene Transition + Movie Clip
**Rationale:** Depends on function calling (already built), NarrativeDirector (Phase 3), and a correct understanding of additive scene loading requirements. This is the narrative climax -- it must be bulletproof.
**Delivers:** SceneTransition handler, MovieClipScene with Timeline/PlayableDirector, pre-loading strategy, camera/AudioListener management, state preservation during clip playback
**Addresses:** Goal-triggered movie clip reveal, scene transition as technical showcase
**Avoids:** Pitfall #5 (scene load destroys state) -- additive loading, pre-loading, and state preservation designed as the primary approach from day one

### Phase 5: Integration + Polish
**Rationale:** The LivestreamController orchestrator depends on all other components. This phase wires everything together and validates the full experience loop.
**Delivers:** LivestreamController orchestrator, complete 10-minute experience loop, cross-component context injection (bot messages to Aya, director notes, shared state), animation function calls (emote registration)
**Addresses:** All remaining table stakes, end-to-end experience validation
**Avoids:** Pitfall #6 (coherence collapse) -- full context injection verified. All pitfalls validated via the "Looks Done But Isn't" checklist from PITFALLS.md

### Phase Ordering Rationale

- **Foundation first** because every other component depends on the REST client, UI shell, and data model
- **Chat bots before narrative** because the chat bot system is the most novel and risky component -- it needs the most iteration time, and it can be tested independently against a simple Aya session
- **Narrative and push-to-talk together** because they both control Aya's conversational flow -- the finish-first logic and director note injection interact at the PersonaSession boundary
- **Scene transition late** because it depends on the narrative director reaching the reveal goal, and the technical pattern (additive loading) is well-documented -- it just needs to be done correctly
- **Integration last** because the orchestrator is a thin wiring layer that depends on all other components existing

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Chat Bot System):** Event-driven timing model needs prototyping. The burst-and-lull pattern, per-bot typing speed, and cross-bot referencing are experiential and hard to specify without iteration. Recommend building a minimal chat-only prototype first.
- **Phase 3 (Narrative Director):** The `SendText("[DIRECTOR NOTE: ...]")` approach for mid-session steering needs validation with the actual Gemini model. How reliably does the model follow director notes embedded in conversation context? This needs testing before committing to the full narrative arc design.
- **Phase 3 (Finish-First Push-to-Talk):** The "wait then speak" approach (delay mic capture until Aya finishes) is simpler than audio buffering but changes the UX. Needs playtesting to determine if users accept waiting to speak or if true audio buffering is required.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Foundation):** REST client is a standard UnityWebRequest pattern (already proven in ChirpTTSClient). ListView is documented Unity 6 API. ScriptableObjects are standard Unity.
- **Phase 4 (Scene Transition):** Additive scene loading is a well-documented Unity pattern. Timeline/PlayableDirector is standard Unity cinematic tooling. The pitfalls are known and avoidable with the right approach.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All technologies verified against official docs. Gemini REST structured output confirmed. Existing codebase inspected directly. No speculative technology choices. |
| Features | MEDIUM-HIGH | Table stakes derived from VTuber/livestream research and existing codebase. Chat bot behavior design is well-reasoned but untested. Narrative pacing draws from drama management research but Gemini-specific behavior needs validation. |
| Architecture | HIGH | Component boundaries clean. Package API surface sufficient (zero modifications). Dual Gemini path is well-justified. Anti-patterns clearly identified. Build order has clear dependency rationale. |
| Pitfalls | HIGH | Pitfalls grounded in HCI research (300ms rule, 4-second degradation threshold), existing codebase constraints (mid-session goal limitation), and Unity platform knowledge (additive scene loading). Recovery strategies identified for each. |

**Overall confidence:** HIGH

### Gaps to Address

- **SendText director note reliability:** How consistently does Gemini follow `[DIRECTOR NOTE: ...]` tags in conversation context? This is prompt engineering, not API limitation, but it has not been tested. Validate early in Phase 3 with a simple test: inject a director note and verify Aya changes topic within 1-2 responses.
- **Finish-first UX acceptance:** Will users accept the "wait then speak" model (mic capture delayed until Aya finishes)? Or will they expect to speak immediately and have their input buffered? Playtesting in Phase 3 will determine if the simpler approach works or if `SendRawAudio()` needs to be added to PersonaSession.
- **ListView dynamic height in Unity 6:** Chat messages have variable length. ListView's dynamic height support may not work perfectly. Fallback: cap message length at 140 characters or use ScrollView with a 50-message cap (sufficient for a small stream).
- **Context window growth:** Over a 10-15 minute session with bot messages injected via SendText, Aya's context window will grow. No research confirmed the token limit impact. Monitor during integration testing and add selective injection (only significant messages) if context grows too large.
- **Gemini 2.5 Flash structured output array reliability:** The schema requests an ARRAY of bot message objects. While structured output is documented, array-of-objects with multiple required fields has not been tested against this specific model version. Validate in Phase 1 when building GeminiTextClient.

## Sources

### Primary (HIGH confidence)
- [Gemini API Structured Output docs](https://ai.google.dev/gemini-api/docs/structured-output) -- response_schema format, supported types, limitations
- [Gemini Live API reference](https://ai.google.dev/api/live) -- confirmed response_schema NOT supported in Live API, configuration immutable mid-session
- [Gemini API GenerationConfig reference](https://ai.google.dev/api/generate-content) -- field names: response_schema, response_mime_type
- [Unity 6 SceneManager.LoadSceneAsync](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) -- additive loading API
- [Unity 6 ListView manual](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-uxml-element-ListView.html) -- virtualization, makeItem/bindItem
- [AssemblyAI -- 300ms rule for voice AI](https://www.assemblyai.com/blog/low-latency-voice-ai) -- latency thresholds for perceived responsiveness
- [GetStream -- Livestream Chat UX](https://getstream.io/blog/7-ux-best-practices-for-livestream-chat/) -- scroll behavior, message pacing, virtualized lists
- Existing codebase: PersonaSession.cs, GeminiLiveClient.cs, GoalManager.cs, QueuedResponseController.cs, ChirpTTSClient.cs, AyaSampleController.cs -- direct source code inspection

### Secondary (MEDIUM confidence)
- [AI VTuber Fandom Research (arxiv)](https://arxiv.org/html/2509.10427v1) -- parasocial dynamics, consistency-as-authenticity
- [Facade Interactive Drama Design](https://www.gamedeveloper.com/design/the-story-of-facade-the-ai-powered-interactive-drama) -- beat system, drama manager patterns
- [Convai Narrative Design](https://convai.com/blog/convai-narrative-design) -- goal-driven steering challenges, narrative stagnation risk
- [arXiv -- Mitigating Response Delays](https://arxiv.org/html/2507.22352v1) -- filler strategies, 4-second degradation threshold
- [Google DialogLab Research](https://research.google/blog/beyond-one-on-one-authoring-simulating-and-testing-dynamic-human-ai-group-conversations/) -- turn-taking, multi-party coherence
- [Unity Discussions -- Additive scene loading](https://discussions.unity.com/t/avoiding-multiple-event-systems-audio-listeners-etc-with-additive-scene-loading/866174) -- duplicate AudioListener workarounds
- [Gemini Session Management](https://ai.google.dev/gemini-api/docs/live-session) -- session resumption with 2-hour token validity

### Tertiary (LOW confidence)
- Disney Research "Let Me Finish First" study (2024) -- referenced via secondary sources, not directly verified
- [Gemini API Pricing 2026](https://www.aifreeapi.com/en/posts/gemini-api-pricing-2026) -- pricing data, may be outdated

---
*Research completed: 2026-02-17*
*Ready for roadmap: yes*
