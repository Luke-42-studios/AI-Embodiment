# Phase 13: Chat Bot System - Research

**Researched:** 2026-02-17
**Domain:** Chat bot scheduling, burst timing, Gemini REST structured output for dynamic responses, TrackedChatMessage tracking, nevatars migration
**Confidence:** HIGH

## Summary

Phase 13 wires the chat bot system on top of Phase 12's foundation: ChatBotConfig ScriptableObjects (6 bots already created as .asset files), GeminiTextClient (REST wrapper with `GenerateAsync<T>`), ChatMessage data model, and LivestreamUI (ListView chat feed with `AddMessage()`). The work breaks into three domains: (1) a ChatBotManager MonoBehaviour that schedules scripted message bursts with organic timing, (2) dynamic Gemini responses triggered by user push-to-talk speech, and (3) migrating response patterns from the nevatars project to populate the currently-empty `messageAlternatives` arrays and expand scripted message pools.

The nevatars codebase (at `/home/cachy/workspaces/projects/games/nevatars/`) contains a fully-implemented ChatBurstController with Fisher-Yates shuffle, randomized bot count (1-4), pauseable timing, and configurable delay ranges (0.8-3.0s). It also contains 110 ResponsePattern assets, each with `chatBurstMessages` -- generic bot reaction lines (not per-bot) like "burnout is real", "take care of yourself". The new system replaces nevatars' pattern-matching with Gemini structured output for dynamic responses, but the scripted message pools and burst-timing algorithm are directly portable.

The key architectural insight is that ChatBotManager needs two operational modes running simultaneously: (1) a periodic scripted burst loop that fires every N seconds (lull period), selects 1-4 bots, picks from their `scriptedMessages`/`messageAlternatives`, and posts with staggered delays; and (2) an event-driven dynamic response path that triggers when `PersonaSession.OnUserSpeakingStopped` fires, sends a single batched Gemini REST call, and posts the returned bot reactions with per-bot timing.

**Primary recommendation:** Build ChatBotManager as a MonoBehaviour with two async loops -- a periodic `ScriptedBurstLoop()` using `Awaitable` delays and an event-driven `OnUserSpeechComplete()` handler that calls `GeminiTextClient.GenerateAsync<BotReaction[]>()`. Use the nevatars ChatBurstController's timing logic (0.8-3.0s random delays, Fisher-Yates shuffle, randomized bot count) translated from coroutines to `async Awaitable`.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| GeminiTextClient | Phase 12 | REST structured output for dynamic bot responses | Already built and tested; wraps UnityWebRequest with Awaitable |
| ChatBotConfig | Phase 12 | Per-bot personality, scripted messages, behavior settings | 6 assets already created with distinct personalities |
| ChatMessage | Phase 12 | Data model flowing between ChatBotManager and LivestreamUI | Convenience constructor from ChatBotConfig |
| LivestreamUI | Phase 12 | ListView chat feed with AddMessage() | Already bound to ListView with DynamicHeight |
| PersonaSession | Package runtime | Events for user speech start/stop, Aya turn completion | OnUserSpeakingStopped triggers dynamic responses |
| Newtonsoft.Json | 3.2.1 | JObject schema building for structured output | Already a dependency; needed for responseSchema |
| UnityEngine.Random | Built-in | Shuffle, random selection, delay randomization | Fisher-Yates shuffle, Random.Range for delays |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| AIEmbodimentSettings | Package runtime | API key access for GeminiTextClient | `AIEmbodimentSettings.Instance.ApiKey` at startup |
| System.Linq | .NET Standard 2.1 | List operations (shuffle, take, where) | Message pool selection, bot filtering |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Awaitable delays | Coroutines (IEnumerator) | Coroutines work (nevatars uses them) but Awaitable is the Unity 6 standard and matches GeminiTextClient pattern |
| Single batched Gemini call | Per-bot Gemini calls | Single call is cheaper, faster, and lets Gemini choose which bots react naturally |
| Fisher-Yates in ChatBotManager | System.Random with OrderBy | Fisher-Yates is O(n) and proven in nevatars ChatBurstController; OrderBy is O(n log n) |

**Installation:**
No new packages needed. All dependencies are already present.

## Architecture Patterns

### Recommended Project Structure
```
Assets/
  AyaLiveStream/
    ChatBotConfig.cs             # EXISTS (Phase 12)
    ChatMessage.cs               # EXISTS (Phase 12)
    GeminiTextClient.cs          # EXISTS (Phase 12)
    LivestreamUI.cs              # EXISTS (Phase 12)
    AyaSampleController.cs       # EXISTS -- modify to wire ChatBotManager
    ChatBotManager.cs            # NEW -- scripted bursts + dynamic responses
    TrackedChatMessage.cs        # NEW -- tracking wrapper for ChatMessage
    BotReaction.cs               # NEW -- deserialization target for Gemini response
    ChatBotConfigs/
      Dad_JohnConfig.asset       # EXISTS -- add messageAlternatives via migration
      TeenFan_MikoConfig.asset   # EXISTS -- add messageAlternatives via migration
      ...                        # 6 bot .asset files
    Editor/
      MigrateChatBotConfigs.cs   # EXISTS -- extend or create new migration script
      MigrateResponsePatterns.cs # NEW -- extract chatBurstMessages from nevatars patterns
```

### Pattern 1: ChatBotManager MonoBehaviour
**What:** Central orchestrator for all bot chat activity -- scripted bursts and dynamic responses
**When to use:** The single entry point for bot chat in the livestream scene
**Example:**
```csharp
// Source: designed from nevatars ChatBurstController pattern + Phase 12 GeminiTextClient API
public class ChatBotManager : MonoBehaviour
{
    [SerializeField] private LivestreamUI _livestreamUI;
    [SerializeField] private PersonaSession _session;
    [SerializeField] private ChatBotConfig[] _bots;

    [Header("Burst Timing")]
    [SerializeField] private float _burstIntervalMin = 8f;
    [SerializeField] private float _burstIntervalMax = 18f;
    [SerializeField] private float _messageDelayMin = 0.8f;
    [SerializeField] private float _messageDelayMax = 3.0f;
    [SerializeField] private int _maxBotsPerBurst = 4;

    private GeminiTextClient _textClient;
    private readonly List<TrackedChatMessage> _trackedMessages = new();
    private bool _running;
    private CancellationTokenSource _cts;

    // Per-bot message index tracking (avoid repeats within a session)
    private Dictionary<ChatBotConfig, List<int>> _usedMessageIndices = new();
}
```

### Pattern 2: Scripted Burst Loop (async Awaitable)
**What:** Periodic loop that fires scripted message bursts with organic timing
**When to use:** Background ambient chat activity throughout the stream
**Example:**
```csharp
// Source: nevatars ChatBurstController.ChatBurstCoroutine translated to async Awaitable
private async Awaitable ScriptedBurstLoop()
{
    while (_running)
    {
        // Lull period between bursts
        float lullDuration = Random.Range(_burstIntervalMin, _burstIntervalMax);
        await Awaitable.WaitForSecondsAsync(lullDuration);

        if (!_running) break;

        // Select 1-4 bots for this burst
        int botCount = Random.Range(1, Mathf.Min(_maxBotsPerBurst, _bots.Length) + 1);
        var shuffledBots = ShuffleCopy(_bots);
        var burstBots = shuffledBots.Take(botCount);

        // Post messages with staggered delays
        foreach (var bot in burstBots)
        {
            string message = PickMessage(bot);
            var chatMsg = new ChatMessage(bot, message);
            _livestreamUI.AddMessage(chatMsg);
            TrackMessage(chatMsg);

            float delay = Random.Range(_messageDelayMin, _messageDelayMax);
            await Awaitable.WaitForSecondsAsync(delay);
        }
    }
}
```

### Pattern 3: Dynamic Response via Gemini Structured Output
**What:** Single batched Gemini call returning multiple bot reactions to user speech
**When to use:** When user speaks via push-to-talk (OnUserSpeakingStopped + OnInputTranscription)
**Example:**
```csharp
// Source: GeminiTextClient.GenerateAsync<T> API + CONTEXT.md decisions
[Serializable]
public class BotReaction
{
    public string botName;
    public string message;
    public float delay; // seconds to wait before posting (staggered timing)
}

private static readonly JObject DynamicResponseSchema = new JObject
{
    ["type"] = "ARRAY",
    ["items"] = new JObject
    {
        ["type"] = "OBJECT",
        ["properties"] = new JObject
        {
            ["botName"] = new JObject { ["type"] = "STRING" },
            ["message"] = new JObject { ["type"] = "STRING" },
            ["delay"] = new JObject { ["type"] = "NUMBER" }
        },
        ["required"] = new JArray("botName", "message", "delay")
    }
};

private async Awaitable HandleUserSpeech(string userTranscript)
{
    // Build prompt with all 6 bot personalities
    string prompt = BuildDynamicPrompt(userTranscript);

    BotReaction[] reactions = await _textClient.GenerateAsync<BotReaction[]>(
        prompt, DynamicResponseSchema);

    // Post reactions with Gemini-determined staggered delays
    foreach (var reaction in reactions)
    {
        await Awaitable.WaitForSecondsAsync(reaction.delay);
        var bot = FindBotByName(reaction.botName);
        if (bot != null)
        {
            var chatMsg = new ChatMessage(bot, reaction.message);
            _livestreamUI.AddMessage(chatMsg);
            TrackMessage(chatMsg);
        }
    }
}
```

### Pattern 4: TrackedChatMessage Wrapper
**What:** Thin wrapper around ChatMessage that tracks whether Aya has responded to it
**When to use:** BOT-06 requirement -- prevents Aya from acknowledging the same message twice
**Example:**
```csharp
// Source: designed for BOT-06 requirement
public class TrackedChatMessage
{
    public ChatMessage Message { get; }
    public bool AyaHasResponded { get; set; }
    public float PostedAtTime { get; }

    public TrackedChatMessage(ChatMessage message)
    {
        Message = message;
        AyaHasResponded = false;
        PostedAtTime = Time.time;
    }
}
```

### Pattern 5: Nevatars Migration Script
**What:** Editor script that reads nevatars ResponsePattern chatBurstMessages and populates ChatBotConfig messageAlternatives
**When to use:** One-time MIG-03 data extraction
**Key insight:** Nevatars `chatBurstMessages` are generic (not per-bot) -- e.g., "burnout is real", "take care of yourself". These need to be distributed across bots based on personality fit. The migration script should extract ALL unique chatBurstMessages from all 110 ResponsePattern assets, then assign them to bots based on personality matching (manual curation or algorithmic by tone).

### Anti-Patterns to Avoid
- **One Gemini call per bot:** The CONTEXT.md explicitly says "One Gemini structured output call per user speech event." Never make 6 separate calls.
- **Bot chain reactions:** CONTEXT.md explicitly says "No chain reactions between bots -- keeps dynamic responses bounded and API costs predictable." Dynamic responses trigger ONLY from user push-to-talk, not from Aya's dialogue or other bots.
- **Metronomic timing:** Fixed-interval bursts feel robotic. Use randomized lull periods (8-18s range) AND randomized per-message delays (0.8-3.0s) with randomized bot counts (1-4).
- **Mutable ScriptableObject data at runtime:** Do not modify ChatBotConfig.scriptedMessages at runtime. Track used message indices in a separate Dictionary to avoid repeats.
- **Blocking the main thread with Gemini calls:** GeminiTextClient.GenerateAsync already uses Awaitable -- just await it. Do not wrap in Task.Run.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Random delay timing | Custom timer system | `Awaitable.WaitForSecondsAsync()` + `Random.Range()` | Unity 6 native; cleaner than coroutines; matches existing pattern |
| List shuffling | LINQ OrderBy(Random) | Fisher-Yates shuffle (from nevatars ChatBurstController) | O(n) vs O(n log n); proven algorithm; no allocation |
| Structured JSON output | Manual JSON parsing | `GeminiTextClient.GenerateAsync<T>()` | Already handles schema, response extraction, deserialization |
| Message deduplication | Custom hash set | Per-bot used-index tracking with `Dictionary<ChatBotConfig, List<int>>` | Simple, deterministic, no hash collisions |
| Chat feed posting | Direct ListView manipulation | `LivestreamUI.AddMessage(ChatMessage)` | Already handles RefreshItems, scroll-to-bottom, deferred execution |
| API key management | Hardcoded or env vars | `AIEmbodimentSettings.Instance.ApiKey` | Already exists in Resources; singleton pattern |

**Key insight:** This phase is primarily wiring and scheduling. The building blocks (REST client, UI, data model, bot configs) all exist from Phase 12. The novel work is the scheduling algorithm, the dynamic response prompt/schema, and the TrackedChatMessage system.

## Common Pitfalls

### Pitfall 1: Awaitable.WaitForSecondsAsync Canceled on Destroy
**What goes wrong:** ChatBotManager is destroyed while an async loop is awaiting, causing OperationCanceledException.
**Why it happens:** Unity cancels Awaitables tied to destroyed MonoBehaviours.
**How to avoid:** Use a CancellationTokenSource and set `_running = false` in OnDisable/OnDestroy. Wrap the burst loop in try/catch for OperationCanceledException. Set `destroyCancellationToken` as the cancellation token.
**Warning signs:** Console errors about canceled operations on scene unload or Stop.

### Pitfall 2: GeminiTextClient Disposed Before Dynamic Response Completes
**What goes wrong:** User speaks near end of session, GeminiTextClient is disposed while the request is in flight.
**Why it happens:** Session cleanup disposes the client before the response arrives.
**How to avoid:** `GenerateAsync<T>` already returns `default` if disposed during request (checked at line 94-95 of GeminiTextClient.cs). The caller should null-check the result.
**Warning signs:** NullReferenceException when processing reactions after session end.

### Pitfall 3: Message Pool Exhaustion
**What goes wrong:** After many bursts, a bot has used all its scripted messages and starts repeating immediately.
**Why it happens:** 6-7 scripted messages per bot get exhausted quickly over a 10-15 minute session.
**How to avoid:** (1) MIG-03 adds messageAlternatives to increase the pool. (2) Track used indices per bot and reset when all are used (full cycle before any repeat). (3) With 7 scripted + alternatives, there should be enough for a session.
**Warning signs:** Same message appearing twice in quick succession from the same bot.

### Pitfall 4: Gemini Response Contains Invalid Bot Names
**What goes wrong:** Gemini returns a botName that doesn't match any ChatBotConfig.
**Why it happens:** Gemini may produce slight variations like "Dad John" instead of "Dad_John".
**How to avoid:** Include exact bot names in the prompt. In the response handler, use case-insensitive matching with underscore normalization. If no match, skip that reaction rather than crashing.
**Warning signs:** "Bot not found" warnings in console; some dynamic reactions silently dropped.

### Pitfall 5: Dynamic Response Timing Conflict with Scripted Burst
**What goes wrong:** Dynamic responses from user speech arrive while a scripted burst is in progress, causing messages to interleave confusingly.
**Why it happens:** Both the scripted burst loop and the dynamic response handler post to the same chat feed.
**How to avoid:** This is actually fine and desirable -- real Twitch chat interleaves organic messages. But if a scripted burst just fired, add a small delay before dynamic responses start (Gemini's per-reaction delays help here). Do NOT try to mutex the two paths.
**Warning signs:** Three messages from the same bot appearing back-to-back (scripted + dynamic).

### Pitfall 6: PersonaSession Events Fire on Main Thread But Async Work May Not Complete Before Next Event
**What goes wrong:** OnUserSpeakingStopped fires, dynamic response request starts, then another OnUserSpeakingStopped fires before the first completes.
**Why it happens:** User presses push-to-talk quickly twice.
**How to avoid:** Use a `_dynamicResponseInFlight` bool guard. If a dynamic response request is already in progress, queue the new transcript and process it after the current one completes, or drop it.
**Warning signs:** Duplicate reactions to the same speech, or reactions to old speech appearing after new speech.

### Pitfall 7: chatBurstMessages from Nevatars Are Generic, Not Per-Bot
**What goes wrong:** Migration script puts all 300+ chatBurstMessages into every bot's messageAlternatives.
**Why it happens:** Nevatars ResponsePattern.chatBurstMessages are topic-level, not character-level (e.g., "burnout is real" is not assigned to a specific character).
**How to avoid:** The migration script must curate which messages go to which bot based on personality fit. Group messages by tone/style and assign to matching bots. Some messages ("burnout is real") fit ArtStudent_Priya. Others ("LETS GOOOOO!!!") fit TeenFan_Miko. Dad_John gets supportive/encouraging ones. This requires manual or semi-automated curation.
**Warning signs:** Ghost404 (the lurker) posting long enthusiastic messages that don't match their personality.

## Code Examples

### Existing Phase 12 API Surface (what ChatBotManager uses)

#### LivestreamUI.AddMessage()
```csharp
// Source: Assets/AyaLiveStream/LivestreamUI.cs line 100-105
public void AddMessage(ChatMessage msg)
{
    _messages.Add(msg);
    _chatFeed.RefreshItems();
    _chatFeed.schedule.Execute(() => _chatFeed.ScrollToItem(_messages.Count - 1));
}
```

#### ChatMessage Constructor
```csharp
// Source: Assets/AyaLiveStream/ChatMessage.cs line 23-31
public ChatMessage(ChatBotConfig source, string text)
{
    BotName = source.botName;
    BotColor = source.chatColor;
    Text = text;
    Timestamp = DateTime.Now.ToString("HH:mm");
    IsFromUser = false;
    Source = source;
}
```

#### GeminiTextClient.GenerateAsync<T>()
```csharp
// Source: Assets/AyaLiveStream/GeminiTextClient.cs line 63
// UPPERCASE type constants required for responseSchema
public async Awaitable<T> GenerateAsync<T>(string prompt, JObject responseSchema)
```

#### PersonaSession Events for Dynamic Response Triggers
```csharp
// Source: Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs
public event Action OnUserSpeakingStopped;     // line 70 -- user released PTT
public event Action<string> OnInputTranscription; // line 52 -- user's speech text
public event Action OnTurnComplete;            // line 49 -- Aya finished responding
```

### Nevatars Burst Timing Pattern (to translate)
```csharp
// Source: nevatars ChatBurstController.cs lines 112-128
// Randomized bot count: 1 to min(4, validMessages.Count)
if (scene.randomizeBotCount && validMessages.Count > 1)
{
    int maxCount = Mathf.Min(4, validMessages.Count);
    int countToInclude = Random.Range(1, maxCount + 1);
    ShuffleList(validMessages);
    validMessages = validMessages.GetRange(0, countToInclude);
}

// Delay between messages: 0.8-3.0s (from StreamingConfig)
float delay = config.GetRandomChatDelay(); // Random.Range(0.8f, 3.0f)
```

### Gemini Structured Output Schema for Dynamic Responses
```csharp
// Source: designed from CONTEXT.md decisions + GeminiTextClient API
// Returns 1-3 bot reactions with staggered timing
private static readonly JObject DynamicResponseSchema = new JObject
{
    ["type"] = "ARRAY",
    ["items"] = new JObject
    {
        ["type"] = "OBJECT",
        ["properties"] = new JObject
        {
            ["botName"] = new JObject
            {
                ["type"] = "STRING",
                ["description"] = "Exact bot name from the list provided"
            },
            ["message"] = new JObject
            {
                ["type"] = "STRING",
                ["description"] = "The bot's chat message"
            },
            ["delay"] = new JObject
            {
                ["type"] = "NUMBER",
                ["description"] = "Seconds to wait before posting (0.5-3.0)"
            }
        },
        ["required"] = new JArray("botName", "message", "delay")
    }
};
```

### Dynamic Response Prompt Template
```csharp
// Source: designed from CONTEXT.md decisions
// All 6 bot personalities included so Gemini picks natural responders
private string BuildDynamicPrompt(string userTranscript)
{
    var sb = new StringBuilder();
    sb.AppendLine("You are generating chat bot reactions for a livestream.");
    sb.AppendLine("The viewer just said via push-to-talk:");
    sb.AppendLine($"\"{userTranscript}\"");
    sb.AppendLine();
    sb.AppendLine("Available chat bots (return 1-3 reactions from the most natural responders):");

    foreach (var bot in _bots)
    {
        sb.AppendLine($"- {bot.botName}: {bot.personality}");
    }

    sb.AppendLine();
    sb.AppendLine("Rules:");
    sb.AppendLine("- Pick 1-3 bots who would naturally react to what the viewer said");
    sb.AppendLine("- Match each bot's personality and speech style exactly");
    sb.AppendLine("- Messages should be chat-length (5-30 words max)");
    sb.AppendLine("- Stagger delays: first bot 0.5-1.0s, subsequent bots 1.0-3.0s apart");
    sb.AppendLine("- Ghost404 (lurker) should be selected rarely");

    return sb.ToString();
}
```

## Nevatars Data: What Needs Migrating (MIG-03)

### Source Data Location
```
/home/cachy/workspaces/projects/games/nevatars/
  Assets/_Project/DialogueAI/StreamingBotsDemo/
    Data/
      Patterns/          # 110 ResponsePattern .asset files (100 topic + 10 fallback)
      Characters/        # 14 StreamingCharacter .asset files (original bot roster)
      Beats/             # 9 NarrativeBeat .asset files (Phase 14, not this phase)
    Scripts/
      Data/
        ResponsePattern.cs     # chatBurstMessages field definition
        StreamingCharacter.cs  # Original character ScriptableObject
```

### What MIG-03 Actually Means
The `chatBurstMessages` field on each ResponsePattern contains 2-4 generic bot reaction lines per topic. With 110 patterns, that is approximately 300-400 unique message strings. These are NOT per-bot -- they are topic-reactions like:

| Pattern Topic | chatBurstMessages |
|---------------|-------------------|
| greeting | "welcome!", "hey!", "hi hi!", "hello!" |
| art_style | "her style is so unique", "love her work", "goals tbh" |
| burnout | "burnout is real", "take care of yourself", "rest is important" |
| favorite_color | "ooo i love color theory talk!", "same aya same!", "that's such an artist answer lol" |

### Migration Strategy
1. **Extract all chatBurstMessages** from all 110 ResponsePattern .asset files (parse YAML, collect all strings)
2. **Curate per-bot assignment** -- distribute messages across the 6 AI-Embodiment bots by personality fit:
   - TeenFan_Miko: enthusiastic, short, caps, emojis ("LETS GOOOOO!!!", "omg yes", "so cool!!")
   - Dad_John: supportive, encouraging, no emojis ("That's wonderful", "Keep it up")
   - ArtStudent_Priya: thoughtful, technical ("interesting technique", "love the color theory")
   - Regular_TechBro42: referencing, gatekeep-y ("day one fan stuff", "told you she's amazing")
   - Troll_xXShadowXx: contrarian, provocative ("is it though?", "I've seen better")
   - Lurker_Ghost404: minimal, lowercase ("cool", "nice", "wow")
3. **Populate messageAlternatives** on each ChatBotConfig .asset (either via editor script or manual editing)

### Important: Character Mapping
The nevatars project has 14 characters; AI-Embodiment has 6. The migration is NOT a 1:1 port. It is a content harvest -- extracting the rich message pool and redistributing it across the new (fewer, more distinct) bot personalities.

| Nevatars Character | Closest AI-Embodiment Bot | Notes |
|-------------------|--------------------------|-------|
| MR_WASAM (dad) | Dad_John | Direct personality match |
| STEPHASAURUS_03 (teen) | TeenFan_Miko | Enthusiastic fan energy |
| ARTFAN405 | ArtStudent_Priya | Art knowledge |
| QUIET_OBSERVER_12 | Lurker_Ghost404 | Minimal participation |
| PIXELMASTER_99 | Regular_TechBro42 | Regular/tech knowledge |
| hUnGrYbOi | Troll_xXShadowXx | Contrarian energy |
| Others (8 chars) | Distribute across all 6 | Personality-based assignment |

## State of the Art

| Old Approach (nevatars) | Current Approach (AI-Embodiment) | Impact |
|------------------------|----------------------------------|--------|
| Coroutine-based burst timing | async Awaitable burst timing | Cleaner code, no IEnumerator boilerplate |
| Pattern-matching for bot responses | Gemini structured output | More natural, context-aware responses |
| Per-character assigned messages in scene data | Per-bot message pools in ScriptableObject | Simpler data model, reusable across scenes |
| 14 characters with minimal personality | 6 characters with deep personality definitions | Fewer bots = more distinct, memorable |
| Generic chatBurstMessages (not per-bot) | Per-bot scripted + Gemini-generated | More personality-consistent messages |

## Open Questions

1. **Exact burst interval range (lull between bursts)**
   - What we know: The 0.8-3.0s requirement is for WITHIN-burst delays (between messages in a single burst). The roadmap does not specify lull duration between bursts.
   - What's unclear: How long should the quiet period be between scripted bursts?
   - Recommendation: Start with 8-18 seconds between bursts. This produces approximately 3-5 bursts per minute of "active chat" which feels like a small audience of 6 viewers. Expose as serialized fields for tuning.

2. **How to capture user transcript for dynamic responses**
   - What we know: `PersonaSession.OnInputTranscription` fires with the user's speech text. `OnUserSpeakingStopped` fires when the user releases PTT.
   - What's unclear: OnInputTranscription may fire multiple times during a single PTT session (streaming transcription). Need to accumulate the final transcript.
   - Recommendation: Subscribe to OnInputTranscription to accumulate text, then on OnUserSpeakingStopped, send the accumulated transcript to Gemini. Reset the buffer after each PTT release.

3. **TrackedChatMessage consumption by downstream systems**
   - What we know: BOT-06 requires tracking which messages Aya has responded to.
   - What's unclear: Phase 13 creates the tracking system, but Phase 14/16 consume it (NarrativeDirector injects unread messages into Aya's context). The exact consumption API is not yet defined.
   - Recommendation: Build TrackedChatMessage with a simple `AyaHasResponded` bool and a `GetUnrespondedMessages()` query method on ChatBotManager. Phase 14/16 will call this to build Aya's context. Keep the API surface minimal and extend later.

4. **Whether messageAlternatives should be separate from scriptedMessages**
   - What we know: ChatBotConfig has two string arrays: `scriptedMessages` and `messageAlternatives`. Both are empty or populated.
   - What's unclear: The original design intent of having two separate pools. Are alternatives for the SAME messages (rephrasing), or additional independent messages?
   - Recommendation: Treat both pools as a single combined pool for scripted bursts. The ChatBotManager picks from `scriptedMessages` first, then `messageAlternatives` once scripted messages are exhausted. Or simply concatenate them. The separation exists for author clarity in the Inspector (original lines vs. migrated lines).

## Sources

### Primary (HIGH confidence)
- `Assets/AyaLiveStream/GeminiTextClient.cs` -- REST client API surface, GenerateAsync<T> signature
- `Assets/AyaLiveStream/ChatBotConfig.cs` -- ScriptableObject fields (botName, personality, scriptedMessages, messageAlternatives, typingSpeed, capsFrequency, emojiFrequency)
- `Assets/AyaLiveStream/ChatMessage.cs` -- Data model, constructor from ChatBotConfig, FromUser factory
- `Assets/AyaLiveStream/LivestreamUI.cs` -- AddMessage() API, ListView binding
- `Assets/AyaLiveStream/AyaSampleController.cs` -- Existing controller pattern, PersonaSession event wiring
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` -- Event API (OnUserSpeakingStopped, OnInputTranscription, OnTurnComplete)
- `Assets/AyaLiveStream/ChatBotConfigs/*.asset` -- 6 bot assets with personalities, scripted messages, behavior settings
- `/home/cachy/workspaces/projects/games/nevatars/Assets/_Project/DialogueAI/StreamingBotsDemo/Scripts/Core/ChatBurstController.cs` -- Burst timing algorithm, Fisher-Yates shuffle, randomized bot count
- `/home/cachy/workspaces/projects/games/nevatars/Assets/_Project/DialogueAI/StreamingBotsDemo/Scripts/Data/ResponsePattern.cs` -- chatBurstMessages field, triggerChatBurst flag
- `/home/cachy/workspaces/projects/games/nevatars/Assets/_Project/DialogueAI/StreamingBotsDemo/Scripts/Data/StreamingConfig.cs` -- GetRandomChatDelay() with 0.8-3.0s range
- `/home/cachy/workspaces/projects/games/nevatars/Assets/_Project/DialogueAI/StreamingBotsDemo/Data/Patterns/*.asset` -- 110 ResponsePattern assets with chatBurstMessages content

### Secondary (MEDIUM confidence)
- `.planning/phases/12-foundation-and-data-model/12-RESEARCH.md` -- Phase 12 research findings (Gemini API format, UPPERCASE types, ListView DynamicHeight)
- `.planning/phases/13-chat-bot-system/13-CONTEXT.md` -- User decisions on batching, triggers, tone matching

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries already in the project; every API surface read directly from source code
- Architecture patterns: HIGH -- ChatBotManager design is a direct translation of nevatars ChatBurstController with Awaitable instead of coroutines, plus the proven GeminiTextClient for dynamic responses
- Burst timing: HIGH -- delay ranges (0.8-3.0s), randomized bot count (1-4), Fisher-Yates shuffle all verified in nevatars ChatBurstController source code
- Dynamic response schema: HIGH -- GeminiTextClient API and Gemini responseSchema format verified in Phase 12 research and source code
- TrackedChatMessage: MEDIUM -- design is straightforward but downstream consumption (Phase 14/16) is not yet defined
- Nevatars migration (MIG-03): HIGH -- source data located and structure understood; 110 pattern files with chatBurstMessages verified; migration strategy is clear
- Personality tuning: MEDIUM -- ChatBotConfig fields exist (typingSpeed, capsFrequency, emojiFrequency) but the ChatBotManager must implement the runtime behavior that uses them (message transformation based on these fields)

**Research date:** 2026-02-17
**Valid until:** 2026-03-17 (stable domain -- all dependencies are internal project code, not external libraries)
