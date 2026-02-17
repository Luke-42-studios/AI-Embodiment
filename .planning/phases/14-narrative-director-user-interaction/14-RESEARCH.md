# Phase 14: Narrative Director & User Interaction - Research

**Researched:** 2026-02-17
**Domain:** Beat/scene narrative orchestration, dual-queue scheduling, push-to-talk finish-first with transcript approval, Gemini Live API SendText steering
**Confidence:** HIGH

## Summary

Phase 14 builds a NarrativeDirector that drives Aya through a 3-beat narrative arc (warm-up, art process, characters) using time-based goal lifecycle and SendText steering notes, while a dual-queue system runs Aya scenes (sequential) and chat scenes (parallel) independently. The push-to-talk system is enhanced with finish-first priority (Aya completes her current response before addressing user input), visual acknowledgment within 500ms, and a transcript approval overlay with 3-second auto-submit.

The existing codebase provides strong foundations: PersonaSession has events for all speaking states (OnAISpeakingStarted/Stopped, OnUserSpeakingStarted/Stopped, OnTurnComplete, OnInputTranscription), ChatBotManager runs a burst loop that can be externally paced, LivestreamUI has chat feed, Aya transcript panel, and PTT status elements, and the GeminiLiveClient.SendText method sends `clientContent` with `turnComplete=true` which triggers Aya to respond -- making it the proven mechanism for director notes.

The nevatars NarrativeBeat/NarrativeScene data model is comprehensive (SceneType enum, ConditionType/ConsequenceType for transitions, per-scene dialogue/action alternates) but significantly over-engineered for the AI-Embodiment demo. Per CONTEXT.md, nevatars data is used as "inspiration for redesigned beats, not migrated as-is." The new data model should be a simplified ScriptableObject with 3 beats, each containing an ordered scene list, time budget, goal description, and director note text.

**Primary recommendation:** Build NarrativeDirector as a MonoBehaviour that owns a time-based beat lifecycle, sends SendText director notes at beat transitions, and coordinates the dual-queue by exposing a `bool IsAyaSpeaking` flag and `event Action OnBeatTransition` for ChatBotManager pacing. The PTT controller is a separate MonoBehaviour that subscribes to PersonaSession events and manages the finish-first state machine with a 4-state flow: Idle, AyaFinishing, Reviewing, Submitted.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Unity 6 ScriptableObject | 6000.x | NarrativeBeat/Scene data authoring | Inspector-friendly, producer-editable without code changes (per CONTEXT.md decision) |
| Unity 6 Awaitable | 6000.x | Async beat timing, auto-submit countdown | Already used by ChatBotManager burst loop; consistent pattern |
| Unity UI Toolkit | 6000.x | Transcript overlay, PTT acknowledgment UI | Already used by LivestreamUI; consistent pattern |
| Unity InputSystem | 1.x | Push-to-talk key detection | Already used by AyaSampleController for SPACE key |
| PersonaSession (existing) | local | SendText for director notes, all speaking/turn events | Core package class; events already wired |
| ChatBotManager (existing) | local | Burst loop pacing, TrackedChatMessage access | Built in Phase 13; ready for dual-queue integration |
| LivestreamUI (existing) | local | Chat feed, Aya transcript, PTT status | Built in Phase 12; ready for overlay additions |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| GeminiLiveClient.SendText | local | Injects director notes as user-role text | At beat transitions and when Aya checks chat |
| GoalManager/ConversationalGoal | local | Escalating urgency goals per beat | NOTE: Goals only apply at next Connect() -- use SendText instead for mid-session |
| destroyCancellationToken | Unity 6 | Clean async cancellation on scene unload | All Awaitable loops (beat timer, auto-submit, burst pacing) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ScriptableObject beats | JSON config files | SO gives Inspector editing, drag-drop, Unity serialization for free |
| Awaitable delays for timing | Coroutines (IEnumerator) | Awaitable is Unity 6 standard, matches ChatBotManager pattern, better cancellation |
| SendText for steering | GoalManager.AddGoal | Goals only apply at next Connect() (confirmed in PersonaSession.SendGoalUpdate line 801-805) -- SendText works mid-session |
| Custom PropertyDrawer | Default Inspector | Default is fine for 3 beats; custom drawer adds complexity without enough benefit |

## Architecture Patterns

### Recommended Project Structure
```
Assets/AyaLiveStream/
  NarrativeDirector.cs          # NEW -- beat lifecycle, dual-queue coordination, SendText steering
  NarrativeBeatConfig.cs        # NEW -- ScriptableObject data model for beats and scenes
  PushToTalkController.cs       # NEW -- finish-first state machine, transcript approval flow
  ChatBotManager.cs             # MODIFIED -- add pacing control (slow down when Aya talks)
  LivestreamUI.cs               # MODIFIED -- add transcript overlay, acknowledgment indicator
  AyaSampleController.cs        # MODIFIED -- wire NarrativeDirector + PTT controller
  Data/
    Beat_WarmUp.asset           # NEW -- beat 1: getting to know Aya (ScriptableObject)
    Beat_ArtProcess.asset       # NEW -- beat 2: art process discussion
    Beat_Characters.asset       # NEW -- beat 3: character discussion -> movie reveal
  UI/
    LivestreamPanel.uxml        # MODIFIED -- add transcript overlay container, ack indicator
    LivestreamPanel.uss         # MODIFIED -- styles for overlay, countdown, ack
```

### Pattern 1: Time-Based Beat Lifecycle
**What:** NarrativeDirector runs a beat loop where each beat has a time budget and a goal condition. The beat advances when either the goal is met (early exit) or time expires (fallback). At each transition, a SendText director note steers Aya to the next topic.
**When to use:** Always -- this is the core orchestration pattern.
**Example:**
```csharp
// Source: designed from existing PersonaSession.SendText + GoalManager patterns
private async Awaitable RunBeatLoop()
{
    try
    {
        for (int i = 0; i < _beats.Length; i++)
        {
            _currentBeatIndex = i;
            NarrativeBeatConfig beat = _beats[i];

            // Send director note to steer Aya
            if (!string.IsNullOrEmpty(beat.directorNote))
            {
                _session.SendText(beat.directorNote);
            }

            OnBeatStarted?.Invoke(beat);

            // Wait for goal completion or time budget expiry
            float elapsed = 0f;
            while (elapsed < beat.timeBudgetSeconds && !_beatGoalMet)
            {
                await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);
                elapsed += 1f;
            }

            _beatGoalMet = false;
            OnBeatEnded?.Invoke(beat);

            // Sync point: queues sync at beat boundary
            OnBeatTransition?.Invoke();
        }
    }
    catch (OperationCanceledException) { }
}
```

### Pattern 2: Dual-Queue Coordination
**What:** Aya scenes run sequentially on the "Aya queue" (AyaDialogue, AyaChecksChat, AyaAction). Chat scenes run in parallel on the "chat queue" (ChatBurst via ChatBotManager). The queues run independently within a beat but sync at beat boundaries. Chat bursts slow down when Aya is speaking (longer delays, fewer bots per burst).
**When to use:** Always -- this is how the livestream experience feels real.
**Example:**
```csharp
// Source: designed from ChatBotManager burst loop + PersonaSession event pattern

// NarrativeDirector exposes state for ChatBotManager pacing
public bool IsAyaSpeaking { get; private set; }
public event Action OnBeatTransition;

// ChatBotManager checks NarrativeDirector state during bursts
private async Awaitable ScriptedBurstLoop()
{
    try
    {
        while (_running)
        {
            // Slow down when Aya is speaking (per CONTEXT.md decision)
            float lullMin = _narrativeDirector != null && _narrativeDirector.IsAyaSpeaking
                ? _burstIntervalMin * 2f   // Double the minimum lull
                : _burstIntervalMin;
            float lullMax = _narrativeDirector != null && _narrativeDirector.IsAyaSpeaking
                ? _burstIntervalMax * 1.5f // 50% longer maximum
                : _burstIntervalMax;
            int maxBots = _narrativeDirector != null && _narrativeDirector.IsAyaSpeaking
                ? Mathf.Max(1, _maxBotsPerBurst / 2) // Half as many bots
                : _maxBotsPerBurst;

            float lullDuration = UnityEngine.Random.Range(lullMin, lullMax);
            await Awaitable.WaitForSecondsAsync(lullDuration, destroyCancellationToken);
            // ... rest of burst logic with maxBots cap
        }
    }
    catch (OperationCanceledException) { }
}
```

### Pattern 3: Finish-First PTT State Machine
**What:** When the user presses push-to-talk while Aya is speaking, a visual acknowledgment appears immediately (<500ms) but Aya finishes her current response before the user's audio is processed. After release, the transcript appears as a bottom overlay with a 3-second auto-submit countdown.
**When to use:** Always for user push-to-talk interaction.
**State machine:**
```
                     [User presses SPACE]
Idle ─────────────────────────────────────> Recording
  ^                                            |
  |                                            | [User releases SPACE]
  |                                            v
  |  [Cancel pressed]                    Reviewing (transcript overlay)
  |<──────────────────────────────────────     |
  |                                            | [Enter pressed OR 3s auto-submit]
  |                                            v
  |  [Aya turn complete]                  Submitted (waiting for Aya)
  |<──────────────────────────────────────
```
**Example:**
```csharp
// Source: designed from QueuedResponseController pattern + CONTEXT.md decisions

public class PushToTalkController : MonoBehaviour
{
    private enum PTTState { Idle, Recording, Reviewing, Submitted }
    private PTTState _state = PTTState.Idle;
    private string _transcript;
    private float _autoSubmitTimer;
    private const float AutoSubmitDelay = 3f;

    // When user starts speaking while Aya is talking:
    // 1. Show "Aya noticed you" indicator immediately (<500ms)
    // 2. Aya continues speaking (finish-first)
    // 3. After Aya's turn completes, the transcript is submitted

    // Visual acknowledgment: USS class toggle (instant, no async)
    private void ShowAcknowledgment()
    {
        _livestreamUI.ShowPTTAcknowledgment(true); // Adds CSS class
    }
}
```

### Pattern 4: SendText Director Notes
**What:** PersonaSession.SendText sends a `clientContent` message with `turnComplete=true`, which Gemini treats as a user message and responds to. Director notes are phrased as invisible context that steers Aya's next response topic.
**When to use:** At beat transitions, when Aya checks chat, when user skip is detected.
**Critical insight:** SendText triggers a Gemini response (it sets `turnComplete=true`). This means director notes will cause Aya to speak. This is actually desirable -- the director note steers what Aya says next. However, you must not send director notes while Aya is already speaking (it would cause an interruption).
**Example:**
```csharp
// Source: GeminiLiveClient.SendText verified in codebase (line 129-148)
// Director note format: context + instruction
string directorNote =
    "[Director: You've been chatting for a few minutes now. " +
    "Naturally transition to talking about your art process -- " +
    "what tools you use, your creative routine, what inspires you today.]";
_session.SendText(directorNote);
// Gemini responds with Aya speaking about her art process
```

### Pattern 5: Scene Type Execution
**What:** Each scene type maps to a specific execution behavior within its queue.
**Scene types and queue assignment:**
```
Aya Queue (sequential):
  AyaDialogue  -- SendText with scripted dialogue context -> Aya speaks
  AyaChecksChat -- Gather unresponded TrackedChatMessages -> SendText -> Aya responds
  AyaAction    -- Fire function call or animation trigger (Phase 15)

Chat Queue (parallel):
  ChatBurst    -- ChatBotManager burst with configured timing
  UserChoice   -- Show choice UI, wait for selection (not in Phase 14 per CONTEXT.md)
```

### Anti-Patterns to Avoid
- **GoalManager for mid-session steering:** Goals only apply at next Connect() (PersonaSession line 801-805). Use SendText instead.
- **Sending director notes while Aya is speaking:** This triggers an interruption event, clears audio buffers, and causes jarring UX. Always wait for OnTurnComplete or OnAISpeakingStopped.
- **Direct microphone stream during PTT review:** The user's speech goes to Gemini via realtimeInput audio. StopListening must be called before the review state, not after.
- **Blocking the main thread in beat transitions:** All timing must use Awaitable, never Thread.Sleep or synchronous waits.
- **Modifying ChatBotManager._running from NarrativeDirector:** Use event-based coordination (IsAyaSpeaking, OnBeatTransition) not direct field manipulation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Beat timing | Custom timer class | Awaitable.WaitForSecondsAsync + destroyCancellationToken | Proven pattern from ChatBotManager; auto-cancels on destroy |
| Audio state tracking | Custom audio monitoring | PersonaSession.OnAISpeakingStarted/Stopped events | Already implemented in PersonaSession; reliable main-thread events |
| User speech transcription | Custom STT | PersonaSession.OnInputTranscription | Gemini Live API provides built-in STT; transcription arrives via GeminiLiveClient events |
| Chat message tracking | Custom message log | ChatBotManager.GetUnrespondedMessages() + TrackedChatMessage | Built in Phase 13; tracks AyaHasResponded and PostedAtTime |
| Auto-scroll chat | Custom scroll logic | LivestreamUI already does schedule.Execute ScrollToItem | Built in Phase 12; reliable deferred scroll pattern |
| ScriptableObject Inspector editing | Custom editor window | Default Inspector + ScriptableObject fields with Header/Tooltip | 3 beats with simple fields; default Inspector is sufficient |

**Key insight:** The existing PersonaSession event system provides everything needed for the finish-first state machine. OnAISpeakingStarted, OnAISpeakingStopped, OnTurnComplete, OnUserSpeakingStarted, OnUserSpeakingStopped, and OnInputTranscription cover all state transitions.

## Common Pitfalls

### Pitfall 1: GoalManager Does Not Work Mid-Session
**What goes wrong:** Calling PersonaSession.AddGoal/ReprioritizeGoal expects to update system instructions, but the Gemini Live API does not support mid-session system instruction updates.
**Why it happens:** PersonaSession.SendGoalUpdate() (line 796-805) explicitly logs this limitation: "Mid-session system instruction updates are not supported by the Gemini Live API."
**How to avoid:** Use SendText for all mid-session steering. Goals are only useful for initial system prompt setup at Connect() time. The NarrativeDirector should set initial goals before Connect(), then use SendText for beat transitions.
**Warning signs:** Debug.Log messages about "Goals will take effect on next connection."

### Pitfall 2: SendText Triggers Aya Response (Not Silent)
**What goes wrong:** Developer sends a "silent" director note expecting it to update context without Aya speaking, but Aya immediately responds.
**Why it happens:** GeminiLiveClient.SendText sends `clientContent` with `turnComplete=true` (line 136-148). This tells Gemini "the user has finished speaking, generate a response." There is no "silent inject" mechanism in the Live API.
**How to avoid:** Accept that director notes trigger Aya responses. Frame director notes so the response is the desired beat transition dialogue. This is actually the intended pattern: "naturally transition to talking about X" causes Aya to naturally transition.
**Warning signs:** Director notes sent at unexpected times causing Aya to interrupt herself.

### Pitfall 3: Sending Director Notes While Aya Is Speaking
**What goes wrong:** A beat timer expires while Aya is mid-response, NarrativeDirector sends a SendText, and Gemini's VAD detects it as user input, triggering an interruption event that clears Aya's audio buffer.
**Why it happens:** The Gemini Live API treats ALL clientContent as potential interruption triggers. Even text-only messages.
**How to avoid:** NarrativeDirector MUST wait for OnTurnComplete before sending any director note. If a beat timer expires while Aya is speaking, queue the transition for after the current turn completes.
**Warning signs:** Frequent OnInterrupted events during beat transitions; Aya cutting herself off mid-sentence.

### Pitfall 4: PTT Acknowledgment Timing (500ms Requirement)
**What goes wrong:** Acknowledgment appears too late because it waits for an async operation or round-trip.
**Why it happens:** Subscribing to OnUserSpeakingStarted is sufficient for detection, but if the visual update requires layout/style recalculation, it may lag.
**How to avoid:** The acknowledgment is a pure CSS class toggle on an existing VisualElement. This is synchronous and frame-immediate. Subscribe to OnUserSpeakingStarted, check if Aya is speaking, and toggle the class. No async, no round-trip, no layout computation.
**Warning signs:** Acknowledgment appearing after user has already finished speaking.

### Pitfall 5: Race Condition Between PTT and Aya Turn Complete
**What goes wrong:** User presses PTT, the PushToTalkController queues for "after Aya finishes," but Aya's TurnComplete fires between the PTT press and the subscription.
**Why it happens:** Events are processed synchronously in Update() via ProcessEvents(), so there is no true race condition. However, if the PTT press happens in the same frame as TurnComplete processing, the order depends on Update execution order.
**How to avoid:** Always check the current state. When PTT is pressed, check if `_aiSpeaking` is true. If not, Aya has already finished -- skip the "finishing" state and go directly to Recording.
**Warning signs:** PTT controller stuck in "AyaFinishing" state with Aya already silent.

### Pitfall 6: Auto-Submit Timer Conflicting with Manual Actions
**What goes wrong:** User presses Enter to submit while the auto-submit timer is also executing, causing double submission.
**Why it happens:** Two code paths (Enter key handler and timer callback) both call the submit function.
**How to avoid:** Submit function should be idempotent -- check if already in Submitted state before acting. Or cancel the timer when Enter is pressed.
**Warning signs:** Duplicate messages appearing in Aya's context; Aya responding twice to the same input.

### Pitfall 7: ChatBotManager Burst Timing During Beat Transitions
**What goes wrong:** A chat burst starts just as a beat boundary is reached, and the burst messages reference context from the old beat while Aya is transitioning to the new beat.
**Why it happens:** The burst loop runs independently with random timing. Beat transitions happen asynchronously.
**How to avoid:** At beat boundaries, NarrativeDirector fires OnBeatTransition. ChatBotManager subscribes and temporarily pauses the burst loop (set a flag that the next lull period checks). Resume after the director note response completes.
**Warning signs:** Chat messages that feel thematically disconnected from what Aya is currently discussing.

### Pitfall 8: Context Window Growth from Director Notes
**What goes wrong:** Each SendText adds to the conversation history. Over a 10-minute session with 3+ beat transitions and multiple AyaChecksChat scenes, the context window fills up.
**Why it happens:** The Gemini Live API has a 15-minute audio session limit. Text messages accumulate in the conversation context alongside audio transcriptions.
**How to avoid:** Keep director notes concise (1-3 sentences). When building AyaChecksChat messages, summarize multiple bot messages rather than injecting each individually. Monitor is deferred to Phase 16 per STATE.md blockers, but be aware now.
**Warning signs:** Aya's responses becoming less coherent or slower toward the end of the session.

## Code Examples

### NarrativeBeatConfig ScriptableObject
```csharp
// Source: designed from nevatars NarrativeBeat (simplified per CONTEXT.md "inspiration not migration")
[CreateAssetMenu(fileName = "Beat_", menuName = "AI Embodiment/Samples/Narrative Beat")]
public class NarrativeBeatConfig : ScriptableObject
{
    [Header("Beat Info")]
    public string beatId = "BEAT_1";
    public string title = "Beat Title";

    [Header("Timing")]
    [Tooltip("Time budget in seconds. Beat advances when goal is met or time expires.")]
    public float timeBudgetSeconds = 180f;

    [Header("Goal")]
    [Tooltip("What Aya should accomplish in this beat. Used for goal-met detection.")]
    [TextArea(2, 4)]
    public string goalDescription = "";

    [Header("Director Note")]
    [Tooltip("Text sent to Gemini via SendText at beat start to steer Aya's conversation.")]
    [TextArea(3, 6)]
    public string directorNote = "";

    [Header("Scenes")]
    [Tooltip("Ordered list of scenes in this beat (Aya queue scenes only; chat runs in parallel)")]
    public NarrativeSceneConfig[] scenes;

    [Header("Chat Pacing")]
    [Tooltip("Whether to slow down chat bursts during this beat")]
    public bool slowChatDuringAya = true;

    [Header("Skip Detection")]
    [Tooltip("Keywords that trigger skip to final beat if user says them")]
    public string[] skipKeywords;
}
```

### NarrativeSceneConfig (Simplified from Nevatars)
```csharp
// Source: simplified from nevatars NarrativeScene (removed Login, pre-recorded audio, exit patterns,
// alternate consequences, repeat limits, suggested questions, poll options, option pairs)
[Serializable]
public class NarrativeSceneConfig
{
    public string sceneId = "SCENE_ID";
    public SceneType type = SceneType.AyaDialogue;

    [TextArea(1, 2)]
    public string description = "";

    [Header("AyaDialogue")]
    [TextArea(2, 4)]
    public string[] dialogueAlternatives;

    [Header("AyaChecksChat")]
    public int maxResponsesToGenerate = 1;

    [Header("AyaAction (Phase 15)")]
    public string actionDescription = "";

    [Header("Conditional Transition")]
    public bool isConditional = false;
    public ConditionType conditionType = ConditionType.TimedOut;
    public float maxDuration = 60f;
}

public enum SceneType
{
    AyaDialogue,   // Aya speaks (SendText with dialogue context)
    ChatBurst,     // Chat bots post messages (parallel queue)
    AyaChecksChat, // Aya responds to bot/user messages
    AyaAction,     // Animation/function call (Phase 15)
    UserChoice     // User picks option (simplified from nevatars)
}

public enum ConditionType
{
    TimedOut,           // Wait for maxDuration seconds
    QuestionsAnswered,  // Aya has answered N questions
    Always              // Immediately after previous scene
}
```

### SendText Director Note at Beat Transition
```csharp
// Source: PersonaSession.SendText (verified in codebase) + CONTEXT.md dual steering
private void TransitionToBeat(NarrativeBeatConfig beat)
{
    // Only send when Aya is NOT speaking (Pitfall 3)
    if (_isAyaSpeaking)
    {
        _pendingBeatTransition = beat;
        return; // Will execute on next OnTurnComplete
    }

    _currentBeat = beat;

    // Director note triggers Aya's next response on the new topic
    if (!string.IsNullOrEmpty(beat.directorNote))
    {
        _session.SendText(beat.directorNote);
    }

    OnBeatStarted?.Invoke(beat);
}
```

### AyaChecksChat Scene Execution
```csharp
// Source: ChatBotManager.GetUnrespondedMessages() + PersonaSession.SendText
private void ExecuteAyaChecksChat(NarrativeSceneConfig scene)
{
    var unresponded = _chatBotManager.GetUnrespondedMessages();
    if (unresponded.Count == 0) return;

    // User messages get priority over bot messages (per CONTEXT.md)
    var userMessages = unresponded.Where(m => m.Message.IsFromUser).ToList();
    var botMessages = unresponded.Where(m => !m.Message.IsFromUser).ToList();

    var toAddress = userMessages.Any() ? userMessages : botMessages;
    int count = Mathf.Min(toAddress.Count, scene.maxResponsesToGenerate);

    // Build a summary rather than injecting each message (Pitfall 8: context window)
    var summary = new StringBuilder();
    summary.AppendLine("[Director: Your chat audience has been active. Here are messages to respond to:]");
    for (int i = 0; i < count; i++)
    {
        var msg = toAddress[i];
        summary.AppendLine($"- {msg.Message.BotName}: \"{msg.Message.Text}\"");
        msg.AyaHasResponded = true; // Mark as addressed
    }
    summary.AppendLine("[Respond naturally to one or more of these, then continue your current topic.]");

    _session.SendText(summary.ToString());
}
```

### PTT Finish-First Controller
```csharp
// Source: designed from QueuedResponseController + CONTEXT.md finish-first decisions
private void HandlePTTPressed()
{
    if (_state != PTTState.Idle) return;

    // Show acknowledgment immediately if Aya is speaking (USR-02: <500ms)
    if (_narrativeDirector.IsAyaSpeaking)
    {
        _livestreamUI.ShowPTTAcknowledgment(true);
    }

    _state = PTTState.Recording;
    _session.StartListening();
    _livestreamUI.SetPTTStatus("Recording...", active: true);

    // Only Aya pauses during PTT; chat keeps flowing (per CONTEXT.md)
    // No need to pause ChatBotManager -- it runs on the chat queue
}

private void HandlePTTReleased()
{
    if (_state != PTTState.Recording) return;

    _session.StopListening(); // Sends audioStreamEnd, triggers Gemini STT
    _state = PTTState.Reviewing;
    _autoSubmitTimer = AutoSubmitDelay;

    // Transcript appears as bottom overlay (slides up over chat feed)
    _livestreamUI.ShowTranscriptOverlay(true);
    _livestreamUI.SetPTTStatus("Review your message", active: false);
}

private void UpdateReviewState()
{
    // Auto-submit countdown
    _autoSubmitTimer -= Time.deltaTime;
    _livestreamUI.UpdateAutoSubmitProgress(_autoSubmitTimer / AutoSubmitDelay);

    if (_autoSubmitTimer <= 0f)
    {
        SubmitTranscript();
        return;
    }

    // Manual submit
    if (Keyboard.current.enterKey.wasPressedThisFrame)
    {
        SubmitTranscript();
        return;
    }

    // Cancel (silent discard, no feedback animation per CONTEXT.md)
    if (Keyboard.current.escapeKey.wasPressedThisFrame)
    {
        _state = PTTState.Idle;
        _livestreamUI.ShowTranscriptOverlay(false);
        _livestreamUI.ShowPTTAcknowledgment(false);
        return;
    }
}
```

### Chat Pacing During Aya Speech
```csharp
// Source: ChatBotManager.ScriptedBurstLoop (existing) + NarrativeDirector coordination
// In ChatBotManager, add a reference to NarrativeDirector
[SerializeField] private NarrativeDirector _narrativeDirector;

// Modify burst timing based on Aya speaking state
private float GetBurstLullDuration()
{
    bool ayaSpeaking = _narrativeDirector != null && _narrativeDirector.IsAyaSpeaking;
    float min = ayaSpeaking ? _burstIntervalMin * 2f : _burstIntervalMin;
    float max = ayaSpeaking ? _burstIntervalMax * 1.5f : _burstIntervalMax;
    return UnityEngine.Random.Range(min, max);
}

private int GetMaxBotsForBurst()
{
    bool ayaSpeaking = _narrativeDirector != null && _narrativeDirector.IsAyaSpeaking;
    return ayaSpeaking ? Mathf.Max(1, _maxBotsPerBurst / 2) : _maxBotsPerBurst;
}
```

## Nevatars Beat/Scene Data: What to Extract as Inspiration

### Nevatars NarrativeBeat Structure (Source)
The nevatars project has 11 NarrativeBeat .asset files organized in 3 acts (Intro/3, Mid/3, Outro/3, plus 2 test beats). Each NarrativeBeat is a ScriptableObject with:
- `beatId`, `title` -- identification
- `scenes` -- List<NarrativeScene> with per-scene type, dialogue alternatives, chat messages, conditional logic
- `exitPatterns` -- ResponsePattern references for background exit monitoring
- `exitConsequenceType` -- what happens when exit triggers
- `exitBotQuestion` -- rescue mechanism for stalled narratives
- `suggestedQuestions` -- tappable question bubbles

### What to Keep (Inspiration)
| Nevatars Concept | AI-Embodiment Equivalent | Simplification |
|------------------|--------------------------|----------------|
| beatId + title | beatId + title | Same |
| List<NarrativeScene> | NarrativeSceneConfig[] | Array instead of List; fewer fields per scene |
| SceneType enum (6 values) | SceneType enum (5 values) | Drop Login type |
| ConditionType enum (8 values) | ConditionType enum (3 values) | Keep TimedOut, QuestionsAnswered, Always; drop rest |
| ConsequenceType enum (7 values) | Not needed | Beats are linear (1->2->3); no jumping needed for 3-beat demo |
| dialogueAlts | dialogueAlternatives | Same concept, simpler name |
| exitPatterns + exitBotQuestion | skipKeywords on beat | Simple keyword match replaces complex pattern system |
| Dual-queue color coding (cyan/yellow) | Not needed in editor | Nice but unnecessary complexity for 3 beats |

### What to Drop (Over-Engineering for Demo)
- Exit patterns with ResponsePattern references (complex pattern matching system)
- Alternate consequences for PatternMatched conditions
- Repeat limits (maxRepeatCount)
- User choice with option pairs and poll options
- Pre-recorded audio per scene
- Token limit conditions
- Bot question rescue mechanism
- Suggested question bubbles

## State of the Art

| Old Approach (nevatars) | Current Approach (AI-Embodiment) | Impact |
|-------------------------|----------------------------------|--------|
| Complex condition/consequence system | Simple time+goal with SendText steering | Dramatically simpler; 3 condition types vs 8 |
| 14 characters, per-scene chat messages | 6 bots with burst loop, parallel queue | ChatBotManager handles all chat; no per-scene message authoring |
| Pattern matching for Aya responses | Gemini generates responses from context | No need for curated response patterns |
| Pre-recorded audio for intro/outro | All Gemini Live generated | No audio file management |
| Custom PropertyDrawer for scenes | Default Inspector | Sufficient for 3 beats |
| Coroutines (IEnumerator) | Async Awaitable | Unity 6 standard; better cancellation |

## Open Questions

1. **SendText director note format reliability**
   - What we know: SendText sends `clientContent` with `turnComplete=true`, which triggers Gemini to respond. The "director note" is treated as a user message.
   - What's unclear: Whether Gemini consistently follows steering instructions phrased as [Director: ...] tags vs natural language requests. This was flagged in STATE.md as needing early validation.
   - Recommendation: Phase 14-02 should include an early validation task that tests 3 different director note formats and verifies Aya transitions topics. Format options: (a) [Director: ...] bracketed, (b) "As a livestreamer, you should now talk about...", (c) Context-setting like "Your viewers have been asking about your art process. What do you think?"

2. **Beat skip detection accuracy**
   - What we know: CONTEXT.md says user can trigger skip to final reveal if they say something "directly on-point."
   - What's unclear: Simple keyword matching may be too brittle or too broad. Gemini STT transcription quality varies.
   - Recommendation: Per CONTEXT.md Claude's discretion, use keyword matching as the initial approach (check OnInputTranscription for skipKeywords). If too brittle, Phase 16 can add semantic analysis.

3. **Exact chat slowdown multipliers**
   - What we know: Chat should slow down when Aya talks (per CONTEXT.md). Need longer delays, fewer bots.
   - What's unclear: What multipliers feel natural (2x delay? 3x? Half the bots? One bot?).
   - Recommendation: Per CONTEXT.md Claude's discretion, start with 2x min delay, 1.5x max delay, half bot count. Expose as serialized fields for producer tuning.

4. **Audio session duration with director notes**
   - What we know: Gemini Live audio sessions have a 15-minute limit (can be extended with context window compression). Director notes add to context.
   - What's unclear: Whether 3 beat transitions + multiple AyaChecksChat scenes will hit the context window before the 10-minute target.
   - Recommendation: Keep director notes concise. Defer context window monitoring to Phase 16 per STATE.md.

## Sources

### Primary (HIGH confidence)
- PersonaSession.cs (local codebase) -- SendText implementation, event system, GoalManager limitation (line 801-805)
- GeminiLiveClient.cs (local codebase) -- SendText payload format, clientContent with turnComplete=true (lines 129-148)
- ChatBotManager.cs (local codebase) -- Burst loop pattern, TrackedChatMessage, dynamic response flow
- LivestreamUI.cs (local codebase) -- Existing UI elements, AddMessage, UpdateAyaTranscript, SetPTTStatus
- QueuedResponseController.cs (local codebase) -- Finish-first state machine reference pattern
- nevatars NarrativeBeat.cs (sibling project) -- Full data model for beat/scene structure
- nevatars NarrativeSceneDrawer.cs (sibling project) -- Dual-queue color coding (Chat=cyan, Aya=yellow)
- nevatars Beats/*.asset (sibling project) -- 11 beat assets showing real scene configurations

### Secondary (MEDIUM confidence)
- [Gemini Live API Reference](https://ai.google.dev/api/live) -- clientContent field documentation, turnComplete behavior
- [Gemini Live API Capabilities Guide](https://ai.google.dev/gemini-api/docs/live-guide) -- Text input mid-session, VAD interruption handling
- [Gemini Live Session Management](https://ai.google.dev/gemini-api/docs/live-session) -- 15-minute session limit, context window compression

### Tertiary (LOW confidence)
- SendText "director note" effectiveness -- untested pattern; needs early validation per STATE.md concern

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries are already in use in the codebase
- Architecture patterns: HIGH -- dual-queue design directly from nevatars + existing ChatBotManager pattern
- Data model: HIGH -- nevatars source code provides complete NarrativeBeat/NarrativeScene reference for simplification
- SendText steering: MEDIUM -- mechanism is verified (codebase + API docs) but effectiveness for "director notes" needs runtime validation
- PTT finish-first: HIGH -- QueuedResponseController provides a working reference; PersonaSession events cover all transitions
- Pitfalls: HIGH -- verified against codebase (GoalManager limitation, SendText behavior, interruption handling)

**Research date:** 2026-02-17
**Valid until:** 2026-03-17 (stable domain; Unity 6 APIs and Gemini Live API unlikely to change)
