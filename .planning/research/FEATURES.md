# Feature Research: v1.0 Livestream Experience

**Domain:** Simulated AI Livestream / Interactive Narrative Experience
**Researched:** 2026-02-17
**Confidence:** MEDIUM-HIGH (domain patterns well-understood; Gemini Live API constraints verified against official docs)

## Context

This research targets the v1.0 Livestream Experience milestone -- a sample scene where Aya hosts an intimate art stream, tells stories about her characters, interacts with chat bot personas and a real user, and drives toward a movie clip reveal. This is NOT a real streaming platform integration. It is a self-contained Unity scene that simulates the feel of a small, intimate livestream.

The existing codebase provides: PersonaSession, ConversationalGoals with priority steering, QueuedResponse push-to-talk pattern, function calling, SyncPackets, and dual TTS backends. The research below focuses on what EXPERIENCE features are needed on top of this infrastructure.

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that must exist for the experience to feel like watching an intimate art stream. Without these, it feels like a chatbot in a window, not a livestream.

#### Stream Atmosphere

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Scrolling chat feed UI** | Every livestream has a chat. Without it, this is just a conversation UI, not a "stream." | MEDIUM | Chat messages from bots + user scroll upward. Needs timestamps, usernames with color, message bubbles. UI Toolkit layout similar to existing AyaChatUI but redesigned for chat feed aesthetic |
| **Aya's spoken audio output** | The host must be heard speaking. Silent text is not a stream. | LOW | Already built -- PersonaSession audio pipeline. Wire existing infrastructure |
| **Aya transcript display** | Viewers see what the streamer is saying as subtitles or a side panel | LOW | Already have OnOutputTranscription. Display in a transcript area separate from chat |
| **Stream status indicators** | "LIVE" badge, viewer count (simulated), stream duration timer | LOW | Simple UI elements. Viewer count can be a static or slowly changing number. Duration is a real timer from session start |
| **Push-to-talk for user** | The user interacts by voice, not typing. This is the core interaction mechanic. | LOW | QueuedResponse pattern already built. Need to adapt it for the livestream context (hold Space, release, Aya hears and responds) |
| **Visual recording indicator** | User must know when their mic is active | LOW | Already exists in QueuedResponseSample. Adapt for livestream UI |
| **Aya "finishes before responding"** | When user speaks, Aya completes her current thought before addressing the user. Interrupting mid-sentence breaks immersion. | MEDIUM | This is the finish-first priority pattern. Aya queues user input and addresses it after completing current response. Builds on QueuedResponse but inverted -- Aya is the one who finishes, not the user who reviews |

#### Chat Bot System

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Multiple chat bot personas in chat** | Streams have regulars who chat. An empty chat feels dead. Minimum 2-3 bots (dad, friend, random viewer) | MEDIUM | Each bot needs: name, personality, message timing, scripted lines. ScriptableObject per bot persona |
| **Bot messages appearing over time** | Bots should post messages at natural intervals, not all at once | MEDIUM | Timer-based message scheduling with randomized intervals. Real chat has irregular timing -- fast bursts, then quiet periods |
| **Bots reacting to Aya** | When Aya says something interesting, a bot might respond with "omg thats so cool" or "tell us more!" | MEDIUM | Bots listen to Aya's transcript and react. Can be scripted triggers (keyword matching) or simple LLM calls |
| **Bot personality differentiation** | Dad sounds different from a friend, who sounds different from a random viewer | LOW | Different speech patterns per bot persona config. Dad: supportive, slightly awkward. Friend: casual, uses slang. Random viewer: short, reactive |

#### Narrative Flow

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Aya drives the conversation** | Aya is not waiting for user prompts. She talks about her art, her characters, shares stories. She is the host. | LOW | System instruction establishes Aya as a proactive streamer. Conversational goals steer her topic flow. Already built -- the goal system handles this |
| **Goal progression over time** | The stream should feel like it is going somewhere, not looping. Topics evolve: warm-up -> art process -> character stories -> big reveal | MEDIUM | Time-based goal injection. Goals escalate from LOW to MEDIUM to HIGH priority. Use existing AddGoal/ReprioritizeGoal API |
| **Climactic reveal (movie clip)** | The stream builds toward showing Aya's movie clip -- the emotional payoff. Without a payoff, the narrative feels aimless | MEDIUM | Goal-triggered function call (start_movie) that loads a cinematic scene. The "reveal" is the narrative climax |

---

### Differentiators (What Makes This Special)

Features that elevate this from "chatbot with chat" to "intimate simulated stream experience." These are what make someone say "this feels real."

#### Intimacy and Presence

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Aya acknowledges user by name** | The user is not anonymous. Aya knows them, greets them, references past interactions within the session. Creates parasocial intimacy | LOW | System instruction includes user's name. Aya addresses them directly. Powerful for small-stream feel |
| **Aya reacts to user emotionally** | If user says something supportive, Aya is genuinely warm. If user asks a deep question, Aya gets reflective. Not just answering questions -- responding emotionally | LOW | Personality traits and system instruction guide emotional range. Gemini native audio handles vocal tone naturally |
| **Small viewer count aesthetic** | This is a 5-viewer stream, not a 50,000-viewer stream. The intimacy is the point. UI should feel cozy, not broadcast-scale | LOW | UI design choice. No massive chat scrolling. Calm, readable pace. Maybe 3-5 chat messages visible at a time |
| **Aya has "activities" (drawing, storytelling)** | Aya is not just talking to camera. She is doing something -- drawing her characters, which gives her natural things to talk about | LOW | System instruction frames Aya as drawing while streaming. Function calls like start_drawing() signal activity changes. Visual representation is optional (can be as simple as a status indicator) |

#### Narrative Sophistication

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Multi-phase goal escalation** | Goals evolve naturally: Phase 1 (warm-up chat), Phase 2 (art process), Phase 3 (character backstory), Phase 4 (emotional reveal setup), Phase 5 (movie clip). Time-based transitions | MEDIUM | Implement a NarrativeDirector that manages goal lifecycle. Add goals, escalate priorities, remove completed goals. Uses existing GoalManager API |
| **Chat bots as narrative nudgers** | Dad asks "so what inspired this character?" -- a question that helps Aya reach her next narrative beat. Bots are not random; they serve the story | HIGH | Requires bots to be aware of current narrative phase and inject relevant questions. Either scripted per-phase or dynamically generated |
| **User input accelerates narrative** | If the user asks about Aya's characters early, the narrative can skip ahead. User agency matters -- they are not just watching | MEDIUM | When user asks relevant questions, the NarrativeDirector detects topic alignment and can reprioritize/escalate goals |
| **Natural transitions between topics** | Aya does not abruptly switch topics. She finishes a thought, pauses, says something like "oh that reminds me..." and naturally segues | LOW | System instruction guidance. Gemini handles this well with good prompting. Goal priority framing already includes "look for natural openings" |

#### Technical Showcase

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Animation function calls** | Aya waves, spins in her chair, makes drawing gestures. These are AI-initiated, not scripted animations | LOW | Already have emote function call infrastructure. Register wave, spin, draw_gesture functions. Aya calls them naturally in conversation |
| **Goal-triggered scene transition** | When Aya reaches the movie reveal, the Unity scene transitions to a pre-rendered cinematic. This demonstrates function calling + Unity integration | MEDIUM | Function call triggers SceneManager.LoadSceneAsync with additive or single loading. Fade transition. Movie clip plays in new scene |
| **Configurable experience** | Developers can swap out bot personas, change Aya's narrative goals, adjust timing -- all via ScriptableObjects in the Inspector | LOW | Design all configuration as ScriptableObjects. NarrativeConfig SO with phase definitions, timing, goal descriptions |

---

### Anti-Features (Deliberately NOT Building)

Features that seem like good ideas but would undermine the experience, blow up scope, or miss the point.

| Anti-Feature | Why Requested | Why Problematic | Alternative |
|--------------|---------------|-----------------|-------------|
| **Real Twitch/YouTube integration** | "Make it a real stream!" | Massively increases scope (OAuth, API integration, moderation, ToS compliance). This is a sample scene, not a streaming product | Simulate the visual feel of a stream. The magic is that it LOOKS like a stream but is entirely self-contained |
| **User typing in chat** | "Let me type messages like a real chat" | Breaks the push-to-talk interaction model. Aya is voice-first. Typing would create a weird hybrid where voice and text compete. Also, typing means Aya cannot hear tone/emotion | Push-to-talk only. User speaks, Aya hears. The user's words can optionally appear in the chat feed as a transcript |
| **Multiple real users / multiplayer** | "Let my friends join the stream" | Each user needs their own Gemini session. Coordination between sessions is a separate product. Out of scope per PROJECT.md | Single user experience. The "audience" is simulated bots. The user is the special guest |
| **Bot voice output (TTS for bots)** | "Bots should speak too!" | Multiple concurrent TTS streams create audio chaos. Bots speaking would compete with Aya for audio attention. Also multiplies API costs | Bots are text-only in chat. Aya is the only voice. This mirrors real streams where chat is text and streamer is voice |
| **Persistent memory across sessions** | "Aya should remember me from last time" | Requires conversation history storage, summarization, token management. Deferred per PROJECT.md | Within-session memory only. Gemini maintains context within the active session. Cross-session memory is a future milestone |
| **Dynamic art generation** | "Show Aya actually drawing on screen" | Requires image generation integration (Imagen, DALL-E), real-time canvas rendering, significant visual system. Way out of scope | Aya TALKS about drawing. Visual representation can be a static or pre-rendered canvas image that "progresses" via simple sprite swaps or a progress indicator |
| **Chat bot conversations with each other** | "Bots should talk to each other in chat" | Creates complexity in conversation threading. Who is Aya listening to? Multiple simultaneous "conversations" confuse the narrative flow | Bots react to Aya and occasionally to the user, but do not have threaded discussions with each other. Keeps chat simple and Aya-focused |
| **User choosing Aya's personality at runtime** | "Let me customize Aya" | Aya's personality is authored content. Runtime personality changes break narrative coherence and voice consistency | Aya's personality is set in PersonaConfig. Developers can create different Aya variants as different configs, but users experience one coherent persona |
| **Skip/fast-forward narrative** | "Let me skip to the movie clip" | Destroys the pacing and emotional build-up that makes the reveal satisfying. The journey IS the experience | No skip. If the user asks Aya directly about the movie, she can hint and tease, which may naturally accelerate via goal reprioritization. But no UI skip button |
| **Complex branching narrative paths** | "Multiple endings based on user choices" | Exponential authoring cost. This is a showcase sample, not a full branching narrative game | Linear narrative with flexible pacing. User interaction affects timing and flavor, not fundamental story direction. One arc, naturally paced |

---

## Feature Dependencies

```
PersonaConfig (Aya)
    |
    v
PersonaSession (Aya's AI connection)
    |
    +-- ConversationalGoals (narrative direction)
    |     |
    |     v
    |   NarrativeDirector (time-based goal lifecycle) ---NEW
    |     |
    |     +-- Phase definitions (warm-up, art, story, reveal)
    |     +-- Goal injection/escalation timing
    |     +-- User input detection for acceleration
    |
    +-- Function Calling (animations, scene transitions)
    |     |
    |     +-- emote() -- wave, spin, draw gestures
    |     +-- start_movie() -- triggers scene load
    |     +-- start_drawing() -- activity state changes
    |
    +-- Push-to-Talk (user voice input)
    |     |
    |     v
    |   Finish-First Controller ---NEW
    |     |
    |     +-- Buffers user input while Aya speaks
    |     +-- Queues user message for Aya after current turn
    |     +-- Shows "Aya is finishing..." indicator
    |
    v
ChatBotSystem ---NEW
    |
    +-- ChatBotConfig (ScriptableObject per bot)
    |     |
    |     +-- name, personality, color
    |     +-- scripted message pool
    |     +-- timing parameters (min/max interval)
    |     +-- phase-specific lines (optional)
    |
    +-- ChatBotScheduler (timing engine)
    |     |
    |     +-- Randomized intervals per bot
    |     +-- Phase-aware message selection
    |     +-- Reactive triggers (keyword/topic detection)
    |
    +-- Dynamic Response Generator (optional)
          |
          +-- Gemini REST API call (NOT Live API)
          +-- Structured output for controlled format
          +-- Used for: reactions to user input, topic-aware nudges
          +-- Cost control: only fire on specific triggers, not every message

LivestreamUI ---NEW
    |
    +-- Chat feed (scrolling messages from bots + user)
    +-- Aya transcript panel
    +-- Stream status (LIVE badge, duration, viewer count)
    +-- Push-to-talk indicator
    +-- Recording state feedback
```

### Dependency Notes

- **NarrativeDirector requires ConversationalGoals:** NarrativeDirector wraps the existing GoalManager with time-based lifecycle management. Must be built after goals are verified working in livestream context
- **Finish-First requires QueuedResponse pattern understanding:** Inverts the QueuedResponse logic -- instead of user reviewing Aya's response, Aya finishes her response before processing user input. Conceptually similar but architecturally different
- **ChatBotSystem is independent of PersonaSession:** Bots are NOT Gemini sessions. They are a local message scheduling system. Scripted lines come from ScriptableObject configs. Dynamic lines (if used) come from a separate Gemini REST call, not the Live API
- **Dynamic Response Generator requires Gemini REST API:** The Live API does NOT support structured output. Chat bot dynamic responses must use the standard Gemini REST API (generateContent) with JSON schema output. Use gemini-2.0-flash for cost efficiency
- **LivestreamUI depends on all other systems:** Chat feed displays messages from ChatBotSystem, user transcript from push-to-talk, and Aya transcript from PersonaSession. Build UI last after data sources are working
- **Scene transition depends on Function Calling:** start_movie() function call triggers scene load. Requires function calling to be wired and working first

---

## MVP Definition

### Launch With (v1.0)

Minimum viable experience -- the stream must feel real and reach its narrative climax.

- [ ] **Aya speaks proactively as stream host** -- system instruction establishes her as an art streamer who drives conversation
- [ ] **Scrolling chat feed with 2-3 bot personas** -- dad and at least one friend posting scripted messages on timers
- [ ] **Push-to-talk user interaction** -- Space bar hold-to-talk, Aya responds after finishing current thought
- [ ] **Finish-first response priority** -- Aya completes current turn before addressing user input
- [ ] **Time-based narrative progression** -- 3-5 phases with goal injection moving from warm-up to reveal
- [ ] **Goal-triggered movie clip reveal** -- start_movie() function call loads cinematic scene
- [ ] **Stream UI** -- LIVE badge, chat feed, transcript, push-to-talk indicator, duration timer
- [ ] **Animation function calls** -- wave, spin, draw gesture emotes triggered by Aya naturally
- [ ] **Bot personality differentiation** -- each bot has distinct voice (text style) and posting patterns
- [ ] **Configurable via ScriptableObjects** -- NarrativeConfig, ChatBotConfig, PersonaConfig all Inspector-editable

### Add After Validation (v1.x)

Features to add once the core loop is proven working and feeling right.

- [ ] **Dynamic bot responses via Gemini REST** -- bots respond contextually to user speech using structured output from gemini-2.0-flash
- [ ] **Chat bots as narrative nudgers** -- phase-aware bot questions that help Aya reach narrative beats
- [ ] **User input acceleration** -- detecting when user questions align with upcoming narrative goals and advancing faster
- [ ] **Richer animation integration** -- more emote functions, activity state changes (drawing, showing, revealing)
- [ ] **Audio ambiance** -- lo-fi background music, notification sounds for chat messages, stream start chime

### Future Consideration (v2+)

Features to defer until the experience format is validated.

- [ ] **Cross-session memory** -- Aya remembers the user from previous sessions
- [ ] **Multiple narrative arcs** -- different story paths with different reveals
- [ ] **Viewer count simulation** -- dynamic viewer count that responds to stream "events"
- [ ] **Custom bot persona editor** -- runtime or editor tool for creating new bot personalities
- [ ] **Art canvas visualization** -- actual visual representation of Aya's drawing process

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Aya proactive hosting (system instruction) | HIGH | LOW | P1 |
| Chat feed UI with bot messages | HIGH | MEDIUM | P1 |
| Push-to-talk with finish-first | HIGH | MEDIUM | P1 |
| Time-based narrative progression | HIGH | MEDIUM | P1 |
| Movie clip reveal (scene transition) | HIGH | MEDIUM | P1 |
| Bot persona differentiation | MEDIUM | LOW | P1 |
| Animation function calls (emotes) | MEDIUM | LOW | P1 |
| Stream status UI (LIVE badge, timer) | MEDIUM | LOW | P1 |
| Configurable ScriptableObjects | MEDIUM | LOW | P1 |
| Dynamic bot responses (Gemini REST) | MEDIUM | HIGH | P2 |
| Chat bots as narrative nudgers | MEDIUM | HIGH | P2 |
| User input acceleration | LOW | MEDIUM | P2 |
| Audio ambiance | LOW | LOW | P3 |
| Art canvas visualization | LOW | HIGH | P3 |
| Cross-session memory | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for launch -- without these, the experience does not work
- P2: Should have -- adds depth and polish, but core experience functions without them
- P3: Nice to have -- defer unless time permits

---

## Chat Bot Behavior Design

This section provides detailed design guidance for the chat bot system, the most novel feature of this milestone.

### Message Timing Patterns

Real livestream chat follows irregular patterns. Bots should NOT post on fixed intervals.

**Timing model per bot:**
- `baseInterval`: Average time between messages (e.g., 15-30 seconds for active bot, 45-90 seconds for lurker)
- `variance`: Random deviation from base (e.g., +/- 50% of base)
- `burstChance`: Small probability of posting 2-3 messages in quick succession (e.g., 10%)
- `silencePeriod`: Occasional long silence (2-3 minutes) to simulate real behavior
- `reactDelay`: When reacting to Aya, delay of 2-8 seconds (reading + typing time)

**Phase-adjusted timing:**
- Warm-up phase: Slower posting, greetings
- Active discussion: Faster posting, reactions
- Emotional moments: Brief pause then reactions
- Reveal: Rapid excited messages

### Bot Personality Archetypes

| Bot | Role | Speech Style | Example Messages |
|-----|------|-------------|-----------------|
| **Dad (e.g., "DadOfAya")** | Supportive parent, slightly awkward with streaming culture | Earnest, uses full sentences, occasional dad jokes, proud tone | "Your mom and I are watching! She says hi", "That shading looks really professional, honey", "How do you draw so fast? Technology these days..." |
| **Friend (e.g., "xKaiArt")** | Art friend, same generation, knows Aya's work | Casual, uses abbreviations, references shared experiences, supportive but real | "omg the colors on this one", "wait is this the character from that one sketch??", "aya your anatomy has gotten SO much better" |
| **Casual Viewer (e.g., "streamfan_22")** | New viewer, curious, asks basic questions | Short messages, asks questions, uses emotes/reactions | "first time here, love the art!", "how long have you been drawing?", "what program do you use?" |

### Reactive vs Scheduled Messages

**Scheduled (scripted):** Pre-authored messages per phase, pulled from a pool with randomized selection. Each bot has a pool of 10-20 messages per phase. These fire on the timing model above.

**Reactive (triggered):** When Aya says certain keywords or reaches certain narrative points, specific bot messages fire after a short delay. Examples:
- Aya mentions her character's name -> Friend: "I love [name]!! tell us more about them"
- Aya says something emotional -> Dad: "We're so proud of you, sweetheart"
- User asks a good question -> Casual viewer: "ooh good question"

**Dynamic (Gemini REST, v1.x):** For reactions to user speech that cannot be predicted, a Gemini REST call generates a contextual response. This uses structured output to control format:
```json
{
  "botName": "xKaiArt",
  "message": "yo that's a really good point actually",
  "delay_seconds": 3.5
}
```

### Chat Bot Message Selection Algorithm

1. Each bot has an independent timer based on its timing model
2. When timer fires, select message source:
   a. Check if any reactive triggers match recent Aya transcript -> use reactive message
   b. Check current narrative phase -> pull from phase-specific pool
   c. Fallback -> pull from general pool
3. Display message in chat feed with bot name, color, and timestamp
4. Reset timer with new randomized interval

---

## Narrative Pacing Design

### Phase Structure

Drawing from drama management research (Facade beat system, drama manager patterns), the narrative should follow a multi-phase arc with increasing emotional stakes.

| Phase | Time Range | Goal Priority | Aya's Focus | Chat Bot Behavior |
|-------|-----------|---------------|-------------|-------------------|
| **1: Warm-up** | 0:00 - 2:00 | LOW | Greet viewers, settle in, start drawing | Greetings, casual banter |
| **2: Art Process** | 2:00 - 5:00 | LOW -> MEDIUM | Talk about what she's drawing, technique, inspiration | Reactions to art, questions about process |
| **3: Character Stories** | 5:00 - 9:00 | MEDIUM | Share backstories of her characters, personal connection to them | Emotional reactions, dad gets sentimental |
| **4: Emotional Build** | 9:00 - 12:00 | MEDIUM -> HIGH | Deeper personal stories, what these characters mean to her, hints at "something she's been working on" | Excitement building, friend knows what's coming |
| **5: Reveal** | 12:00+ | HIGH | Build anticipation, then trigger movie clip | Hype messages, "OMG", dad crying |

### Goal Injection Strategy

Uses existing PersonaSession.AddGoal/ReprioritizeGoal/RemoveGoal API.

**Implementation pattern:**
```
Time 0:00 - AddGoal("warmup", "Welcome viewers, settle into streaming, mention what you're drawing today", LOW)
Time 2:00 - ReprioritizeGoal("warmup", MEDIUM) + AddGoal("art_process", "Talk about your drawing technique and what inspired this piece", LOW)
Time 3:00 - RemoveGoal("warmup") + ReprioritizeGoal("art_process", MEDIUM)
Time 5:00 - AddGoal("character_story", "Share the backstory of the character you're drawing. What do they mean to you personally?", MEDIUM)
Time 7:00 - RemoveGoal("art_process")
Time 9:00 - ReprioritizeGoal("character_story", HIGH) + AddGoal("reveal_tease", "You've been working on something special - a short movie featuring your characters. Start hinting at it.", MEDIUM)
Time 11:00 - RemoveGoal("character_story") + ReprioritizeGoal("reveal_tease", HIGH)
Time 12:00+ - AddGoal("trigger_reveal", "It's time! Tell the audience you want to show them the movie you've been working on. Call start_movie() when you feel the moment is right.", HIGH)
```

**Important constraint:** Gemini Live API does NOT support mid-session system instruction updates. Goals set at connect time are fixed for the session. This means the NarrativeDirector cannot use AddGoal/ReprioritizeGoal dynamically.

**Workaround options:**
1. **Pre-load all goals at session start** with a time-based instruction: "After approximately 2 minutes, shift focus to art process. After 5 minutes, share character stories..." This embeds the arc in the system instruction rather than using dynamic goal injection.
2. **Use function calling for narrative steering:** Register a `narrative_phase` function that Aya calls periodically, and the response tells her what to focus on next.
3. **SendText as narrative injection:** Send text messages to Aya as the NarrativeDirector (e.g., "[Director note: It's been 5 minutes. Start sharing your character's backstory.]"). This keeps the conversation going while injecting new direction.
4. **Session reconnect at phase boundaries:** Disconnect and reconnect with updated system instructions at major phase transitions. Risky -- breaks conversation flow and loses context.

**Recommended approach:** Option 3 (SendText injection) combined with Option 1 (pre-loaded arc in system instruction). The system instruction describes the full narrative arc with approximate timing. SendText injects gentle nudges if Aya drifts. This avoids the mid-session instruction update limitation.

### Finish-First Priority Design

When the user speaks (push-to-talk), the system should:

1. **Capture user audio** via existing AudioCapture
2. **Send to Gemini** as normal (StartListening/StopListening)
3. **If Aya is currently speaking:** Buffer Gemini's response to the user's input (audio chunks arrive but are not played immediately)
4. **When Aya's current turn completes:** Play the buffered response

This is similar to QueuedResponse but inverted:
- QueuedResponse: User speaks -> AI responds -> User reviews -> User approves playback
- Finish-First: Aya speaks -> User interrupts -> Aya finishes current turn -> AI's response to user plays

**Key design question:** Does the user's input to Gemini interrupt Aya's current audio? In the Gemini Live API, sending audio while the model is speaking triggers an interruption. This means we need to:
- Either delay sending user audio until Aya finishes (buffer locally, send after TurnComplete)
- Or allow the interruption but re-send the context of what Aya was saying

**Recommended:** Buffer user audio locally. Do NOT send to Gemini until Aya's current turn completes. Then send the buffered audio. This preserves Aya's current speech and ensures she finishes her thought before hearing the user.

---

## Competitor Feature Analysis

| Feature | Neuro-sama (AI VTuber) | Character.AI | Inworld AI | Our Approach |
|---------|----------------------|--------------|-----------|--------------|
| Voice output | Yes (TTS) | No (text only) | Yes (TTS) | Yes -- Gemini native or Chirp TTS |
| Chat interaction | Real Twitch chat | Direct conversation | Scripted + dynamic | Simulated chat with scripted bots |
| Narrative goals | No structured goals | No | Yes (goals, relationships) | Yes -- ConversationalGoals with priority steering |
| Multiple personas | Single AI + human handler | One at a time | Multiple NPCs | Aya (voice) + bots (text), single Gemini session |
| User voice input | No (text chat only) | No | Yes | Yes -- push-to-talk |
| Proactive speech | Yes (generates unprompted) | No (responds only) | Yes (can initiate) | Yes -- Aya drives conversation proactively |
| Activity simulation | Games, singing | N/A | Context-dependent | Art streaming (described, not visually rendered) |
| Emotional range | Limited | Moderate | Good | Good -- Gemini native audio conveys tone naturally |

**Key insight from Neuro-sama research:** Audiences value "consistency-as-authenticity" -- they know it is AI but develop attachment because the AI maintains a coherent, reliable character. This validates our approach of a well-defined PersonaConfig with clear personality traits, backstory, and speech patterns. Aya should be CONSISTENTLY Aya, not a generic chatbot. The research also shows an "inverted parasocial dynamic" where fans actively probe and shape AI responses through questions and commands. This aligns with our push-to-talk design where the user actively engages rather than passively watching.

---

## Confidence Notes

| Area | Confidence | Reason |
|------|------------|--------|
| Table stakes features | HIGH | Derived from extensive VTuber/livestream research and existing codebase analysis |
| Chat bot behavior design | MEDIUM-HIGH | Timing patterns from streaming platform research; specific implementation untested |
| Narrative pacing | MEDIUM | Drama manager patterns well-documented in academic literature; specific Gemini Live behavior with long system instructions needs validation |
| Finish-first priority | MEDIUM | Conceptually sound; Gemini Live interruption behavior with buffered audio needs testing |
| Mid-session goal limitation | HIGH | Verified in existing codebase -- SendGoalUpdate() logs a warning about mid-session instruction updates not being supported |
| Gemini REST for bot responses | HIGH | Standard Gemini API (non-Live) with structured output is well-documented and verified |
| Anti-features list | HIGH | Clear scoping from PROJECT.md constraints and architectural principles |

## Sources

- [My Favorite Streamer is an LLM: AI VTuber Fandom Research (arxiv)](https://arxiv.org/html/2509.10427v1) -- Chat interaction patterns, parasocial dynamics, consistency-as-authenticity
- [Neuro-sama Wikipedia](https://en.wikipedia.org/wiki/Neuro-sama) -- AI VTuber technical architecture reference
- [Facade Interactive Drama Design](https://www.gamedeveloper.com/design/the-story-of-facade-the-ai-powered-interactive-drama) -- Beat system, drama manager, narrative pacing patterns
- [Drama Management for Interactive Fiction (Georgia Tech)](https://sites.cc.gatech.edu/fac/ashwin/papers/er-09-10.pdf) -- Drama manager architecture, goal escalation
- [Gemini Live API Documentation](https://ai.google.dev/gemini-api/docs/live) -- Response modalities, session constraints
- [Gemini Live API Capabilities Guide](https://ai.google.dev/gemini-api/docs/live-guide) -- TEXT vs AUDIO modality, no structured output in Live API
- [Gemini Structured Output Documentation](https://ai.google.dev/gemini-api/docs/structured-output) -- JSON schema for REST API bot responses
- [VTuber Content Ideas (Creatoko)](https://creatoko.com/vtuber-content-stream-ideas/) -- Art stream format, cozy stream aesthetics
- [AI Streamers Research (Clemson CHI 2026)](https://guof.people.clemson.edu/papers/chi26streaming.pdf) -- AI streamer audience interaction patterns
- [Push-to-Talk UX Design (Vertext Labs)](https://vertextlabs.com/push-to-talk-generative-voice-business-messaging/) -- PTT latency, recording indicators, queue management
- Existing codebase: PersonaSession.cs, GoalManager.cs, QueuedResponseController.cs, AyaSampleController.cs
- PROJECT.md constraints and out-of-scope definitions

---

*Feature landscape analysis for v1.0 Livestream Experience milestone*
*Researched: 2026-02-17*
