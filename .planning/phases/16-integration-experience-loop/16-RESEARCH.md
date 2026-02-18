# Phase 16: Integration & Experience Loop - Research

**Researched:** 2026-02-17
**Domain:** Unity MonoBehaviour orchestration, cross-system context injection, shared fact tracking, narrative catalyst tuning, dead air detection, async initialization patterns
**Confidence:** HIGH

## Summary

Phase 16 is a pure integration phase -- no new frameworks, no new external dependencies. Every subsystem already exists and has been validated in Phases 12-15. The work is wiring subsystems together through a new LivestreamController MonoBehaviour, adding cross-system context flows (bot-to-Aya already exists, Aya-to-bot needs new prompt context), building a shared fact tracker for coherence, enriching beat configs with catalyst messages, and validating the full 10-minute experience.

The existing codebase provides all the building blocks. AyaSampleController (Phase 12) demonstrates the pattern of connecting PersonaSession and registering functions. NarrativeDirector (Phase 14) drives the beat loop and scene execution. ChatBotManager (Phase 13) runs scripted bursts and dynamic Gemini responses. PushToTalkController (Phase 14) handles user input with finish-first priority. SceneTransitionHandler (Phase 15) listens for OnAllBeatsComplete and loads the movie scene. LivestreamUI (Phase 12) provides all UI elements. Each subsystem has explicit [SerializeField] references and clean event subscription/unsubscription patterns.

The primary risk is ordering -- subsystems must initialize in the right sequence, and the LivestreamController must wait for PersonaSession to reach `SessionState.Connected` before starting the narrative. The secondary risk is context window growth over a 10-minute session with bot message injection via SendText director notes. The tertiary risk is dead air if Gemini response latency exceeds user patience.

**Primary recommendation:** Build LivestreamController as a thin orchestrator that holds [SerializeField] references to all subsystems, initializes them in parallel (PersonaSession.Connect + GeminiTextClient warmup), waits for connection, then kicks off NarrativeDirector.StartNarrative + ChatBotManager.StartBursts in sequence. The controller replaces AyaSampleController as the scene's root controller. Cross-system context injection uses existing patterns (SendText for bot-to-Aya, Gemini REST prompt enrichment for Aya-to-bot). The fact tracker is a simple Dictionary<string, bool> queried by subsystems.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| PersonaSession | local package | WebSocket session lifecycle, events, SendText | Core orchestration hub; all subsystems already depend on it |
| NarrativeDirector | Phase 14 | Beat loop, scene execution, SendText steering | Proven in Phase 14; exposes OnBeatStarted/Ended/Transition/AllBeatsComplete |
| ChatBotManager | Phase 13 | Scripted bursts, dynamic Gemini responses | Proven in Phase 13; exposes StartBursts/StopBursts, GetUnrespondedMessages |
| PushToTalkController | Phase 14 | User speech input with finish-first pattern | Proven in Phase 14; self-contained state machine |
| SceneTransitionHandler | Phase 15 | Clean scene exit to movie clip | Proven in Phase 15; listens to OnAllBeatsComplete |
| LivestreamUI | Phase 12 | Chat feed, transcript, PTT status, toast | Proven in Phase 12; all UI methods already exist |
| GeminiTextClient | Phase 12 | REST structured output for dynamic bot responses | Used by ChatBotManager; will also enrich prompts with Aya context |
| Unity 6 Awaitable | 6000.x | Async initialization, dead air detection | Consistent pattern across all existing subsystems |
| Unity 6 ScriptableObject | 6000.x | NarrativeBeatConfig catalyst fields | Extend existing ScriptableObject; Inspector-editable |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| destroyCancellationToken | Unity 6 | Clean async cancellation on scene unload | All async loops in LivestreamController |
| SessionState enum | local package | Connection state tracking | LivestreamController waits for Connected state |
| AnimationConfig | Phase 15 | Animation function registration | LivestreamController registers functions before Connect |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Dictionary<string,bool> fact tracker | Custom class with typed properties | Dictionary is simpler, extensible, serializable; typed class adds compile-time safety but more code for same effect |
| Polling for SessionState.Connected | Subscribing to OnStateChanged event | Event-based is cleaner but polling with Awaitable is simpler and matches the existing initialization pattern in AyaSampleController |
| LivestreamController as new class | Modifying AyaSampleController | New class avoids breaking existing sample scene; AyaSampleController can remain for the simpler demo |

## Architecture Patterns

### Recommended Project Structure
```
Assets/AyaLiveStream/
  LivestreamController.cs       # NEW -- top-level orchestrator replacing AyaSampleController for livestream
  FactTracker.cs                # NEW -- shared fact dictionary for cross-system coherence
  NarrativeDirector.cs          # MODIFIED -- add catalystGoal support, dead air detection hook
  NarrativeBeatConfig.cs        # MODIFIED -- add catalystGoal string field
  ChatBotManager.cs             # MODIFIED -- accept Aya transcript context for dynamic prompts, catalyst message support, dead air callback
  LivestreamUI.cs               # MODIFIED -- add "thinking" indicator, loading state
  AyaSampleController.cs        # UNCHANGED -- remains for simpler demo scene
  Data/
    Beat_WarmUp.asset           # MODIFIED -- add catalystGoal, catalyst scripted messages
    Beat_ArtProcess.asset       # MODIFIED -- add catalystGoal, catalyst scripted messages
    Beat_Characters.asset       # MODIFIED -- add catalystGoal, catalyst scripted messages
```

### Pattern 1: Parallel Initialization with Readiness Gate
**What:** LivestreamController starts subsystem initialization in parallel (PersonaSession.Connect is async, GeminiTextClient creation is sync). It then polls for readiness (SessionState.Connected) before starting the experience. If connection fails after a timeout, the controller degrades gracefully.
**When to use:** Always -- this is the startup sequence.
**Example:**
```csharp
// Source: derived from AyaSampleController.PlayIntroThenGoLive pattern
// (Assets/AyaLiveStream/AyaSampleController.cs lines 74-94)
private async Awaitable InitializeAndStart()
{
    try
    {
        // Show loading state
        _livestreamUI.SetLoadingState(true);

        // Register functions before Connect (required by PersonaSession)
        RegisterFunctions();

        // Start connection (async, returns immediately)
        _session.Connect();

        // Wait for connection with timeout
        float elapsed = 0f;
        while (_session.State != SessionState.Connected && elapsed < ConnectionTimeoutSeconds)
        {
            if (_session.State == SessionState.Error)
            {
                Debug.LogError("[LivestreamController] Session connection failed.");
                _livestreamUI.SetLoadingState(false);
                return;
            }
            await Awaitable.WaitForSecondsAsync(0.1f, destroyCancellationToken);
            elapsed += 0.1f;
        }

        if (_session.State != SessionState.Connected)
        {
            Debug.LogError("[LivestreamController] Connection timed out.");
            return;
        }

        // "Going live" transition moment
        _livestreamUI.SetLoadingState(false);
        _livestreamUI.ShowGoingLive();

        // Brief pause for the "going live" visual to register
        await Awaitable.WaitForSecondsAsync(1.5f, destroyCancellationToken);

        // Start subsystems in order
        _chatBotManager.StartBursts();
        _narrativeDirector.StartNarrative();
    }
    catch (OperationCanceledException) { }
}
```

### Pattern 2: Cross-System Context Injection (Bot-to-Aya)
**What:** Already implemented via NarrativeDirector.ExecuteAyaChecksChat. The director gathers unresponded messages from ChatBotManager, builds a StringBuilder summary, and sends via PersonaSession.SendText as a director note. No new code needed for this direction.
**When to use:** Bot-to-Aya direction is already working.
**Key code path:** NarrativeDirector.ExecuteAyaChecksChat (line 344-388 of NarrativeDirector.cs)

### Pattern 3: Cross-System Context Injection (Aya-to-Bot)
**What:** When generating dynamic bot responses to user PTT, include the last 2-3 Aya transcript turns in the Gemini REST prompt so bots can reference what Aya actually said. This requires LivestreamController (or a transcript buffer) to accumulate Aya's output transcription and pass it to ChatBotManager.
**When to use:** Every time ChatBotManager.BuildDynamicPrompt is called.
**Example:**
```csharp
// Source: extends ChatBotManager.BuildDynamicPrompt
// (Assets/AyaLiveStream/ChatBotManager.cs lines 472-495)
private string BuildDynamicPrompt(string userTranscript)
{
    var sb = new StringBuilder();
    sb.AppendLine("You are generating chat bot reactions for a livestream.");
    sb.AppendLine("The viewer just said via push-to-talk:");
    sb.AppendLine($"\"{userTranscript}\"");
    sb.AppendLine();

    // NEW: Include Aya's recent transcript for context
    string ayaContext = _ayaTranscriptBuffer?.GetRecentTurns(3);
    if (!string.IsNullOrEmpty(ayaContext))
    {
        sb.AppendLine("Aya (the host) has been saying:");
        sb.AppendLine(ayaContext);
        sb.AppendLine();
    }

    // NEW: Include established facts to prevent contradictions
    if (_factTracker != null)
    {
        string facts = _factTracker.GetFactsSummary();
        if (!string.IsNullOrEmpty(facts))
        {
            sb.AppendLine("Established facts (do NOT contradict these):");
            sb.AppendLine(facts);
            sb.AppendLine();
        }
    }

    sb.AppendLine("Available chat bots...");
    // ... rest of existing prompt
    return sb.ToString();
}
```

### Pattern 4: Shared Fact Tracker
**What:** A simple central object that tracks what has been established in the experience. Subsystems add facts when they become true, and query facts to avoid contradictions. Facts are strings keyed by ID with boolean values.
**When to use:** When bot prompts need to know what Aya has/hasn't mentioned, and when NarrativeDirector needs to know if the user has been told about certain topics.
**Example:**
```csharp
// Source: new class, design per CONTEXT.md "Shared fact tracker" decision
public class FactTracker
{
    private readonly Dictionary<string, bool> _facts = new();

    /// <summary>Records a fact as established.</summary>
    public void SetFact(string factId, bool value = true)
    {
        _facts[factId] = value;
        Debug.Log($"[FactTracker] {factId} = {value}");
    }

    /// <summary>Checks if a fact has been established.</summary>
    public bool HasFact(string factId)
    {
        return _facts.TryGetValue(factId, out bool value) && value;
    }

    /// <summary>Returns a summary string for prompt injection.</summary>
    public string GetFactsSummary()
    {
        var sb = new StringBuilder();
        foreach (var kvp in _facts)
        {
            if (kvp.Value)
                sb.AppendLine($"- {kvp.Key}");
        }
        return sb.ToString();
    }
}
```

### Pattern 5: Aya Transcript Buffer
**What:** A ring buffer of Aya's recent transcript turns, populated by subscribing to PersonaSession.OnOutputTranscription and OnTurnComplete. Provides GetRecentTurns(n) for prompt enrichment.
**When to use:** Whenever ChatBotManager needs to know what Aya said.
**Example:**
```csharp
// Source: new class for Aya-to-bot context flow
public class AyaTranscriptBuffer
{
    private readonly List<string> _turns = new();
    private StringBuilder _currentTurn = new();
    private readonly int _maxTurns;

    public AyaTranscriptBuffer(int maxTurns = 5) { _maxTurns = maxTurns; }

    public void AppendText(string text) => _currentTurn.Append(text);

    public void CompleteTurn()
    {
        if (_currentTurn.Length > 0)
        {
            _turns.Add(_currentTurn.ToString());
            _currentTurn.Clear();
            if (_turns.Count > _maxTurns) _turns.RemoveAt(0);
        }
    }

    public string GetRecentTurns(int count)
    {
        int start = Mathf.Max(0, _turns.Count - count);
        var sb = new StringBuilder();
        for (int i = start; i < _turns.Count; i++)
            sb.AppendLine($"- \"{_turns[i]}\"");
        return sb.ToString();
    }
}
```

### Pattern 6: Catalyst Message Selection
**What:** Each NarrativeBeatConfig has a catalystGoal string (e.g., "Get Aya to talk about the character she drew"). ChatBotManager uses this to bias scripted message selection toward catalyst messages that push the narrative forward. Catalyst messages are tagged in the message pool or as a separate array on the beat config.
**When to use:** During scripted burst message selection when a narrative beat is active.
**Example approach:** Add a `catalystMessages` string array to NarrativeBeatConfig alongside the existing `catalystGoal`. During PickMessage, if the current beat has catalyst messages, occasionally (e.g., 30% of the time) pick from the catalyst pool instead of the bot's own message pool. This keeps catalysts sprinkled organically.

### Pattern 7: Dead Air Detection
**What:** If neither Aya nor bots have produced output for a configurable silence threshold (e.g., 8-12 seconds), trigger a bot chat burst to re-engage. This prevents awkward silence during Gemini latency spikes.
**When to use:** Continuously during the experience, as a background timer in LivestreamController or NarrativeDirector.
**Example approach:** Track `Time.time` of the last meaningful output (Aya speech or bot message). In an async polling loop, check if the gap exceeds the threshold. If so, trigger a burst from ChatBotManager and optionally show the "Aya is thinking..." indicator.

### Pattern 8: User PTT Skip-Ahead
**What:** Already partially implemented in NarrativeDirector.CheckSkipKeywords. The existing code checks user transcript against skipKeywords and advances to the final beat. Phase 16 extends this: if the user asks about a topic relevant to a FUTURE beat (not just the final one), the director jumps to that specific beat.
**When to use:** During user PTT speech processing.
**Approach:** For each beat, define topicKeywords (not just skip keywords). When user speaks, check all future beats' topicKeywords. If a match is found, set _currentBeatIndex to that beat's index - 1 and mark goalMet.

### Anti-Patterns to Avoid
- **Direct field manipulation across subsystems:** Never set `_running` on ChatBotManager from LivestreamController. Use public methods (StartBursts/StopBursts) and events only. Already enforced by existing architecture.
- **SendText during Aya speaking:** Never send director notes while IsAyaSpeaking is true. Queue for OnTurnComplete. Already enforced in NarrativeDirector.
- **Unbounded context injection:** Don't inject all bot messages into Aya's context. Use the existing StringBuilder summary pattern with message limits (Pitfall 8 from Phase 14 research).
- **GoalManager for mid-session steering:** Goals only apply at next Connect(). Use SendText for mid-session. Already enforced and documented.
- **Runtime discovery (GetComponent/FindObjectOfType):** Per CONTEXT.md, LivestreamController uses explicit [SerializeField] references. No runtime discovery.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bot-to-Aya context injection | Custom SendText injection system | NarrativeDirector.ExecuteAyaChecksChat (Phase 14) | Already implemented with user message priority, response tracking, and context window management |
| Aya speaking state tracking | Custom audio monitoring | NarrativeDirector.IsAyaSpeaking (subscribes to PersonaSession events) | Already tracks OnAISpeakingStarted/Stopped reliably |
| Beat progression | Custom timer/state machine | NarrativeDirector.RunBeatLoop (Phase 14) | Time-based lifecycle with early exit, goal-met, and skip keywords |
| Chat burst timing | Custom scheduler | ChatBotManager.ScriptedBurstLoop (Phase 13) | Organic burst-and-lull with pacing awareness |
| Scene transition | Custom scene loader | SceneTransitionHandler (Phase 15) | Clean disconnect, build settings validation, instant cut |
| Function registration | Manual JSON building | PersonaSession.RegisterFunction + FunctionDeclaration (package) | Type-safe, proven, handles native and prompt-based paths |
| Connection lifecycle | Manual WebSocket management | PersonaSession.Connect/Disconnect (package) | Handles auth, handshake, reconnection states, cleanup |
| PTT state machine | Custom input handling | PushToTalkController (Phase 14) | Finish-first pattern, transcript review, auto-submit, WaitingForAya state |
| Chat message tracking | Custom tracking system | TrackedChatMessage + ChatBotManager.GetUnrespondedMessages (Phase 13) | Already tracks responded state with recency timestamps |

**Key insight:** Phase 16 builds almost nothing from scratch. The work is connecting existing pieces, adding thin enrichment layers (fact tracker, transcript buffer, catalyst messages), and validating the whole chain.

## Common Pitfalls

### Pitfall 1: Initialization Order Race Condition
**What goes wrong:** LivestreamController starts NarrativeDirector.StartNarrative before PersonaSession.State == Connected. SendText calls fail silently (PersonaSession.SendText guards with a state check on line 269).
**Why it happens:** PersonaSession.Connect() is async -- it returns immediately but connection completes later when GeminiLiveClient fires Connected event.
**How to avoid:** LivestreamController MUST poll or subscribe to OnStateChanged and wait for Connected before calling StartNarrative or StartBursts.
**Warning signs:** NarrativeDirector sends director notes but Aya never responds. Console shows no errors (because SendText silently returns when not connected).

### Pitfall 2: Event Subscription Leaks on Scene Unload
**What goes wrong:** If LivestreamController subscribes to PersonaSession events but SceneTransitionHandler calls Disconnect() and LoadSceneAsync, the MonoBehaviours are destroyed. Dangling event handlers can fire after OnDestroy.
**Why it happens:** LoadSceneMode.Single destroys all objects in the current scene. If events fire between Disconnect and destruction, handlers touch destroyed objects.
**How to avoid:** SceneTransitionHandler already calls Disconnect() first (line 51). LivestreamController must unsubscribe from all events in OnDestroy. ChatBotManager already does this (lines 132-160). Follow the same pattern.
**Warning signs:** MissingReferenceException in console during scene transition.

### Pitfall 3: Context Window Growth Over 10 Minutes
**What goes wrong:** AyaChecksChat injects bot message summaries via SendText. Over 10 minutes with 30+ bot messages, the accumulated context may cause Gemini to lose coherence or hit token limits.
**Why it happens:** Each SendText adds to the conversation history. Director notes at beat transitions + AyaChecksChat summaries + user speech transcripts all accumulate.
**How to avoid:** Keep AyaChecksChat summaries brief (3-5 messages max per injection, already enforced by `scene.maxResponsesToGenerate`). Track total director notes sent. If approaching limits, reduce injection frequency. The existing StringBuilder pattern with `maxResponsesToGenerate` caps already mitigate this.
**Warning signs:** Aya's responses become increasingly generic or incoherent in the final beat. Aya starts mixing up topics.

### Pitfall 4: Double PTT Handling
**What goes wrong:** Both AyaSampleController.Update and PushToTalkController.Update listen for SPACE key. If both are active in the scene, double StartListening/StopListening calls occur.
**Why it happens:** AyaSampleController was the original demo controller with basic PTT. The new LivestreamController scene uses PushToTalkController instead.
**How to avoid:** LivestreamController replaces AyaSampleController in the livestream scene. Do NOT have both controllers active on the same GameObject or scene. The livestream scene should have LivestreamController, not AyaSampleController.
**Warning signs:** Double audio stream, Gemini receiving duplicate audio.

### Pitfall 5: Catalyst Messages Clustering
**What goes wrong:** If catalyst messages are only placed at the end of the message pool, they cluster at the end of the beat instead of being sprinkled throughout.
**Why it happens:** PickMessage uses sequential index tracking through the combined pool. If catalyst messages are appended at the end, they appear after all regular messages.
**How to avoid:** Catalyst messages should be selected by probability (e.g., 30% chance per burst to pick a catalyst instead of regular), not by pool order. Alternatively, interleave catalyst messages in the pool. The probability approach is simpler and more organic.
**Warning signs:** Bot messages feel generic for most of the beat, then suddenly become narrative-focused near the end.

### Pitfall 6: Dead Air During Gemini Latency Spikes
**What goes wrong:** Gemini Live WebSocket response latency can spike to 5-10 seconds. During this time, Aya is silent, bots may also be in a lull period, and the user sees nothing happening.
**Why it happens:** Gemini Live API latency is variable. Network conditions and model load affect response time.
**How to avoid:** Dead air detection timer triggers bot activity after silence threshold (8-12s). "Aya is thinking..." indicator shows after 5s. Both give the user feedback that the system is working.
**Warning signs:** User stares at a silent screen for 10+ seconds with no indication anything is happening.

### Pitfall 7: Fact Tracker Scope Creep
**What goes wrong:** The fact tracker becomes a complex state machine tracking every possible conversation topic, requiring extensive authoring per beat.
**Why it happens:** Overengineering the coherence system for edge cases that won't be noticed in a 10-minute demo.
**How to avoid:** Keep the fact tracker to 5-8 high-level facts (e.g., "aya_mentioned_movie", "aya_talked_about_characters", "user_asked_about_reveal"). Update facts at beat transitions, not continuously. Query facts only in dynamic bot prompts, not in every system.
**Warning signs:** Authoring beat configs becomes tedious with dozens of fact IDs. System complexity exceeds the 10-minute experience duration.

### Pitfall 8: ChatBotManager Not Stopped Before Scene Transition
**What goes wrong:** SceneTransitionHandler disconnects PersonaSession and loads a new scene. But ChatBotManager's burst loop is still running and tries to access destroyed UI elements.
**Why it happens:** ChatBotManager.StopBursts is never called before the scene transition.
**How to avoid:** LivestreamController subscribes to OnAllBeatsComplete and calls ChatBotManager.StopBursts() before the scene transition begins. Or: SceneTransitionHandler triggers a "shutting down" event that LivestreamController listens to for orderly cleanup.
**Warning signs:** OperationCanceledException or MissingReferenceException from ChatBotManager during scene unload.

## Code Examples

Verified patterns from the existing codebase:

### LivestreamController Skeleton
```csharp
// Source: synthesized from AyaSampleController.cs, NarrativeDirector.cs, ChatBotManager.cs patterns
public class LivestreamController : MonoBehaviour
{
    [Header("Core Subsystems")]
    [SerializeField] private PersonaSession _session;
    [SerializeField] private NarrativeDirector _narrativeDirector;
    [SerializeField] private ChatBotManager _chatBotManager;
    [SerializeField] private PushToTalkController _pttController;
    [SerializeField] private SceneTransitionHandler _sceneTransitionHandler;
    [SerializeField] private LivestreamUI _livestreamUI;

    [Header("Configuration")]
    [SerializeField] private AnimationConfig _animationConfig;
    [SerializeField] private float _connectionTimeout = 15f;
    [SerializeField] private float _deadAirThreshold = 10f;
    [SerializeField] private float _thinkingIndicatorDelay = 5f;

    // Cross-system context
    private AyaTranscriptBuffer _ayaTranscriptBuffer;
    private FactTracker _factTracker;
    private float _lastOutputTime;

    private void Start()
    {
        _ayaTranscriptBuffer = new AyaTranscriptBuffer(maxTurns: 5);
        _factTracker = new FactTracker();

        // Wire transcript buffer to PersonaSession events
        _session.OnOutputTranscription += text => _ayaTranscriptBuffer.AppendText(text);
        _session.OnTurnComplete += () => _ayaTranscriptBuffer.CompleteTurn();

        // Track last output time for dead air detection
        _session.OnAISpeakingStarted += () => _lastOutputTime = Time.time;

        // Pass shared context to subsystems
        _chatBotManager.SetContextProviders(_ayaTranscriptBuffer, _factTracker);
        _narrativeDirector.SetFactTracker(_factTracker);

        _ = InitializeAndStart();
    }
}
```

### Graceful Degradation on Connection Failure
```csharp
// Source: extends PersonaSession.Connect pattern (line 136-250 of PersonaSession.cs)
// with timeout and error handling
private async Awaitable WaitForConnection()
{
    float elapsed = 0f;
    while (_session.State == SessionState.Connecting && elapsed < _connectionTimeout)
    {
        await Awaitable.WaitForSecondsAsync(0.1f, destroyCancellationToken);
        elapsed += 0.1f;
    }

    if (_session.State == SessionState.Connected)
    {
        Debug.Log("[LivestreamController] Connected to Gemini.");
        return;
    }

    // Graceful degradation: log warning, disable Aya subsystems, keep bots running
    Debug.LogWarning(
        $"[LivestreamController] Connection {(_session.State == SessionState.Error ? "failed" : "timed out")}. " +
        "Starting in degraded mode (bots only, no Aya).");
}
```

### Orderly Shutdown Before Scene Transition
```csharp
// Source: follows SceneTransitionHandler.HandleAllBeatsComplete pattern (line 41-65)
// Extended with subsystem cleanup
private void HandleNarrativeComplete()
{
    // Stop subsystems in reverse order
    _chatBotManager.StopBursts();

    // SceneTransitionHandler handles Disconnect + scene load
    // (already subscribed to OnAllBeatsComplete in its own OnEnable)
}
```

### Dead Air Detection Loop
```csharp
// Source: new pattern following ChatBotManager.ScriptedBurstLoop async Awaitable style
private async Awaitable MonitorDeadAir()
{
    try
    {
        while (_running)
        {
            await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

            float silenceDuration = Time.time - _lastOutputTime;

            // Show "thinking" indicator after threshold
            if (silenceDuration >= _thinkingIndicatorDelay && !_thinkingShown)
            {
                _livestreamUI.ShowThinkingIndicator(true);
                _thinkingShown = true;
            }

            // Trigger bot activity after dead air threshold
            if (silenceDuration >= _deadAirThreshold)
            {
                // Reset timer to avoid repeated triggers
                _lastOutputTime = Time.time;
                _thinkingShown = false;
                _livestreamUI.ShowThinkingIndicator(false);
                // ChatBotManager's next burst will fire naturally
                // Or: explicitly trigger a burst here
            }
        }
    }
    catch (OperationCanceledException) { }
}
```

### NarrativeBeatConfig with Catalyst Fields
```csharp
// Source: extends existing NarrativeBeatConfig (Assets/AyaLiveStream/NarrativeBeatConfig.cs)
// Adding catalyst support per CONTEXT.md decisions

// Add to NarrativeBeatConfig:
[Header("Catalyst")]
[Tooltip("What are bots trying to nudge Aya toward in this beat?")]
[TextArea(1, 3)]
public string catalystGoal;

[Tooltip("Catalyst messages sprinkled in chat to push narrative forward. Mixed with regular messages.")]
[TextArea(1, 3)]
public string[] catalystMessages;
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| AyaSampleController as root | LivestreamController as root | Phase 16 | New controller owns initialization, wiring, and lifecycle; AyaSampleController preserved for simple demo |
| No cross-system context | AyaTranscriptBuffer + FactTracker | Phase 16 | Bots know what Aya said; no system contradicts established facts |
| No catalyst messages | catalystGoal + catalystMessages on NarrativeBeatConfig | Phase 16 | Bots actively push narrative, not just ambient chat |
| No dead air handling | Dead air timer + thinking indicator | Phase 16 | User never sees unexplained silence |
| No "going live" moment | Loading state + going live transition | Phase 16 | Experience feels like tuning into a real stream |

**Unchanged (confirmed still current):**
- SendText for mid-session steering (GoalManager cannot update mid-session, confirmed line 801-805 of PersonaSession.cs)
- destroyCancellationToken for async loop cleanup (Unity 6 standard, used by all existing subsystems)
- AyaChecksChat summary pattern for bot-to-Aya injection (Phase 14, still correct)
- SceneTransitionHandler instant cut with Disconnect-before-load (Phase 15, still correct)

## Subsystem Wiring Map

This is the key reference for the planner -- which subsystem connects to which, and how:

```
LivestreamController (NEW)
  |-- [SerializeField] PersonaSession
  |     |-- Connect() / Disconnect()
  |     |-- OnStateChanged -> wait for Connected
  |     |-- OnOutputTranscription -> AyaTranscriptBuffer.AppendText
  |     |-- OnTurnComplete -> AyaTranscriptBuffer.CompleteTurn
  |     |-- OnAISpeakingStarted -> reset dead air timer
  |     |-- RegisterFunction("play_animation", ...)
  |
  |-- [SerializeField] NarrativeDirector
  |     |-- StartNarrative() -- called after Connected
  |     |-- OnAllBeatsComplete -> orderly shutdown
  |     |-- OnBeatStarted -> update FactTracker
  |     |-- SetFactTracker(FactTracker)
  |     |-- Already wired to: PersonaSession, ChatBotManager, LivestreamUI
  |
  |-- [SerializeField] ChatBotManager
  |     |-- StartBursts() / StopBursts()
  |     |-- SetContextProviders(AyaTranscriptBuffer, FactTracker)
  |     |-- Already wired to: PersonaSession, LivestreamUI, NarrativeDirector
  |
  |-- [SerializeField] PushToTalkController
  |     |-- Self-contained (reads SPACE key, manages state machine)
  |     |-- Already wired to: PersonaSession, NarrativeDirector, LivestreamUI
  |
  |-- [SerializeField] SceneTransitionHandler
  |     |-- Self-contained (subscribes to OnAllBeatsComplete in OnEnable)
  |     |-- Already wired to: NarrativeDirector, PersonaSession
  |
  |-- [SerializeField] LivestreamUI
  |     |-- AddMessage, ShowToast, SetAyaSpeaking, SetPTTStatus
  |     |-- NEW: SetLoadingState, ShowGoingLive, ShowThinkingIndicator
  |
  |-- AyaTranscriptBuffer (plain C# object, created in Start)
  |-- FactTracker (plain C# object, created in Start)
```

### Event Flow During Normal Operation
```
1. LivestreamController.Start()
   -> RegisterFunctions
   -> PersonaSession.Connect()
   -> Poll for Connected
   -> "Going live" transition
   -> ChatBotManager.StartBursts()
   -> NarrativeDirector.StartNarrative()
   -> Start dead air monitor

2. Beat Loop (NarrativeDirector runs this):
   -> OnBeatStarted fires -> FactTracker updated
   -> ExecuteBeatScenes:
     - AyaDialogue: SendText -> Aya speaks -> OnOutputTranscription -> AyaTranscriptBuffer
     - AyaChecksChat: GetUnrespondedMessages -> summary SendText -> Aya speaks
   -> OnBeatEnded fires
   -> OnBeatTransition fires -> ChatBotManager pauses 5s

3. Chat Bursts (ChatBotManager runs in parallel):
   -> ScriptedBurstLoop picks messages (regular + catalyst)
   -> Posts to LivestreamUI
   -> TrackedChatMessage recorded

4. User PTT (PushToTalkController handles independently):
   -> SPACE pressed -> Recording
   -> SPACE released -> Reviewing -> auto-submit
   -> ChatBotManager.HandleUserSpeakingStopped -> dynamic Gemini call (enriched with AyaTranscriptBuffer + FactTracker)
   -> NarrativeDirector.CheckSkipKeywords -> possible beat skip

5. All Beats Complete:
   -> NarrativeDirector.OnAllBeatsComplete
   -> LivestreamController stops ChatBotManager
   -> SceneTransitionHandler disconnects + loads movie scene
```

## Open Questions

Things that couldn't be fully resolved:

1. **Exact dead air threshold**
   - What we know: CONTEXT.md says "silence exceeds a threshold" but left specific duration to Claude's discretion
   - What's unclear: What feels natural? Too short (5s) triggers too often during normal Gemini latency. Too long (15s) leaves obvious dead air.
   - Recommendation: Default to 10 seconds for dead air bot trigger, 5 seconds for "thinking" indicator. Make both configurable via [SerializeField] so they can be tuned during validation.

2. **Catalyst message frequency**
   - What we know: CONTEXT.md says "sprinkled throughout the beat, mixed with regular messages"
   - What's unclear: What percentage of messages should be catalysts? Too many feels forced, too few has no narrative effect.
   - Recommendation: 25-30% chance per burst to select a catalyst message instead of a regular message. Configurable via a field on NarrativeBeatConfig.

3. **Fact tracker granularity**
   - What we know: CONTEXT.md says "central object tracks established facts"
   - What's unclear: Should facts be automatically detected from Aya's transcript, or manually set at beat transitions?
   - Recommendation: Manual at beat transitions (simple, reliable). Auto-detection from transcript would require NLP and add complexity for a 10-minute demo. Beat-level facts (5-8 total) are sufficient.

4. **PTT topic matching for skip-ahead**
   - What we know: Existing CheckSkipKeywords matches against the current beat's keywords and skips to the final beat. CONTEXT.md says "if the user asks about a future beat topic, the director jumps to that beat."
   - What's unclear: How sophisticated should topic matching be? Simple keyword match on all future beats? Or more nuanced?
   - Recommendation: Simple keyword match against all future beats' skipKeywords arrays. If a match is found for beat N (where N > currentBeat), advance to N. Keep it simple -- for a 3-beat demo with 1-2 skip keywords per beat, simple string matching is sufficient.

5. **"Going live" transition visual**
   - What we know: CONTEXT.md says "brief loading state while subsystems connect, then a going live transition moment"
   - What's unclear: Exact presentation (text overlay? animation? sound?)
   - Recommendation: Simple text change in LivestreamUI -- loading state shows "Connecting..." text, then changes to "GOING LIVE!" for 1.5 seconds before the experience starts. CSS class toggle for visual emphasis. No audio.

## Sources

### Primary (HIGH confidence)
- AyaSampleController.cs (Assets/AyaLiveStream/) -- existing orchestration pattern
- PersonaSession.cs (Packages/.../Runtime/) -- session lifecycle, events, SendText API
- NarrativeDirector.cs (Assets/AyaLiveStream/) -- beat loop, scene execution, event model
- ChatBotManager.cs (Assets/AyaLiveStream/) -- burst loop, dynamic responses, message tracking
- PushToTalkController.cs (Assets/AyaLiveStream/) -- PTT state machine
- SceneTransitionHandler.cs (Assets/AyaLiveStream/) -- clean scene exit
- LivestreamUI.cs (Assets/AyaLiveStream/) -- UI API surface
- NarrativeBeatConfig.cs (Assets/AyaLiveStream/) -- beat data model
- 16-CONTEXT.md -- user decisions constraining Phase 16 implementation

### Secondary (MEDIUM confidence)
- Phase 14 RESEARCH.md -- pitfalls 3 and 8 carry forward (SendText-during-speaking, context window growth)
- Phase 15 RESEARCH.md -- scene transition patterns carry forward
- CreateBeatAssets.cs -- existing beat asset structure that will be extended with catalyst fields

### Tertiary (LOW confidence)
- Dead air threshold values (10s / 5s) -- empirical, needs validation in testing
- Catalyst message frequency (25-30%) -- empirical, needs tuning

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all components already exist and are validated in the codebase
- Architecture: HIGH -- patterns are direct extensions of proven existing patterns (AyaSampleController, NarrativeDirector, ChatBotManager)
- Pitfalls: HIGH -- derived from actual code analysis, not hypothetical
- Cross-system context: MEDIUM -- the Aya-to-bot and fact tracker patterns are new but follow established conventions
- Tuning values (dead air, catalyst frequency): LOW -- empirical, require real-world testing

**Research date:** 2026-02-17
**Valid until:** 2026-03-17 (stable; all components are local code, no external dependency changes expected)
