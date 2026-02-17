# Architecture Patterns: v1.0 Livestream Experience

**Domain:** Livestream sample scene integrating chat bots, narrative steering, and scene transitions with existing AI Embodiment package
**Researched:** 2026-02-17
**Overall confidence:** HIGH (based on direct reading of all 26 runtime source files in the package, Gemini Live API WebSocket reference, and Gemini structured output documentation)

## Executive Summary

The v1.0 Livestream Experience builds a sample scene on top of the existing AI Embodiment package. The existing package components (PersonaSession, QueuedResponseController, ConversationalGoals, AudioPlayback, SyncPackets) are well-architected and should NOT be modified at the package level. All new components live in the sample scene layer (`Samples~/LivestreamSample/`), consuming the package's public API surface.

The core architectural question -- "Should chat bots share Aya's PersonaSession?" -- has a clear answer: **No.** Chat bots should use separate, lightweight Gemini REST calls (`generateContent`) for structured JSON output, not the Live API WebSocket. Aya's PersonaSession remains the single Live API connection. This avoids multiplying WebSocket sessions (expensive, stateful, audio-oriented) when chat bots only need text-in/text-out.

## Recommended Architecture

```
+=========================================================================+
|                        LIVESTREAM SAMPLE SCENE                          |
|  (Assets/LivestreamSample/ or Samples~/LivestreamSample/)              |
|                                                                         |
|  +------------------+     +-------------------+     +-----------------+ |
|  | LivestreamController |  | NarrativeDirector  |  | SceneTransition | |
|  | (main orchestrator) |  | (time-based goals) |  | (goal-triggered)| |
|  +--------+---------+   +--------+----------+   +--------+--------+ |
|           |                      |                        |           |
|           |   +------------------+                        |           |
|           |   |                                           |           |
|           v   v                                           |           |
|  +------------------+                                     |           |
|  |  ChatBotManager  |     +-------------------+           |           |
|  |  (REST Gemini)   |     |  LivestreamUI     |<----------+           |
|  +--------+---------+     |  (chat feed, PTT) |                      |
|           |                +-------------------+                      |
+===========|==========================================================+
            |
+-----------v---------------------------------------------------------+
|                    EXISTING PACKAGE (unchanged)                      |
|                                                                      |
|  PersonaSession  GoalManager  FunctionRegistry  AudioPlayback       |
|  GeminiLiveClient  PacketAssembler  AudioCapture  PersonaConfig     |
|  SyncPacket  ConversationalGoal  SystemInstructionBuilder            |
+----------------------------------------------------------------------+
```

### Component Boundaries

| Component | Layer | Responsibility | Communicates With | New/Existing |
|-----------|-------|---------------|-------------------|--------------|
| **PersonaSession** | Package | Aya's Gemini Live WebSocket session, events, audio, function calling, goals | GeminiLiveClient, GoalManager, AudioPlayback | EXISTING -- no changes |
| **GoalManager** | Package | Stores/composes goal instructions for system prompt | PersonaSession (internal) | EXISTING -- no changes |
| **QueuedResponseController** | Package Sample | 5-state push-to-talk audio buffering | PersonaSession, AudioPlayback, UI | EXISTING -- study for patterns |
| **LivestreamController** | Sample | Top-level orchestrator: wires Aya session, ChatBotManager, NarrativeDirector, user input, SceneTransition | PersonaSession, ChatBotManager, NarrativeDirector, LivestreamUI, SceneTransition | **NEW** |
| **ChatBotManager** | Sample | Manages bot personas, scripted timers, Gemini REST structured output calls for dynamic responses | LivestreamController, LivestreamUI | **NEW** |
| **NarrativeDirector** | Sample | Time-based goal escalation, conversation progress tracking toward movie reveal | PersonaSession (AddGoal/ReprioritizeGoal), LivestreamController | **NEW** |
| **LivestreamUI** | Sample | Chat feed (bots + Aya + user), push-to-talk controls, stream status, Aya transcript | LivestreamController, PersonaSession events | **NEW** |
| **SceneTransition** | Sample | Loads Unity movie clip scene when goal fires via function call | PersonaSession (function handler) | **NEW** |
| **ChatBotConfig** | Sample | ScriptableObject defining a bot persona (name, avatar color, scripted lines, personality for dynamic responses) | ChatBotManager | **NEW** |

## Key Architecture Decisions

### Decision 1: Chat Bots Use REST generateContent, Not Live API

**Recommendation:** Chat bots use separate `UnityWebRequest` HTTP calls to `generateContent` (Gemini REST API) with structured JSON output, NOT the Live API WebSocket.

**Rationale:**
- Chat bots need text-in/text-out only. No audio, no streaming, no function calling.
- The Live API (`BidiGenerateContent`) is stateful, holds a persistent WebSocket, and is designed for real-time bidirectional audio. Each session consumes server resources and has rate limits.
- The REST `generateContent` endpoint with `responseMimeType: "application/json"` and a `responseSchema` gives guaranteed JSON structure for bot messages.
- Cost: REST calls to gemini-2.5-flash are cheap ($0.15/1M input, $0.60/1M output tokens). A chat bot message is ~100 tokens round-trip -- negligible cost.
- Latency: REST calls take 500ms-2s, which is acceptable for chat bot messages (they appear as typed text, not speech).
- gemini-2.5-flash supports structured output (verified in official docs).

**Implementation:** A new `GeminiTextClient` utility class wraps `UnityWebRequest` for `POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent`. Returns parsed JSON. Reusable across all chat bots.

**Confidence:** HIGH -- verified that gemini-2.5-flash supports structured output via official documentation.

### Decision 2: Goals Set at Connect Time, Escalated via Text Injection

**Critical constraint:** The Gemini Live API does NOT support mid-session system instruction updates. The `SendGoalUpdate()` method in PersonaSession already documents this limitation:

```csharp
// Gemini Live API does not support mid-session system instruction updates.
// Goals accumulate locally and will be applied at next Connect().
```

**Workaround for narrative steering:**

1. **Pre-load initial goals at connect time.** Aya's PersonaSession should be connected with an initial set of goals already in the system instruction. The NarrativeDirector configures these before `Connect()` is called.

2. **Use `SendText()` for mid-session steering.** PersonaSession.SendText() sends `clientContent` via the WebSocket. NarrativeDirector can inject invisible steering text as "system" messages. Example:
   ```
   session.SendText("[DIRECTOR NOTE: The audience is asking about your characters. " +
       "Start transitioning toward sharing the movie clip you've been working on. " +
       "Build excitement naturally.]");
   ```
   This is not a system instruction update -- it is a user-role text message that Aya's model will treat as conversational context. The system instruction at connect time should include a meta-instruction telling Aya to follow `[DIRECTOR NOTE: ...]` tags as stage directions.

3. **Session resume for major goal changes.** If a goal change is radical enough to need a new system instruction, use Gemini's session resumption: disconnect, reconnect with new goals, and the 2-hour resumption token preserves conversation context. This should be a last resort -- the `SendText()` approach handles incremental steering.

**Confidence:** HIGH -- verified via Gemini Live API WebSocket reference that configuration cannot be updated mid-session, and that `clientContent` text can be sent during an active audio session.

### Decision 3: Finish-First Priority via State Machine Extension

**How it works:** The user's push-to-talk input should be queued (not sent to Gemini immediately) while Aya is speaking. When Aya's turn completes (`OnTurnComplete`), the queued user audio is released.

**Implementation:** LivestreamController extends the QueuedResponseController pattern with an additional state:

```
LivestreamState:
  Connecting    -- waiting for PersonaSession.Connect()
  Streaming     -- Aya is speaking autonomously or responding to bots
  UserRecording -- user is holding push-to-talk (Aya continues current response)
  UserQueued    -- user released push-to-talk, audio buffered, waiting for Aya to finish
  UserPlaying   -- Aya is responding to user's input
```

The key difference from QueuedResponseController is that in the **Streaming** state, Aya speaks without user input (driven by chat bot questions and narrative flow). The user can press push-to-talk at any time -- audio is captured and buffered but NOT sent to Gemini until Aya's current turn completes.

**Why not suppress at the PersonaSession level:** The finish-first behavior is a sample-specific UX choice, not a package feature. Other samples (like the existing AyaLiveStream) may want immediate interruption. Keep the behavior in the sample controller.

**Confidence:** HIGH -- the existing QueuedResponseController code demonstrates this buffering pattern already.

### Decision 4: NarrativeDirector Drives Goals via Pre-Connect + SendText

The NarrativeDirector is a time-based state machine that manages the narrative arc:

```
NarrativePhase:
  WarmUp         -- 0-2 min: Aya introduces herself, ambient chat bots
  CharacterIntro -- 2-5 min: Aya talks about her characters (medium priority goal)
  BuildUp        -- 5-8 min: Aya hints at the movie project (high priority goal)
  Reveal         -- 8+ min: Aya reveals the movie clip (triggers start_movie function)
```

**Interaction with ConversationalGoals:**
- At connect time: NarrativeDirector calls `session.AddGoal()` for the initial goals BEFORE `session.Connect()`. These get baked into the system instruction.
- During the session: NarrativeDirector uses `session.SendText()` with `[DIRECTOR NOTE: ...]` tags to steer Aya through phases. These are invisible text messages injected into the conversation context.
- The `start_movie` function call is already registered as a function (seen in existing AyaSampleController). When Aya decides it is time, she calls `start_movie`, which triggers SceneTransition.

**Why NarrativeDirector does not call AddGoal/ReprioritizeGoal mid-session:** Because the Gemini Live API does not support mid-session system instruction updates. The PersonaSession's `SendGoalUpdate()` explicitly logs this. Instead, use SendText() for runtime steering.

### Decision 5: Chat Bot Data Flow

```
Timer tick or user speaks
        |
        v
ChatBotManager selects bot and message type
        |
        +--- SCRIPTED: timer-driven, picks from ChatBotConfig.scriptedLines[]
        |    |
        |    v
        |    LivestreamUI.AddChatMessage(botName, message)
        |    |
        |    v
        |    PersonaSession.SendText($"[CHAT] {botName}: {message}")
        |    (Aya sees the chat message as context and may respond to it)
        |
        +--- DYNAMIC: user said something interesting, bot reacts
             |
             v
             GeminiTextClient.GenerateAsync(prompt, responseSchema)
             |  (REST call to generateContent with structured output)
             |  prompt includes: bot personality, user's message, Aya's recent transcript
             |  responseSchema: { "message": "string", "emote": "string?" }
             v
             LivestreamUI.AddChatMessage(botName, response.message)
             |
             v
             PersonaSession.SendText($"[CHAT] {botName}: {response.message}")
```

**Key insight:** Chat bot messages are sent to Aya via `PersonaSession.SendText()` so she sees them as conversation context and can react. The system instruction tells Aya: "You are hosting a livestream. Messages prefixed with [CHAT] are from your audience. Respond to interesting ones naturally."

**Confidence:** HIGH -- `SendText()` is a proven API in the existing codebase for injecting text into the Live session.

## Data Flow Diagrams

### Complete Data Flow: User Push-to-Talk Through System

```
USER                   LIVESTREAM SCENE                              PACKAGE
                       LAYER                                         LAYER
 |                      |                                              |
 | press spacebar       |                                              |
 |--------------------->| LivestreamController                         |
 |                      | state = UserRecording                        |
 |                      |                                              |
 |                      | If Aya is speaking:                          |
 |                      |   buffer audio locally                       |
 |                      |   (DO NOT call session.StartListening)       |
 |                      |                                              |
 | release spacebar     |                                              |
 |--------------------->| LivestreamController                         |
 |                      | state = UserQueued                           |
 |                      |                                              |
 |                      |                  OnTurnComplete               |
 |                      |<---------------------------------------------| PersonaSession
 |                      |                                              |
 |                      | state = UserPlaying                          |
 |                      | flush buffered audio:                        |
 |                      |   session.StartListening()                   |
 |                      |   send buffered chunks -------->             | PersonaSession
 |                      |   session.StopListening()                    | -> GeminiLiveClient
 |                      |                                              |    -> WebSocket
 |                      |                                              |
 |                      |                  OnOutputTranscription        |
 |                      |<---------------------------------------------| PersonaSession
 |                      | LivestreamUI.SetAyaTranscript(text)          |
 |                      |                                              |
 |                      |                  OnSyncPacket (audio)        |
 |                      |<---------------------------------------------| PersonaSession
 |                      |                                              | -> AudioPlayback
 |                      |                                              |    -> ring buffer
 |                      |                                              |    -> AudioSource
```

**Critical detail on buffered audio:** The existing AudioCapture produces float[] chunks that are normally sent directly to GeminiLiveClient.SendAudio() via PersonaSession.HandleAudioCaptured(). For finish-first, the LivestreamController needs to intercept this:

- Option A: Use AudioCapture directly (subscribe to OnAudioCaptured), buffer chunks in a List<float[]>, then replay them via session.StartListening() when ready. Problem: StartListening() starts the mic again, it does not replay buffered audio.
- Option B (recommended): Record via AudioCapture, buffer the float[] chunks, then after Aya finishes, convert the buffered chunks to PCM bytes and send them directly via `_session.SendText()` with a note that audio follows, or find a way to feed buffered audio. But PersonaSession does not expose direct audio sending without mic capture.

**Revised approach for finish-first:** Rather than buffering raw audio, use a simpler model:
1. User presses push-to-talk. If Aya is speaking, show "Hold on, Aya is finishing..." in the UI. Do NOT start mic capture yet.
2. When Aya's OnTurnComplete fires, THEN start mic capture normally via session.StartListening().
3. User speaks, releases, audio goes through the normal PersonaSession pipeline.

This is simpler, avoids audio buffering complexity, and gives better UX (user knows to wait). The "finish-first" concept becomes "Aya completes, then you speak" rather than "you speak into a buffer while Aya finishes."

**Alternative if true buffering is needed:** Add a `SendRawAudio(byte[] pcm)` method to PersonaSession that wraps `_client.SendAudio()`. This is a small, safe package API addition. But the simpler approach above should be tried first.

### Chat Bot Reaction to User Input

```
USER speaks to Aya
        |
        v
PersonaSession.OnInputTranscription
        |
        v
LivestreamController receives user transcript
        |
        v
ChatBotManager.OnUserSpoke(userTranscript)
        |
        v
Select 0-2 bots to react (based on personality match, random chance)
        |
        v
For each reacting bot:
  GeminiTextClient.GenerateAsync(
    systemPrompt: bot.personality + "You are a viewer in a livestream chat",
    userPrompt: $"The viewer just said: '{userTranscript}'. React briefly.",
    responseSchema: { message: string }
  )
        |
        v (500ms-2s async)
LivestreamUI.AddChatMessage(bot.name, response.message)
        |
        v
PersonaSession.SendText($"[CHAT] {bot.name}: {response.message}")
        |
        v
Aya sees the chat message and may comment on it
```

### NarrativeDirector Timeline

```
Time    NarrativePhase     Actions
0:00    WarmUp             - Aya's system instruction includes personality + initial goals
                           - ChatBotManager starts scripted timer (greetings every 15-30s)
                           - No steering yet

2:00    CharacterIntro     - NarrativeDirector.SendText("[DIRECTOR NOTE: Start talking
                             about your characters and what inspires your art]")
                           - ChatBotManager injects scripted questions:
                             "hey aya who's that character on the left?"

5:00    BuildUp            - NarrativeDirector.SendText("[DIRECTOR NOTE: You've been
                             hinting at a special project. Start building excitement
                             about the movie clip you've been working on]")
                           - ChatBotManager injects: "wait are you making a MOVIE??"

8:00    Reveal             - NarrativeDirector.SendText("[DIRECTOR NOTE: It's time.
                             Announce the movie clip and call start_movie]")
                           - Aya calls start_movie() function
                           - SceneTransition.LoadMovieScene()
```

## Component Dependency Graph (Build Order)

```
Phase 1: Foundation
  ChatBotConfig (ScriptableObject)          -- standalone, no dependencies
  GeminiTextClient (REST utility)           -- depends on: UnityWebRequest, Newtonsoft.Json
  LivestreamUI (UXML/USS + MonoBehaviour)   -- depends on: UIDocument, PersonaSession events

Phase 2: Chat Bot System
  ChatBotManager                            -- depends on: ChatBotConfig[], GeminiTextClient
                                               LivestreamUI

Phase 3: Narrative + Scene
  NarrativeDirector                         -- depends on: PersonaSession.AddGoal/SendText
  SceneTransition                           -- depends on: PersonaSession function calling,
                                               UnityEngine.SceneManagement

Phase 4: Orchestrator + Integration
  LivestreamController                      -- depends on: PersonaSession, ChatBotManager,
                                               NarrativeDirector, LivestreamUI, SceneTransition
  Aya PersonaConfig (ScriptableObject)      -- configured with livestream-specific
                                               system instruction, goals, functions
```

**Rationale for ordering:**
- Phase 1 has zero dependencies on other new components. Each can be built and tested in isolation.
- Phase 2 introduces the first inter-component communication (ChatBotManager uses GeminiTextClient and LivestreamUI).
- Phase 3 adds narrative logic that depends on PersonaSession's existing public API.
- Phase 4 wires everything together. The LivestreamController is built last because it depends on all other components.

## Which Existing Components Need Modification

### Package Runtime (com.google.ai-embodiment): NO CHANGES NEEDED

The existing public API surface is sufficient for the livestream sample:

| API Used | Existing? | Notes |
|----------|-----------|-------|
| `PersonaSession.Connect()` | Yes | Connect with goals pre-loaded |
| `PersonaSession.StartListening()` / `StopListening()` | Yes | Push-to-talk |
| `PersonaSession.SendText(string)` | Yes | Director notes + chat bot messages to Aya |
| `PersonaSession.AddGoal(id, desc, priority)` | Yes | Pre-connect goal setup |
| `PersonaSession.RegisterFunction(name, decl, handler)` | Yes | `start_movie`, `emote` |
| `PersonaSession.OnOutputTranscription` | Yes | Aya's speech transcript |
| `PersonaSession.OnInputTranscription` | Yes | User's speech transcript |
| `PersonaSession.OnTurnComplete` | Yes | Finish-first signal |
| `PersonaSession.OnSyncPacket` | Yes | Audio + text correlation |
| `PersonaSession.OnStateChanged` | Yes | Connection lifecycle |
| `PersonaSession.OnAISpeakingStarted/Stopped` | Yes | UI speaking indicator |
| `PersonaSession.OnFunctionError` | Yes | Error logging |
| `PersonaSession.Config` | Yes | Read persona display name |

All new functionality lives in the sample scene layer. The package remains a clean, general-purpose library.

**One potential small addition (optional):** A `SendRawAudio(byte[] pcm)` public method on PersonaSession, if true audio buffering is needed for finish-first. But the simpler "wait then speak" approach avoids this entirely.

### Package Samples: EXISTING SAMPLES UNCHANGED

The AyaLiveStream and QueuedResponseSample remain as-is. The LivestreamSample is a new, third sample scene.

## File Structure

```
Packages/com.google.ai-embodiment/
  Samples~/
    LivestreamSample/                          # NEW sample
      LivestreamSample.unity                   # Scene file
      LivestreamController.cs                  # Main orchestrator MonoBehaviour
      ChatBotManager.cs                        # Bot personas + Gemini REST calls
      ChatBotConfig.cs                         # ScriptableObject for bot definition
      GeminiTextClient.cs                      # REST utility for generateContent
      NarrativeDirector.cs                     # Time-based goal steering
      SceneTransition.cs                       # Function handler for scene loading
      LivestreamUI.cs                          # UI Toolkit controller
      AyaLivestreamConfig.asset                # PersonaConfig for Aya (livestream)
      ChatBots/                                # Bot persona assets
        ChatterBot1.asset
        ChatterBot2.asset
        ChatterBot3.asset
      UI/
        LivestreamPanel.uxml                   # UI layout
        LivestreamPanel.uss                    # UI styles
      MovieClip/
        MovieClipScene.unity                   # Scene loaded on goal trigger
```

Also mirrored in Assets/ for development:
```
Assets/LivestreamSample/                       # Development copy (same files)
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Multiple Live API WebSocket Sessions

**What:** Creating a PersonaSession per chat bot to give each bot its own Gemini Live connection.

**Why bad:** Each Live API session is a persistent WebSocket with server-side state (context window, audio buffers). Multiple sessions multiply: API costs, rate limit consumption, audio thread overhead, and WebSocket connection management. Chat bots do not need real-time audio -- they produce text messages.

**Instead:** One PersonaSession for Aya (the only character that speaks). Chat bots use lightweight REST `generateContent` calls.

### Anti-Pattern 2: Modifying Package Code for Sample-Specific Behavior

**What:** Adding livestream-specific logic (finish-first, narrative director) into PersonaSession or GoalManager.

**Why bad:** The package is a general-purpose library. Livestream UX is one specific use case. Baking sample behavior into the package makes it harder for other developers to use the package differently.

**Instead:** All livestream-specific logic lives in the sample scene layer. The sample consumes the package's public API without modification.

### Anti-Pattern 3: Polling Goals Mid-Session

**What:** Calling `ReprioritizeGoal()` or `AddGoal()` repeatedly during a live session expecting Aya to change behavior.

**Why bad:** The Gemini Live API does not support mid-session system instruction updates. Goals only take effect at `Connect()` time. The PersonaSession code explicitly logs this limitation.

**Instead:** Use `SendText()` with `[DIRECTOR NOTE: ...]` tags for mid-session steering. Set all initial goals before `Connect()`.

### Anti-Pattern 4: Synchronous Gemini REST Calls

**What:** Blocking the main thread while waiting for a `generateContent` response for chat bot messages.

**Why bad:** REST calls to Gemini take 500ms-2s. Blocking the main thread causes frame drops and audio glitches.

**Instead:** Use `async/await` with `UnityWebRequest.SendWebRequest()` in the GeminiTextClient. Chat bot messages appear asynchronously when the response arrives.

### Anti-Pattern 5: Direct Audio Buffer Manipulation for Finish-First

**What:** Trying to record user audio into a buffer while Aya speaks, then replaying the buffer through PersonaSession's audio pipeline.

**Why bad:** PersonaSession's audio pipeline (AudioCapture -> HandleAudioCaptured -> FloatToPcm16 -> GeminiLiveClient.SendAudio) is tightly coupled. There is no public API to inject pre-recorded audio. Attempting to replay buffered audio would require either modifying the package or working around its internals.

**Instead:** Use the simpler "wait then speak" approach: when Aya is speaking and user presses push-to-talk, show a "Aya is finishing..." indicator. When OnTurnComplete fires, enable mic capture normally.

## Scalability Considerations

| Concern | This Sample (3-5 bots) | Future (10+ bots) |
|---------|------------------------|---------------------|
| REST API calls | 3-5 concurrent, well within rate limits | May need request queuing, debouncing |
| Chat UI messages | Simple ScrollView, ~50 messages | Need virtualization or message culling |
| SendText frequency | Every 15-30s for chat, fine | High frequency may fill context window; use summarization |
| Context window | ~30 min session, manageable | Need context compression or session resume |
| Audio playback | Single AudioSource for Aya | Unchanged -- only Aya speaks |

## Sources

- PersonaSession.cs (lines 354-386: AddGoal/RemoveGoal/ReprioritizeGoal API, lines 796-806: SendGoalUpdate limitation)
- QueuedResponseController.cs (lines 1-209: 5-state machine pattern for push-to-talk)
- AyaSampleController.cs (lines 1-162: function registration, goal injection after warm-up exchanges)
- GeminiLiveClient.cs (lines 127-148: SendText via clientContent)
- GoalManager.cs (lines 1-134: goal composition for system instruction)
- SystemInstructionBuilder.cs (lines 1-115: system instruction generation with goals)
- [Gemini Live API WebSocket Reference](https://ai.google.dev/api/live) -- configuration cannot be updated mid-session
- [Gemini Structured Output Documentation](https://ai.google.dev/gemini-api/docs/structured-output) -- gemini-2.5-flash supports structured JSON output
- [Gemini Live API Capabilities Guide](https://ai.google.dev/gemini-api/docs/live-guide) -- clientContent text can be sent during active audio session
- [Gemini Session Management](https://ai.google.dev/gemini-api/docs/live-session) -- session resumption with 2-hour token validity

---

*Architecture research: 2026-02-17*
