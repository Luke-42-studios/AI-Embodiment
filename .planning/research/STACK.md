# Stack Research

**Domain:** AI-driven livestream experience (simulated chat bots, structured output, narrative steering, scene loading)
**Researched:** 2026-02-17
**Confidence:** HIGH (verified against official Gemini API docs, Unity 6 docs, and existing codebase)

---

## Recommended Stack

### Core Technologies (Already In Use -- No Changes)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Unity 6 | 6000.3.7f1 | Engine, audio pipeline, scene management, UI Toolkit | Already in use. Stable. |
| C# | 9.0 (.NET Standard 2.1) | Primary language | Confirmed via .csproj. Async/await, pattern matching, records. |
| Newtonsoft.Json | com.unity.nuget.newtonsoft-json | JSON serialization for all API communication | Already in use since v0.8. Required for Gemini REST API structured output. |
| Gemini Live (WebSocket) | v1beta BidiGenerateContent | Real-time Aya conversation with audio | Already in use. PersonaSession + GeminiLiveClient handle this. |
| Input System | com.unity.inputsystem | Keyboard push-to-talk | Already in use in AyaSampleController. |

### New Technologies for v1.0 Milestone

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Gemini REST API (generateContent) | v1beta | Chat bot structured output responses | Live API does NOT support `response_schema`. Chat bots need structured JSON output. Must use separate REST `generateContent` endpoint. |
| UnityWebRequest | Built-in | HTTP client for REST API calls | Already used for ChirpTTSClient. Main-thread safe, Unity lifecycle aware. |
| SceneManager.LoadSceneAsync | Built-in | Additive scene loading for movie clip | Standard Unity API. Load movie scene additively without unloading livestream. |
| UI Toolkit ListView | Built-in (Unity 6) | Virtualized chat feed | ScrollView degrades with 100+ elements. ListView provides element recycling via makeItem/bindItem pattern. |

### Supporting Patterns (Reuse Existing Infrastructure)

| Pattern | Where It Exists | How It's Reused |
|---------|----------------|-----------------|
| PersonaSession | Runtime/PersonaSession.cs | Aya's live conversation -- unchanged |
| ConversationalGoals | Runtime/GoalManager.cs | Time-based and user-triggered narrative steering |
| FunctionCalling | Runtime/FunctionRegistry.cs | Animation triggers (emote, start_movie, start_drawing) |
| QueuedResponse | Samples~/QueuedResponseSample/ | Finish-first push-to-talk pattern (adapt, don't copy) |
| AyaChatUI | Samples~/AyaLiveStream/ | Upgrade from ScrollView to ListView, add chat bot messages |

---

## Critical Architecture Decision: Two Gemini Paths

This milestone requires TWO distinct Gemini API paths running simultaneously:

### Path 1: Gemini Live WebSocket (Aya's Conversation)

**Already built.** PersonaSession connects via `GeminiLiveClient` to `wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent`.

- Audio-only response modality (AUDIO)
- Real-time bidirectional streaming
- Function calling for animation triggers
- Conversational goals for narrative steering
- Does NOT support `response_schema` or `response_mime_type`

### Path 2: Gemini REST generateContent (Chat Bot Responses)

**New.** A lightweight HTTP client that calls the standard `generateContent` endpoint for structured JSON output.

- Text-only (no audio)
- Request-response (not streaming)
- Structured output with JSON schema enforcement
- Used to generate chat bot persona responses
- Runs on main thread via UnityWebRequest

**Why two paths instead of one:** The Gemini Live API explicitly does not support `responseSchema`, `responseMimeType`, `responseLogprobs`, or `stopSequence` in its `BidiGenerateContentSetup`. This is confirmed in the [Live API reference](https://ai.google.dev/api/live). Chat bots need guaranteed JSON structure (bot name, message text, optional reaction type). The REST API is the only way to get schema-enforced output.

---

## Gemini REST API: Structured Output Details

### Endpoint

```
POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={API_KEY}
Content-Type: application/json
```

### Model Selection

Use `gemini-2.5-flash` for chat bot responses because:
- Cheapest model that supports structured output
- Fastest response time (chat bots should respond quickly)
- Supports `response_schema` in `generationConfig`
- Gemini 2.0 Flash is being retired March 31, 2026

**Do NOT use `gemini-2.5-flash-native-audio`** for chat bots -- that model is for the Live API audio path only.

### Request Format

```json
{
  "contents": [
    {
      "role": "user",
      "parts": [{ "text": "Generate chat bot responses for: {context}" }]
    }
  ],
  "generationConfig": {
    "response_mime_type": "application/json",
    "response_schema": {
      "type": "ARRAY",
      "items": {
        "type": "OBJECT",
        "properties": {
          "bot_name": {
            "type": "STRING",
            "description": "Name of the chat bot persona"
          },
          "message": {
            "type": "STRING",
            "description": "The chat message text"
          },
          "reaction_type": {
            "type": "STRING",
            "enum": ["none", "heart", "laugh", "wow", "sad"],
            "description": "Optional emoji reaction"
          }
        },
        "required": ["bot_name", "message"]
      }
    },
    "temperature": 0.9,
    "max_output_tokens": 256
  }
}
```

### Response Format

```json
{
  "candidates": [
    {
      "content": {
        "parts": [
          {
            "text": "[{\"bot_name\":\"artfan99\",\"message\":\"omg that shading technique is amazing!\",\"reaction_type\":\"heart\"}]"
          }
        ]
      }
    }
  ]
}
```

Parse `candidates[0].content.parts[0].text` as JSON. The structured output guarantees it matches the schema.

### Supported Schema Types

| Type | JSON Value | Notes |
|------|-----------|-------|
| STRING | `"type": "STRING"` | Supports `enum` for restricted values |
| NUMBER | `"type": "NUMBER"` | Floating point. Supports `minimum`, `maximum` |
| INTEGER | `"type": "INTEGER"` | Whole numbers |
| BOOLEAN | `"type": "BOOLEAN"` | true/false |
| OBJECT | `"type": "OBJECT"` | With `properties`, `required` |
| ARRAY | `"type": "ARRAY"` | With `items` schema |

**Limitations:**
- Schema size counts toward input token limit
- Deeply nested schemas may be rejected -- keep flat
- Not all JSON Schema features are supported (no `$ref`, no `allOf`)

### Field Name Convention

The REST API accepts **both** `snake_case` and `camelCase` field names. The official documentation uses `snake_case` in curl examples. Use `snake_case` in raw JSON to match official examples.

**Confidence:** HIGH -- verified against [Gemini API structured output docs](https://ai.google.dev/gemini-api/docs/structured-output) and [API reference](https://ai.google.dev/api/generate-content).

---

## Chat Bot Design: Single Call vs Multiple Calls

### Recommendation: Single call, array response

Generate ALL bot responses in a single `generateContent` call by requesting an array of bot message objects.

**Why single call:**
- Lower latency (one HTTP round trip vs N)
- Lower cost (one prompt evaluation vs N)
- Better coherence (bots can react to each other in context)
- Schema enforcement via `ARRAY` type with `items` schema

**How it works:**
1. System prompt describes all bot personas (name, personality, typical messages)
2. User prompt provides current context (what Aya just said, what the user said, current topic)
3. Response schema is `ARRAY` of bot message objects
4. Parse the array and drip-feed messages into the chat at randomized intervals

**Schema for multi-bot response:**

```json
{
  "type": "ARRAY",
  "items": {
    "type": "OBJECT",
    "properties": {
      "bot_name": { "type": "STRING" },
      "message": { "type": "STRING" },
      "delay_seconds": { "type": "NUMBER", "description": "Suggested delay before showing this message (0-5 seconds)" }
    },
    "required": ["bot_name", "message"]
  }
}
```

The `delay_seconds` field lets the model suggest staggering for natural chat pacing. The client can use this or override with its own timing.

---

## Chat Bot System: Scripted + Dynamic Hybrid

### Architecture

```
ChatBotManager
  |
  +-- ScriptedMessageQueue (List<ScriptedMessage>)
  |     Pre-authored messages with trigger conditions (time, event, goal state)
  |     Zero latency, zero cost, fires on schedule
  |
  +-- DynamicMessageGenerator (GeminiRestClient)
  |     Calls generateContent when user speaks or Aya says something noteworthy
  |     ~500ms-2s latency, costs API tokens per call
  |
  +-- MessageMerger
        Interleaves scripted and dynamic messages
        Enforces minimum gap between messages
        Prevents bot message spam during Aya's speech
```

### ScriptedMessage ScriptableObject

```csharp
[CreateAssetMenu(menuName = "AI Embodiment/Chat Bot/Scripted Message")]
public class ScriptedMessage : ScriptableObject
{
    public string botName;
    public string message;
    public float triggerTimeSeconds;  // -1 for event-triggered
    public string triggerEvent;       // e.g., "goal_activated", "user_spoke", "aya_mentioned_character"
}
```

### ChatBotConfig ScriptableObject

```csharp
[CreateAssetMenu(menuName = "AI Embodiment/Chat Bot/Bot Config")]
public class ChatBotConfig : ScriptableObject
{
    public string botName;
    public string personality;       // e.g., "enthusiastic art student"
    public string[] typicalPhrases;  // Seed phrases for the model
    public Color nameColor;          // Chat UI display color
}
```

---

## Unity Scene Loading: Movie Clip Trigger

### API

```csharp
using UnityEngine.SceneManagement;

// Load movie scene additively (does not unload livestream scene)
AsyncOperation op = SceneManager.LoadSceneAsync("MovieClipScene", LoadSceneMode.Additive);

// Optional: preload without activating
op.allowSceneActivation = false;
// ... later, when ready:
op.allowSceneActivation = true;

// When movie is done, unload:
SceneManager.UnloadSceneAsync("MovieClipScene");
```

### Pattern: Preload on Goal Escalation

```
1. Goal "reveal_movie" reaches HIGH priority
2. Preload movie scene (allowSceneActivation = false)
3. Aya triggers start_movie function call
4. Function handler activates scene (allowSceneActivation = true)
5. Transition UI (fade, camera switch, or overlay)
6. Movie plays via VideoPlayer or Timeline
7. On complete: unload movie scene, return to livestream
```

### Movie Clip Playback Options

| Option | Component | When to Use |
|--------|-----------|-------------|
| VideoPlayer + RenderTexture | `UnityEngine.Video.VideoPlayer` | Pre-rendered video file (MP4, WebM) |
| Timeline + Playable Director | `UnityEngine.Timeline.PlayableDirector` | Unity-rendered cinematic with cameras, animations, post-processing |
| Camera switch only | Custom script | Movie scene has its own camera, deactivate livestream camera |

**Recommendation:** Use Timeline + PlayableDirector for "Unity-rendered movie clip" since the project spec says "Unity-rendered" not pre-recorded video. The movie scene would contain a PlayableDirector with animation tracks, camera tracks, and audio tracks.

---

## Livestream UI: Chat Feed Implementation

### Current State

The existing `AyaChatUI.cs` uses a `ScrollView` with dynamically added `Label` elements. This works for the current simple transcript but will degrade with 100+ chat bot messages.

### Recommendation: Upgrade to ListView

Use UI Toolkit's `ListView` for the chat feed because:
- Virtualized element recycling (only renders visible items)
- Handles hundreds of messages without performance degradation
- makeItem/bindItem pattern matches the chat message data model

### ListView Configuration

```csharp
// In chat UI initialization
_chatListView = root.Q<ListView>("chat-feed");
_chatListView.makeItem = () => {
    var label = new Label();
    label.AddToClassList("chat-message");
    return label;
};
_chatListView.bindItem = (element, index) => {
    var msg = _messages[index];
    var label = (Label)element;
    label.text = $"{msg.SenderName}: {msg.Text}";
    label.RemoveFromClassList("msg-aya");
    label.RemoveFromClassList("msg-user");
    label.RemoveFromClassList("msg-bot");
    label.RemoveFromClassList("msg-system");
    label.AddToClassList(msg.CssClass);
};
_chatListView.itemsSource = _messages;
_chatListView.selectionType = SelectionType.None;
```

### Variable Height Caveat

ListView in Unity 6 supports `fixedItemHeight` and dynamic sizing. For chat messages with varying lengths:
- Set `ListView.fixedItemHeight = -1` (or use `virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight` if available)
- If dynamic height is not reliable, use a **capped message length** (e.g., max 140 characters per chat message) so all items are approximately the same height
- Alternative fallback: keep ScrollView but cap at ~50 messages (remove oldest when adding new), which is sufficient for a livestream chat that scrolls fast

### Chat Message Data Model

```csharp
public enum ChatMessageType { Aya, User, Bot, System }

public class ChatMessage
{
    public string SenderName;
    public string Text;
    public ChatMessageType Type;
    public float Timestamp;

    public string CssClass => Type switch
    {
        ChatMessageType.Aya => "msg-aya",
        ChatMessageType.User => "msg-user",
        ChatMessageType.Bot => "msg-bot",
        ChatMessageType.System => "msg-system",
        _ => "msg-system"
    };
}
```

### UI Layout (UXML)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="root" class="root">
    <!-- Stream viewport (where Aya's 3D character renders) -->
    <ui:VisualElement name="stream-viewport" class="viewport" />

    <!-- Chat panel (right side or overlay) -->
    <ui:VisualElement name="chat-panel" class="chat-panel">
      <ui:VisualElement name="header" class="header">
        <ui:VisualElement name="live-indicator" class="live-dot" />
        <ui:Label name="stream-title" text="Aya's Art Stream" class="title" />
      </ui:VisualElement>

      <!-- Aya transcript (what she's currently saying) -->
      <ui:Label name="aya-transcript" class="transcript" />

      <!-- Chat feed (bot messages + user messages) -->
      <ui:ListView name="chat-feed" class="chat-feed" />

      <!-- Footer: PTT + status -->
      <ui:VisualElement name="footer" class="footer">
        <ui:Label name="status-label" text="Hold SPACE to talk" class="status" />
        <ui:Button name="ptt-button" text="Push to Talk" class="ptt-btn" />
      </ui:VisualElement>
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

---

## GeminiRestClient: New Component

A lightweight REST client for `generateContent` requests. Separate from `GeminiLiveClient` (WebSocket).

### API Surface

```csharp
public class GeminiRestClient
{
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiRestClient(string apiKey, string model = "gemini-2.5-flash")
    {
        _apiKey = apiKey;
        _model = model;
    }

    /// <summary>
    /// Sends a generateContent request with structured output schema.
    /// Returns the parsed text from the first candidate.
    /// </summary>
    public async Task<string> GenerateStructuredAsync(
        string systemPrompt,
        string userPrompt,
        JObject responseSchema,
        float temperature = 0.9f,
        int maxTokens = 256)
    {
        // Build request body
        // POST via UnityWebRequest
        // Parse candidates[0].content.parts[0].text
        // Return raw JSON string for caller to deserialize
    }
}
```

### Implementation Pattern

Use `UnityWebRequest.Post` with raw JSON body. The method returns `Task<string>` using `SendWebRequest()` with a `TaskCompletionSource` wrapper:

```csharp
private Task<string> PostAsync(string url, string jsonBody)
{
    var tcs = new TaskCompletionSource<string>();
    var request = new UnityWebRequest(url, "POST");
    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
    request.downloadHandler = new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type", "application/json");

    var op = request.SendWebRequest();
    op.completed += _ =>
    {
        if (request.result == UnityWebRequest.Result.Success)
            tcs.SetResult(request.downloadHandler.text);
        else
            tcs.SetException(new Exception(request.error));
        request.Dispose();
    };
    return tcs.Task;
}
```

**This pattern is already proven** in `ChirpTTSClient.cs` for TTS HTTP calls. The same `UnityWebRequest` async pattern applies.

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Bot responses | Gemini REST generateContent | Live API text-only session | Live API has no `response_schema`. Cannot guarantee JSON structure. Would need prompt-only enforcement which is unreliable. |
| Bot responses | Single call, array schema | Multiple calls per bot | N calls = N times latency and cost. Single call with array schema is cheaper and more coherent. |
| Bot responses | Gemini 2.5 Flash | Gemini 2.5 Pro | Flash is sufficient for short chat messages. Pro is more expensive and slower for this use case. |
| Chat feed UI | ListView (virtualized) | ScrollView (current) | ScrollView adds all elements to DOM. At 100+ messages it degrades. ListView recycles visible elements only. |
| Chat feed UI | ListView | IMGUI / Canvas-based UI | Project already uses UI Toolkit. Switching to Canvas adds a dependency and inconsistency. |
| Scene loading | LoadSceneAsync Additive | Single scene with enable/disable | Additive loading is cleaner: movie scene is self-contained, can be preloaded, and unloading is automatic. Enable/disable requires all movie objects in the main scene. |
| Movie playback | Timeline + PlayableDirector | VideoPlayer | Project spec says "Unity-rendered movie clip" -- Timeline is for authored cinematics. VideoPlayer is for pre-recorded video files. |
| Structured output | response_schema enforcement | Prompt-only JSON instruction | Schema enforcement guarantees valid JSON structure. Prompt-only fails ~5-15% of the time with malformed JSON. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Gemini Live API for chat bots | Does not support `response_schema`, `response_mime_type`. No structured output. | Gemini REST `generateContent` with `response_schema` |
| `responseJsonSchema` field name | This appears in some docs but is NOT the correct REST field name | `response_schema` (snake_case) in the REST JSON body |
| Firebase AI Logic SDK | Removed in v0.8. Zero Firebase dependency is a project constraint. | Direct WebSocket (GeminiLiveClient) + direct REST (GeminiRestClient) |
| System.Net.Http.HttpClient | Does not respect Unity lifecycle. Can cause issues on IL2CPP/mobile. | UnityWebRequest |
| Gemini 2.0 Flash for REST calls | Being retired March 31, 2026 | gemini-2.5-flash |
| ScrollView for 100+ chat messages | No virtualization, all elements in DOM, degrades at scale | ListView with makeItem/bindItem |
| Multiple PersonaSession instances for bots | Each session opens a WebSocket. Bots don't need real-time audio. Wasteful. | Single REST client shared across all bot persona generation |
| UGemini Unity package | Third-party dependency. Our REST needs are simple (one endpoint, one schema). | Custom GeminiRestClient (~100 lines) |
| Batch API for bot responses | 24-hour turnaround. Chat bots need responses in <2 seconds. | Standard generateContent (synchronous, immediate response) |

---

## Version Compatibility

| Component | Version | Compatible With | Notes |
|-----------|---------|-----------------|-------|
| gemini-2.5-flash | Current stable | generateContent + response_schema | Structured output confirmed supported |
| gemini-2.5-flash-native-audio | Preview | Live API (BidiGenerateContent) only | Used for Aya's voice session. Does NOT support REST generateContent. |
| Unity 6 | 6000.3.7f1 | SceneManager.LoadSceneAsync, ListView, UI Toolkit | All APIs stable in Unity 6 |
| Newtonsoft.Json | com.unity.nuget.newtonsoft-json | JObject for REST request/response building | Already imported |
| Input System | com.unity.inputsystem | Keyboard push-to-talk | Already in use |

---

## Implementation Sizing

| New Component | Estimated Lines | Complexity | Notes |
|---------------|----------------|------------|-------|
| GeminiRestClient | ~100-150 | Low | UnityWebRequest POST + JSON build/parse. Pattern from ChirpTTSClient. |
| ChatBotManager | ~200-300 | Medium | Scripted queue + dynamic generator + message merging + timing |
| ChatBotConfig (ScriptableObject) | ~30 | Low | Bot name, personality, color, scripted lines |
| LivestreamUI (upgraded AyaChatUI) | ~250-350 | Medium | ListView chat feed, transcript panel, live indicator, PTT |
| NarrativeDirector | ~150-250 | Medium | Time-based goal escalation, event routing, scene load trigger |
| MovieSceneController | ~80-120 | Low | Scene load/unload, playback monitoring, return-to-stream |

**Total new code estimate:** ~800-1,200 lines of C#

---

## Sources

- [Gemini API Structured Output docs](https://ai.google.dev/gemini-api/docs/structured-output) -- response_schema format, supported types, limitations (HIGH confidence)
- [Gemini Live API reference](https://ai.google.dev/api/live) -- confirmed response_schema NOT supported in Live API (HIGH confidence)
- [Gemini API GenerationConfig reference](https://ai.google.dev/api/generate-content) -- exact field names: response_schema, response_mime_type (HIGH confidence)
- [Gemini models page](https://ai.google.dev/gemini-api/docs/models) -- gemini-2.5-flash supports structured output (HIGH confidence)
- [Unity 6 SceneManager.LoadSceneAsync](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) -- additive scene loading API (HIGH confidence)
- [Unity 6 ListView manual](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-uxml-element-ListView.html) -- virtualization, makeItem/bindItem (HIGH confidence)
- Existing codebase: PersonaSession.cs, GeminiLiveClient.cs, ChirpTTSClient.cs, AyaChatUI.cs, AyaSampleController.cs -- direct source code inspection (HIGH confidence)
- [Unity VideoPlayer reference](https://docs.unity3d.com/Manual/class-VideoPlayer.html) -- render targets, API-only mode (MEDIUM confidence -- project may use Timeline instead)

---

*Stack research for: AI Embodiment v1.0 Livestream Experience*
*Researched: 2026-02-17*
