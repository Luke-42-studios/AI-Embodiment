# Phase 12: Foundation & Data Model - Research

**Researched:** 2026-02-17
**Domain:** Gemini REST API structured output, Unity UI Toolkit ListView, ScriptableObject data model, data migration
**Confidence:** HIGH

## Summary

Phase 12 establishes three foundational components: (1) a GeminiTextClient REST wrapper for structured JSON output from gemini-2.5-flash, (2) a livestream UI shell with ListView-based chat feed, and (3) ChatBotConfig ScriptableObjects populated from migrated nevatars character data.

The codebase already contains a proven pattern for REST API calls in `ChirpTTSClient.cs` -- using `UnityWebRequest` with Unity 6's `Awaitable` return type, Newtonsoft.Json for serialization, and API key auth via `x-goog-api-key` header. The GeminiTextClient should follow this exact pattern. The existing `AyaChatUI.cs` uses `ScrollView` for the Aya chat log, but the livestream chat feed requires `ListView` with `DynamicHeight` virtualization for performance with potentially hundreds of bot messages. The `ChatBotConfig` ScriptableObject is a new type that lives in the sample layer alongside existing `PersonaConfig` assets.

**Primary recommendation:** Follow the ChirpTTSClient pattern exactly for the REST client, use ListView (not ScrollView) with DynamicHeight for the chat feed, and create ChatBotConfig as a new ScriptableObject type in the `AIEmbodiment.Samples` namespace.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| UnityWebRequest | Unity 6000.3.7f1 built-in | HTTP REST calls to Gemini API | Already used by ChirpTTSClient; runs on main thread; supports Awaitable |
| Newtonsoft.Json | 3.2.1 (via com.unity.nuget.newtonsoft-json) | JSON serialization/deserialization | Already a package dependency; used throughout GeminiLiveClient, ChirpTTSClient, PersonaSession |
| Unity UI Toolkit | Unity 6000.3.7f1 built-in (com.unity.modules.uielements) | UXML/USS layout, ListView, runtime UI | Already used by AyaChatUI, QueuedResponseUI; project standard for all UI |
| ScriptableObject | Unity built-in | ChatBotConfig data assets | Already used for PersonaConfig, AIEmbodimentSettings; project standard for config data |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Unity Input System | 1.18.0 | Keyboard input for push-to-talk | Already referenced by sample asmdefs; used for space/enter key detection |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| UnityWebRequest | System.Net.Http.HttpClient | HttpClient is more standard .NET but requires thread marshaling; UnityWebRequest integrates with Unity's Awaitable natively and is already proven in ChirpTTSClient |
| Newtonsoft.Json | JsonUtility | JsonUtility is faster but cannot handle dynamic schemas, JObject manipulation, or polymorphic types needed for Gemini API request/response |
| ListView | ScrollView | ScrollView is simpler (already used in AyaChatUI) but does not virtualize -- performance degrades with 100+ messages. ListView with DynamicHeight recycles elements |

**Installation:**
No new packages needed. All dependencies are already present:
- `com.unity.nuget.newtonsoft-json` 3.2.1 (transitive via `com.google.ai-embodiment`)
- `com.unity.modules.uielements` (built-in)
- `com.unity.modules.unitywebrequest` (built-in)

## Architecture Patterns

### Recommended Project Structure
```
Assets/
  AyaLiveStream/                    # Existing sample directory
    AyaChatUI.cs                    # Existing -- Aya transcript panel (keep as-is)
    AyaSampleController.cs          # Existing -- main controller (keep as-is)
    AyaLiveStream.asmdef            # Existing -- add no new references needed
    AyaPersonaConfig.asset          # Existing -- Aya's PersonaConfig
    GeminiTextClient.cs             # NEW -- REST wrapper for structured output
    LivestreamUI.cs                 # NEW -- chat feed + stream status controller
    ChatBotConfig.cs                # NEW -- ScriptableObject definition
    ChatMessage.cs                  # NEW -- data model for chat messages
    UI/
      AyaPanel.uxml                 # Existing -- Aya's simple chat panel
      AyaPanel.uss                  # Existing -- Aya's styles
      LivestreamPanel.uxml          # NEW -- full livestream layout with ListView
      LivestreamPanel.uss           # NEW -- livestream styles
    ChatBotConfigs/                  # NEW -- migrated character ScriptableObjects
      Dad_JohnConfig.asset          # NEW -- migrated from nevatars
      TeenFan_MikoConfig.asset      # NEW -- migrated from nevatars
      ...                           # one .asset per character
```

### Pattern 1: REST Client (follow ChirpTTSClient)
**What:** Plain C# class (not MonoBehaviour) that wraps UnityWebRequest for Gemini REST API
**When to use:** Any non-streaming HTTP call to Google AI APIs
**Example:**
```csharp
// Source: Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs (lines 100-161)
// Exact pattern already proven in this codebase
public class GeminiTextClient : IDisposable
{
    private const string ENDPOINT =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    private readonly string _apiKey;
    private bool _disposed;

    public GeminiTextClient(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    /// <summary>
    /// Sends a prompt with a response schema and returns the structured JSON.
    /// MUST be called from the main thread (UnityWebRequest requirement).
    /// </summary>
    public async Awaitable<T> GenerateAsync<T>(string prompt, JObject responseSchema)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GeminiTextClient));

        var requestBody = new JObject
        {
            ["contents"] = new JArray
            {
                new JObject
                {
                    ["parts"] = new JArray { new JObject { ["text"] = prompt } }
                }
            },
            ["generationConfig"] = new JObject
            {
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = responseSchema
            }
        };

        byte[] bodyBytes = Encoding.UTF8.GetBytes(requestBody.ToString(Formatting.None));

        using var request = new UnityWebRequest(ENDPOINT, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-goog-api-key", _apiKey);

        await request.SendWebRequest();

        if (_disposed) return default;

        if (request.result != UnityWebRequest.Result.Success)
            throw new Exception($"Gemini REST failed: {request.error}\n{request.downloadHandler?.text}");

        // Parse response: candidates[0].content.parts[0].text contains the JSON
        var response = JObject.Parse(request.downloadHandler.text);
        string jsonText = response["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

        return JsonConvert.DeserializeObject<T>(jsonText);
    }

    public void Dispose() { _disposed = true; }
}
```

### Pattern 2: ListView Chat Feed with DynamicHeight
**What:** UI Toolkit ListView with virtualization for performant chat message display
**When to use:** Any scrollable list with many items of variable height
**Example:**
```csharp
// Source: Unity 6 docs + verified against existing AyaChatUI.cs patterns
public class LivestreamUI : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private ListView _chatFeed;
    private readonly List<ChatMessage> _messages = new List<ChatMessage>();

    private void OnEnable()
    {
        var root = _uiDocument.rootVisualElement;
        _chatFeed = root.Q<ListView>("chat-feed");

        _chatFeed.makeItem = MakeChatItem;
        _chatFeed.bindItem = BindChatItem;
        _chatFeed.itemsSource = _messages;
        _chatFeed.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
        _chatFeed.selectionType = SelectionType.None;
    }

    private VisualElement MakeChatItem()
    {
        var container = new VisualElement();
        container.AddToClassList("chat-message");
        var nameLabel = new Label();
        nameLabel.name = "bot-name";
        nameLabel.AddToClassList("bot-name");
        var textLabel = new Label();
        textLabel.name = "message-text";
        textLabel.AddToClassList("message-text");
        textLabel.style.whiteSpace = WhiteSpace.Normal; // enable word wrap
        var timestampLabel = new Label();
        timestampLabel.name = "timestamp";
        timestampLabel.AddToClassList("timestamp");
        container.Add(nameLabel);
        container.Add(textLabel);
        container.Add(timestampLabel);
        return container;
    }

    private void BindChatItem(VisualElement element, int index)
    {
        var msg = _messages[index];
        var nameLabel = element.Q<Label>("bot-name");
        var textLabel = element.Q<Label>("message-text");
        var timestampLabel = element.Q<Label>("timestamp");

        nameLabel.text = msg.BotName;
        nameLabel.style.color = msg.BotColor;
        textLabel.text = msg.Text;
        timestampLabel.text = msg.Timestamp;
    }

    public void AddMessage(ChatMessage msg)
    {
        _messages.Add(msg);
        _chatFeed.RefreshItems();
        // Scroll to bottom
        _chatFeed.schedule.Execute(() =>
        {
            _chatFeed.ScrollToItem(_messages.Count - 1);
        });
    }
}
```

### Pattern 3: ChatBotConfig ScriptableObject
**What:** ScriptableObject for per-bot configuration, following existing PersonaConfig pattern
**When to use:** Any developer-editable bot configuration
**Example:**
```csharp
// Source: modeled on PersonaConfig.cs (Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs)
[CreateAssetMenu(fileName = "NewChatBotConfig", menuName = "AI Embodiment/Samples/Chat Bot Config")]
public class ChatBotConfig : ScriptableObject
{
    [Header("Identity")]
    public string botName = "New Bot";
    public Color chatColor = Color.white;

    [Header("Personality")]
    [TextArea(3, 8)]
    public string personality;
    public string[] speechTraits;

    [Header("Scripted Messages")]
    [TextArea(1, 3)]
    public string[] scriptedMessages;

    [Header("Behavior")]
    [Range(0.5f, 5f)]
    public float typingSpeed = 1.5f;

    [Range(0f, 1f)]
    public float capsFrequency = 0.1f;

    [Range(0f, 1f)]
    public float emojiFrequency = 0.2f;
}
```

### Pattern 4: ChatMessage Data Model
**What:** Plain C# class (not MonoBehaviour) for individual chat messages
**When to use:** Data flowing between bot system and UI
**Example:**
```csharp
// Lightweight data container -- no Unity dependencies
public class ChatMessage
{
    public string BotName;
    public Color BotColor;
    public string Text;
    public string Timestamp;
    public bool IsFromUser;

    // Reference back to config for downstream systems (Phase 13: TrackedChatMessage)
    public ChatBotConfig Source;
}
```

### Anti-Patterns to Avoid
- **Using ScrollView for chat feed:** The existing AyaChatUI uses ScrollView. Do NOT copy this pattern for the livestream chat feed. ScrollView creates a new Label per message and never recycles -- it will degrade with 100+ messages. Use ListView with DynamicHeight instead.
- **Putting GeminiTextClient in the package runtime:** The constraint is "zero package modifications." GeminiTextClient goes in `Assets/AyaLiveStream/`, not in `Packages/com.google.ai-embodiment/Runtime/`.
- **Using `responseJsonSchema` instead of `responseSchema`:** The Gemini API supports both field names. `responseJsonSchema` is for advanced JSON Schema features ($ref, $defs, anyOf). Use the simpler `responseSchema` (OpenAPI-style) since bot message schemas are straightforward.
- **Creating MonoBehaviour for GeminiTextClient:** Follow the ChirpTTSClient pattern -- plain C# class with IDisposable. The caller (controller MonoBehaviour) manages lifetime.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization | Custom string formatting | Newtonsoft.Json JObject/JsonConvert | Already a dependency; handles escaping, nested objects, type conversion correctly |
| HTTP requests | System.Net.Http or custom WebSocket | UnityWebRequest with Awaitable | Unity main-thread requirement; proven pattern in ChirpTTSClient |
| Virtualized scrolling list | Custom recycling with ScrollView | ListView with DynamicHeight | Unity's built-in virtualization handles element recycling, layout calculation |
| API key management | Hardcoded strings or env vars | AIEmbodimentSettings.Instance.ApiKey | Already exists in Resources; singleton pattern proven |
| ScriptableObject creation in editor | Manual .asset file writing | AssetDatabase.CreateAsset + EditorUtility.SetDirty | Unity's standard approach; handles YAML serialization, .meta files, GUIDs |

**Key insight:** This phase is almost entirely wiring -- connecting existing Gemini APIs to existing Unity UI patterns using existing project patterns. The ChirpTTSClient is the rosetta stone for how REST calls work in this codebase.

## Common Pitfalls

### Pitfall 1: UnityWebRequest Must Run on Main Thread
**What goes wrong:** Calling `GeminiTextClient.GenerateAsync()` from a background thread causes a Unity exception.
**Why it happens:** UnityWebRequest is not thread-safe and must be created/sent on the main thread.
**How to avoid:** The `Awaitable` return type naturally keeps execution on the main thread. Never use `Task.Run()` to call the client.
**Warning signs:** `InvalidOperationException` mentioning thread context.

### Pitfall 2: Gemini Response Structure Requires Deep Traversal
**What goes wrong:** Trying to parse the root response as the structured data directly.
**Why it happens:** The Gemini API wraps the structured JSON inside `candidates[0].content.parts[0].text` as a string, not as a parsed object.
**How to avoid:** Always extract the text field first, then deserialize: `JObject.Parse(response)["candidates"][0]["content"]["parts"][0]["text"]` yields a string that must be passed through `JsonConvert.DeserializeObject<T>()`.
**Warning signs:** Deserialization returns null or throws because the root object has `candidates`, not your schema fields.

### Pitfall 3: ListView DynamicHeight Performance with RefreshItems
**What goes wrong:** Calling `RefreshItems()` on every message add causes visible stuttering with many items.
**Why it happens:** DynamicHeight virtualization calls `bindItem` for all visible items on refresh, and must recalculate heights.
**How to avoid:** Use `RefreshItems()` (not `Rebuild()`). For streaming scenarios, batch multiple message adds and refresh once. The chat bot system posts messages with 0.8-3.0s delays, so single-message RefreshItems is acceptable.
**Warning signs:** Frame drops when adding messages rapidly.

### Pitfall 4: ListView ScrollToItem Requires Deferred Execution
**What goes wrong:** Calling `ScrollToItem()` immediately after adding an item scrolls to the wrong position.
**Why it happens:** Layout has not been recalculated yet when the item is added.
**How to avoid:** Use `schedule.Execute()` to defer the scroll, exactly as AyaChatUI does with `AutoScroll()`.
**Warning signs:** Chat feed scrolls to second-to-last item instead of the latest.

### Pitfall 5: responseSchema Type Case Matters
**What goes wrong:** Using lowercase type names in `responseSchema` (e.g., `"type": "string"`) causes 400 errors.
**Why it happens:** The Google AI `v1beta` endpoint's `responseSchema` field uses OpenAPI-style UPPERCASE type constants: `STRING`, `OBJECT`, `ARRAY`, `BOOLEAN`, `NUMBER`, `INTEGER`. The newer `responseJsonSchema` field uses lowercase JSON Schema types. These are two different field names with different type conventions.
**How to avoid:** Use `responseSchema` with UPPERCASE types consistently: `"type": "OBJECT"`, `"type": "STRING"`, `"type": "ARRAY"`.
**Warning signs:** 400 Bad Request from the Gemini API with unhelpful error messages.

### Pitfall 6: ScriptableObject Assets Need EditorUtility.SetDirty
**What goes wrong:** Programmatically creating or modifying ScriptableObject assets in an editor script, but changes are not saved to disk.
**Why it happens:** Unity's serialization system only saves assets marked as dirty.
**How to avoid:** After creating or modifying an asset, call `EditorUtility.SetDirty(asset)` followed by `AssetDatabase.SaveAssets()`.
**Warning signs:** Assets revert to default values on next editor restart.

### Pitfall 7: nevatars Data May Not Exist as Serialized Assets
**What goes wrong:** Writing a migration script that expects to load StreamingCharacter ScriptableObjects, but the nevatars data may only exist as design documentation or external data.
**Why it happens:** The nevatars project is the predecessor to this project, but its data may not be in the current repository.
**How to avoid:** The migration script should be a one-time editor menu item that creates ChatBotConfig assets from hardcoded data (character names, colors, personalities) extracted from the nevatars design. If source assets exist, load them; if not, define the data inline in the migration script.
**Warning signs:** `Resources.Load` or `AssetDatabase.LoadAssetAtPath` returning null for nevatars paths.

## Code Examples

Verified patterns from the existing codebase:

### REST API Call Pattern (from ChirpTTSClient)
```csharp
// Source: Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs lines 100-161
// This is the PROVEN pattern in this codebase for REST calls

public async Awaitable<TTSResult> SynthesizeAsync(string text, string voiceName, string languageCode, ...)
{
    if (_disposed) throw new ObjectDisposedException(nameof(ChirpTTSClient));

    string json = BuildRequestJson(text, voiceName, languageCode, _voiceCloningKey);
    byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

    using var request = new UnityWebRequest(endpoint, "POST");
    request.uploadHandler = new UploadHandlerRaw(bodyBytes);
    request.downloadHandler = new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
    request.SetRequestHeader("x-goog-api-key", _apiKey);

    await request.SendWebRequest();

    if (_disposed) return default;

    if (request.result != UnityWebRequest.Result.Success)
        throw new Exception($"Request failed: {request.error}\n{request.downloadHandler?.text}");

    string responseJson = request.downloadHandler.text;
    // ... parse response
}
```

### UI Toolkit UXML Element Binding (from AyaChatUI)
```csharp
// Source: Assets/AyaLiveStream/AyaChatUI.cs lines 27-48
private void OnEnable()
{
    var root = _uiDocument.rootVisualElement;
    _chatLog = root.Q<ScrollView>("chat-log");
    _nameLabel = root.Q<Label>("persona-name");
    _statusLabel = root.Q<Label>("status-label");
    // ... subscribe to events
}
```

### Auto-Scroll Pattern (from AyaChatUI)
```csharp
// Source: Assets/AyaLiveStream/AyaChatUI.cs lines 169-175
private void AutoScroll()
{
    _chatLog.schedule.Execute(() =>
    {
        _chatLog.scrollOffset = new Vector2(0, _chatLog.contentContainer.layout.height);
    });
}
```

### ScriptableObject Definition Pattern (from PersonaConfig)
```csharp
// Source: Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs
[CreateAssetMenu(fileName = "NewPersonaConfig", menuName = "AI Embodiment/Persona Config")]
public class PersonaConfig : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "New Persona";

    [TextArea(3, 10)]
    public string backstory;

    [Header("Personality")]
    public string[] personalityTraits;
}
```

### API Key Access Pattern (from PersonaSession)
```csharp
// Source: Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs lines 148-153
var settings = AIEmbodimentSettings.Instance;
if (settings == null || string.IsNullOrEmpty(settings.ApiKey))
{
    Debug.LogError("No API key configured. Create an AIEmbodimentSettings asset.");
    return;
}
```

## Gemini REST API: Structured Output Reference

### Endpoint
```
POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent
```

### Headers
```
Content-Type: application/json
x-goog-api-key: {API_KEY}
```

### Request Body (for bot chat messages)
```json
{
  "contents": [
    {
      "parts": [
        { "text": "Generate 3 chat messages from these bots reacting to: '{context}'" }
      ]
    }
  ],
  "generationConfig": {
    "responseMimeType": "application/json",
    "responseSchema": {
      "type": "ARRAY",
      "items": {
        "type": "OBJECT",
        "properties": {
          "botName": { "type": "STRING" },
          "message": { "type": "STRING" },
          "emotion": { "type": "STRING" }
        },
        "required": ["botName", "message"]
      }
    }
  }
}
```

### Response Structure
```json
{
  "candidates": [
    {
      "content": {
        "parts": [
          {
            "text": "[{\"botName\":\"Miko\",\"message\":\"omg thats so cool!!\",\"emotion\":\"excited\"}]"
          }
        ]
      },
      "finishReason": "STOP"
    }
  ],
  "usageMetadata": {
    "promptTokenCount": 42,
    "candidatesTokenCount": 68,
    "totalTokenCount": 110
  }
}
```

**Critical:** The `text` field contains a JSON *string*, not a parsed object. Must be deserialized separately.

### Supported Models for Structured Output
- gemini-2.5-flash (confirmed, HIGH confidence)
- gemini-2.5-pro (confirmed)
- gemini-2.0-flash (confirmed)

## ListView UXML Reference

### UXML for Chat Feed
```xml
<ui:ListView name="chat-feed"
    virtualization-method="DynamicHeight"
    fixed-item-height="40"
    selection-type="None"
    show-alternating-row-backgrounds="None"
    show-border="false"
    reorderable="false"
    class="chat-feed" />
```

**Notes:**
- `virtualization-method="DynamicHeight"` allows variable height chat messages
- `fixed-item-height="40"` is used as an initial estimate only (not a constraint) in DynamicHeight mode
- `selection-type="None"` since chat messages are display-only
- `reorderable="false"` since message order is chronological

### Scroll-to-Bottom for ListView
```csharp
// Use ScrollToItem with deferred execution
_chatFeed.schedule.Execute(() => _chatFeed.ScrollToItem(_messages.Count - 1));
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ScrollView + manual Labels | ListView with DynamicHeight | Unity 2022+ | Virtualization prevents memory growth with many messages |
| Coroutine + UnityWebRequest | Awaitable + UnityWebRequest | Unity 6 (6000.0) | Cleaner async code; no coroutine boilerplate |
| JsonUtility for API responses | Newtonsoft.Json JObject | Project decision (v0.8) | Handles dynamic schemas, nested objects, polymorphism |
| `responseJsonSchema` (lowercase types) | `responseSchema` (UPPERCASE types) | Gemini API -- both exist | `responseSchema` is simpler for straightforward schemas |

**Deprecated/outdated:**
- `www` class: replaced by `UnityWebRequest` long ago
- `StartCoroutine` + `yield return`: still works but `Awaitable` is preferred in Unity 6
- Firebase AI Logic SDK: was in `Assets/Firebase/` in earlier project versions, now removed. Project uses custom `GeminiLiveClient` in the package runtime.

## Key Constraints

1. **Zero package modifications:** All new code goes in `Assets/AyaLiveStream/`. The `Packages/com.google.ai-embodiment/` directory is read-only for this phase.

2. **Assembly definition boundaries:** The `AyaLiveStream.asmdef` references `com.google.ai-embodiment` and `Unity.InputSystem`. New files in `Assets/AyaLiveStream/` automatically belong to this assembly. If ChatBotConfig needs to be accessible from the package, it cannot be -- it stays in the sample layer.

3. **API key reuse:** GeminiTextClient reuses the same API key from `AIEmbodimentSettings.Instance.ApiKey`. No new credentials infrastructure needed.

4. **Namespace:** All new types go in `AIEmbodiment.Samples` (matching existing AyaChatUI, AyaSampleController).

5. **nevatars migration:** The StreamingCharacter assets from the nevatars project are not present in this repository. Migration means creating new ChatBotConfig assets with character data extracted from design documents or hardcoded in a one-time editor script.

## Open Questions

Things that could not be fully resolved:

1. **Exact nevatars character data**
   - What we know: The requirements reference "StreamingCharacter ScriptableObjects from nevatars" with names, colors, and personalities to preserve.
   - What is unclear: The actual nevatars character data is not in this repository. We do not know the exact number of characters, their names, colors, or personality descriptions.
   - Recommendation: The planning phase should define a set of representative bot characters (e.g., 5-8 bots with distinct personalities like "enthusiastic teen fan", "chill dad", "art student", "first-time viewer") if the original nevatars data cannot be sourced. The migration script should be structured so characters can be added later.

2. **ListView DynamicHeight reliability in Unity 6000.3.7f1**
   - What we know: Community reports indicate DynamicHeight has had bugs in various Unity versions (selection issues, layout miscalculation). Unity 6 should have the most fixes.
   - What is unclear: Whether 6000.3.7f1 specifically has resolved all DynamicHeight issues.
   - Recommendation: Implement with DynamicHeight but prepare a fallback: if DynamicHeight proves buggy, switch to FixedHeight with a generous item height (120px) that accommodates most messages. This matches the blocker noted in the phase context: "ListView dynamic height for variable-length chat messages may need fallback."

3. **Gemini API rate limits for gemini-2.5-flash REST**
   - What we know: The API has per-project rate limits, but they are not documented in the public docs.
   - What is unclear: Whether the free tier rate limit is sufficient for the chat bot burst pattern (multiple requests in quick succession during a burst).
   - Recommendation: Implement request queuing in GeminiTextClient with a minimum interval between requests (e.g., 500ms). The hybrid approach (scripted + dynamic) means most messages are scripted and do not hit the API.

## Sources

### Primary (HIGH confidence)
- `Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs` -- REST API call pattern with UnityWebRequest + Awaitable
- `Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs` -- ScriptableObject definition pattern
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` -- API key access, event subscription patterns
- `Assets/AyaLiveStream/AyaChatUI.cs` -- UI Toolkit binding, auto-scroll pattern
- `Assets/AyaLiveStream/AyaLiveStream.asmdef` -- assembly definition references
- [Gemini API generateContent reference](https://ai.google.dev/api/generate-content) -- REST endpoint format, GenerationConfig fields
- [Gemini API structured output docs](https://ai.google.dev/gemini-api/docs/structured-output) -- responseSchema format, supported models

### Secondary (MEDIUM confidence)
- [Unity 6 ListView manual](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-uxml-element-ListView.html) -- DynamicHeight, makeItem/bindItem, UXML attributes
- [Unity 6 Awaitable introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/async-awaitable-introduction.html) -- Awaitable pooling caveat, thread model
- [Google structured output blog](https://blog.google/technology/developers/gemini-api-structured-outputs/) -- responseJsonSchema vs responseSchema distinction

### Tertiary (LOW confidence)
- [Unity Discussions: ListView DynamicHeight issues](https://discussions.unity.com/t/dynamicheight-virtualization-method-on-listview-doesnt-virtualize-anything/881093) -- community reports of DynamicHeight bugs (may be fixed in Unity 6)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries already in the project; no new dependencies
- Architecture: HIGH -- patterns copied directly from existing codebase files (ChirpTTSClient, AyaChatUI, PersonaConfig)
- REST API format: HIGH -- verified against official Gemini API docs; two field name options confirmed (responseSchema vs responseJsonSchema)
- ListView DynamicHeight: MEDIUM -- official docs confirm the feature exists; community reports suggest potential bugs but Unity 6 should be more stable
- nevatars migration: LOW -- source data not found in repository; character details must be defined at implementation time

**Research date:** 2026-02-17
**Valid until:** 2026-03-17 (stable domain -- Unity 6 and Gemini API are not expected to change significantly)
