# Phase 4: Function Calling and Conversational Goals - Research

**Researched:** 2026-02-05
**Domain:** Firebase AI function calling, Gemini Live API tool protocol, system instruction injection
**Confidence:** HIGH

## Summary

Phase 4 adds two complementary features to PersonaSession: (1) a function calling registry that lets developers declare functions with schemas and C# handlers that fire synchronized with the audio/text timeline, and (2) a conversational goals system that injects urgency-framed goal text into system instructions.

The Firebase AI SDK already provides all the low-level primitives needed. `FunctionDeclaration` + `Schema` handle function schema definition. `Tool` wraps declarations for the setup message. `LiveSession.SendAsync` with `ModelContent.FunctionResponse` sends responses back. `LiveSessionToolCall` and `LiveSessionToolCallCancellation` are already parsed by the SDK. The existing `ProcessResponse` in PersonaSession already routes `LiveSessionToolCall` to `PacketAssembler.AddFunctionCall`, creating FunctionCall-type SyncPackets. What Phase 4 must add is: (a) the registration layer between developer code and Firebase types, (b) handler dispatch when SyncPackets arrive, (c) automatic response sending back to the model, and (d) the goals system that composes updated system instructions and sends them mid-session.

A critical discovery: the Gemini Live API supports mid-session system instruction updates via `clientContent` with `role: "system"`. This means goals can be added/removed/reprioritized at runtime and the updated instruction takes effect immediately without reconnection -- exactly matching the CONTEXT.md requirement. Tools (function declarations), however, are fixed at session setup and cannot be changed mid-session.

**Primary recommendation:** Build a `FunctionRegistry` class that maps function names to `FunctionDeclaration` + handler delegate pairs, registered before `Connect()`. Build a `GoalManager` class that maintains the goal list and composes system instruction text. Wire both into PersonaSession: registry produces `Tool[]` for the setup message, and goal changes trigger immediate system instruction updates via `SendAsync` with role "system".

## Standard Stack

The established libraries/tools for this domain:

### Core (All from Firebase AI SDK -- already imported)
| Type | Name | Purpose | Why Standard |
|------|------|---------|--------------|
| struct | `FunctionDeclaration` | Declares function schema (name, description, parameters) | Firebase SDK type; serializes to correct Gemini wire format |
| class | `Schema` | Defines parameter types (String, Int, Float, Boolean, Array, Object, Enum, AnyOf) | Firebase SDK type; full JSON Schema subset matching Gemini spec |
| struct | `Tool` | Wraps `FunctionDeclaration[]` for model setup | Firebase SDK type; passed to `GetLiveModel` |
| struct | `ModelContent.FunctionCallPart` | Received function call (Name, Args, Id) | Firebase SDK type; parsed from `LiveSessionToolCall` |
| struct | `ModelContent.FunctionResponsePart` | Function result sent back to model (Name, Response, Id) | Firebase SDK type; serialized as `toolResponse` wire message |
| static | `ModelContent.FunctionResponse(name, response, id)` | Factory for creating response content | Firebase SDK factory; creates correctly-formed response |
| struct | `LiveSessionToolCall` | Server message containing function call batch | Firebase SDK type; already handled in ProcessResponse |
| struct | `LiveSessionToolCallCancellation` | Server message cancelling pending function calls | Firebase SDK type; NOT yet handled in ProcessResponse |
| struct | `FunctionCallingConfig` | Controls function calling mode (Auto, Any, None) | Firebase SDK type; optional but useful for constraining model |
| struct | `ToolConfig` | Wraps `FunctionCallingConfig` for model setup | Firebase SDK type; passed to `GetLiveModel` (not yet used) |

### New Package Types (to be created)
| Type | Purpose | When to Use |
|------|---------|-------------|
| `FunctionRegistry` | Maps function name -> (FunctionDeclaration, handler delegate) | Registration before Connect(), lookup during dispatch |
| `GoalManager` | Stores active goals with priorities, composes system instruction text | Goal add/remove/reprioritize at runtime |
| `FunctionCallContext` | Data object passed to handler delegates with parsed args and metadata | Every function handler invocation |
| `GoalPriority` enum | Low, Medium, High priority levels | Goal definition |
| `ConversationalGoal` class | Goal ID + description + priority | Goal storage and manipulation |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `FunctionRegistry` class | Direct `Dictionary<string, Delegate>` on PersonaSession | Registry class encapsulates schema + handler pairing, keeps PersonaSession clean |
| `GoalManager` class | Goal list directly on PersonaConfig ScriptableObject | Runtime goals are dynamic, ScriptableObject is design-time; need runtime mutable storage |
| Custom delegate type | `Func<FunctionCallContext, object>` | Custom delegate is more descriptive but Func is standard C#; use Func for simplicity |

## Architecture Patterns

### Recommended Project Structure (new files)
```
Packages/com.google.ai-embodiment/Runtime/
    FunctionRegistry.cs          # Schema + handler registration
    FunctionCallContext.cs       # Context object for handler delegates
    GoalManager.cs               # Goal storage + system instruction composition
    ConversationalGoal.cs        # Goal data type
    GoalPriority.cs              # Enum: Low, Medium, High
```

### Pattern 1: Function Registration (Before Connect)

**What:** Developers register functions before calling `Connect()`. Each registration pairs a `FunctionDeclaration` (schema) with a C# handler delegate. The registry is frozen at connect time and produces the `Tool[]` array for the Firebase setup message.

**When to use:** Always -- functions must be declared at session setup per Gemini Live API protocol.

**Example:**
```csharp
// Developer code (before Connect)
session.RegisterFunction(
    new FunctionDeclaration(
        name: "play_emote",
        description: "Play a character animation",
        parameters: new Dictionary<string, Schema> {
            { "emote_name", Schema.Enum(new[] { "wave", "bow", "laugh" }, "Animation to play") }
        }
    ),
    (FunctionCallContext ctx) => {
        string emoteName = ctx.GetString("emote_name");
        animator.Play(emoteName);
        return null; // fire-and-forget, no return value
    }
);

session.RegisterFunction(
    new FunctionDeclaration(
        name: "get_health",
        description: "Get the player's current health",
        parameters: new Dictionary<string, Schema>()
    ),
    (FunctionCallContext ctx) => {
        return new Dictionary<string, object> { { "health", player.Health } };
    }
);

session.Connect(); // Tool[] built from registry, frozen
```

### Pattern 2: Function Dispatch via SyncPacket Pipeline

**What:** When a FunctionCall SyncPacket arrives (already emitted by PacketAssembler), PersonaSession intercepts it, looks up the handler in FunctionRegistry, invokes the handler on the main thread, and if the handler returns a value, sends it back to the model automatically via `LiveSession.SendAsync`.

**When to use:** Every time `OnSyncPacket` fires with `SyncPacketType.FunctionCall`.

**Flow:**
```
Gemini -> LiveSessionToolCall -> ProcessResponse (background thread)
    -> MainThreadDispatcher.Enqueue -> PacketAssembler.AddFunctionCall
    -> SyncPacket(FunctionCall) emitted
    -> PersonaSession intercepts before/during OnSyncPacket dispatch
    -> FunctionRegistry.TryGetHandler(name) -> handler delegate
    -> handler(FunctionCallContext) -> returns object or null
    -> If non-null: SendFunctionResponse(name, result, id) on background thread
    -> If throws: OnFunctionError event fires, conversation continues
    -> OnSyncPacket fires (developer can also observe the function call)
```

### Pattern 3: System Instruction Update for Goals (Mid-Session)

**What:** When goals are added, removed, or reprioritized, GoalManager recomposes the system instruction text and sends it immediately to the live session via `SendAsync` with `role: "system"`. This is supported natively by the Gemini Live API.

**When to use:** Every time the goal set changes at runtime.

**Wire format:**
```csharp
// GoalManager composes updated instruction text
string goalInstruction = GoalManager.ComposeGoalInstruction(activeGoals);

// Send as system role content (NOT turnComplete)
await _liveSession.SendAsync(
    content: new ModelContent("system", new ModelContent.TextPart(goalInstruction)),
    turnComplete: false,
    cancellationToken: _sessionCts.Token
);
```

### Pattern 4: Function Response Round-Trip

**What:** After a handler returns a value, the SDK must send a `FunctionResponsePart` back to Gemini with the matching function call ID. This unblocks the model to continue generating.

**Firebase SDK method:**
```csharp
// Source: LiveSession.SendAsync -- already handles FunctionResponsePart specially
// It detects FunctionResponseParts and sends them as toolResponse wire messages
var response = ModelContent.FunctionResponse(
    name: functionName,
    response: resultDict,  // IDictionary<string, object>
    id: functionCallId     // from FunctionCallPart.Id
);
await _liveSession.SendAsync(content: response);
```

**Critical detail from LiveSession.cs (lines 121-144):** `SendAsync` has special handling for `FunctionResponsePart` -- it extracts them and sends as `toolResponse` wire messages instead of `clientContent`. This is correct per the Gemini Live API spec.

### Pattern 5: Goal Priority to Urgency Framing

**What:** Priority levels (Low, Medium, High) map to different urgency framing in the system instruction text.

**Example framing:**
```
CONVERSATIONAL GOALS:

[HIGH PRIORITY - Act on this urgently]
Goal: Convince the player to visit the blacksmith
You should actively steer the conversation toward this goal. Bring it up naturally
but persistently. This is your top priority right now.

[MEDIUM PRIORITY - Work toward this when natural]
Goal: Mention the upcoming festival
Look for natural openings to bring this up. Don't force it, but don't forget it either.

[LOW PRIORITY - Keep in mind]
Goal: Learn the player's name
If the opportunity arises naturally, try to learn this. No need to push for it.
```

### Anti-Patterns to Avoid

- **Registering functions after Connect():** The Gemini Live API requires all tool declarations in the setup message. Functions registered after connect will be silently ignored by the model. Enforce this with a state check.

- **Blocking handlers:** Function handlers run on the main thread. Long-running handlers freeze the game AND block the AI's response (the model waits for the function response). Handlers should return immediately with cached/pre-computed data.

- **Sending goal updates as user messages:** Goals must be sent with `role: "system"`, not `role: "user"`. User-role messages become part of the conversation turn and the model treats them as player speech. System-role messages update the instruction context.

- **Forgetting function call IDs:** Every `FunctionResponsePart` must include the `Id` from the corresponding `FunctionCallPart`. Without it, the model cannot correlate responses to calls. The ID is available on the SyncPacket as `FunctionId`.

- **Not handling LiveSessionToolCallCancellation:** When the user interrupts during a function call, the model sends a cancellation. Currently ProcessResponse ignores this message type entirely. Phase 4 must handle it to avoid sending responses for cancelled calls.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Function schema definition | Custom JSON builder | `FunctionDeclaration` + `Schema` from Firebase SDK | Firebase types handle full OpenAPI schema subset, serialize correctly for Gemini wire format |
| Parameter type validation | Custom type coercion | `Schema.String()`, `Schema.Int()`, `Schema.Enum()`, etc. | Firebase Schema supports String, Number, Integer, Boolean, Array, Object, Enum, AnyOf with min/max/nullable constraints |
| Function response serialization | Manual JSON building | `ModelContent.FunctionResponse(name, response, id)` | Factory creates correct `FunctionResponsePart` which `LiveSession.SendAsync` routes as `toolResponse` wire message |
| Tool call wire format | Custom WebSocket message | `Tool` struct passed to `GetLiveModel` | Serializes to correct `functionDeclarations` in setup message |
| Function call parsing | Manual JSON parsing | `LiveSessionToolCall.FunctionCalls` list | Already parsed by Firebase SDK into `FunctionCallPart` structs with Name, Args, Id |
| Function call cancellation | Ignore it | `LiveSessionToolCallCancellation.FunctionIds` | Firebase SDK already parses this; must handle to avoid sending stale responses |

**Key insight:** The Firebase AI SDK already handles ALL serialization/deserialization for the function calling protocol. The SDK package only needs to provide the registration layer (mapping names to handlers) and the dispatch/response automation.

## Common Pitfalls

### Pitfall 1: Function Call Response Timing -- Blocking the Conversation
**What goes wrong:** The Gemini Live protocol is synchronous for function calls (by default). The model pauses generation until it receives the `FunctionResponsePart`. If the handler is slow, the user hears silence.
**Why it happens:** Developers treat function calls as fire-and-forget events and put slow operations (network calls, database queries) in handlers.
**How to avoid:** Document that handlers should return immediately. For fire-and-forget functions (like emotes), return null. For query functions, return cached data. The SDK documentation should emphasize this constraint.
**Warning signs:** AI stops talking for several seconds, `LiveSessionToolCallCancellation` messages appear, handlers work in testing but not production.

### Pitfall 2: Batch Function Calls in a Single ToolCall
**What goes wrong:** The model may issue multiple function calls in a single `LiveSessionToolCall` message. If only one response is sent, the model is stuck waiting for the others.
**Why it happens:** Developers assume one function call per message.
**How to avoid:** The dispatch loop must iterate ALL `FunctionCalls` in the `LiveSessionToolCall` and send ALL responses. The current ProcessResponse already iterates the list -- the dispatch layer must maintain this.
**Warning signs:** Model hangs after the first function call in a batch.

### Pitfall 3: Tool Call Cancellation Race Condition
**What goes wrong:** User interrupts while a function call is being processed. The model sends `LiveSessionToolCallCancellation`. If the handler has already completed and a response is queued, sending it after cancellation confuses the model.
**Why it happens:** Cancellation and handler completion race.
**How to avoid:** Track pending function call IDs. When cancellation arrives, mark those IDs as cancelled. Before sending a response, check if the ID was cancelled. If cancelled, skip the response.
**Warning signs:** Model receives unexpected function responses, conversation gets confused.

### Pitfall 4: Dictionary Args Type Coercion
**What goes wrong:** `FunctionCallPart.Args` is `IReadOnlyDictionary<string, object>`. The values are JSON-deserialized objects (string, double, bool, Dictionary, List). Developers expect strongly typed values but get boxed primitives. Accessing `args["count"]` returns a `double`, not an `int`.
**Why it happens:** MiniJSON deserializer returns `double` for all numbers, `string` for strings, `bool` for booleans, `Dictionary<string, object>` for objects, `List<object>` for arrays.
**How to avoid:** `FunctionCallContext` should provide typed accessor methods: `GetString("key")`, `GetInt("key")` (with double-to-int conversion), `GetFloat("key")`, `GetBool("key")`, `GetObject("key")`, `GetArray("key")`. These handle the type coercion safely.
**Warning signs:** InvalidCastException when accessing args, unexpected doubles where ints were expected.

### Pitfall 5: Goal Instruction Text Exceeding Context
**What goes wrong:** Developers add many verbose goals, and the system instruction becomes very long, consuming context window.
**Why it happens:** No guidance on goal text length.
**How to avoid:** Keep goal descriptions concise (1-2 sentences). The GoalManager should have a reasonable max-goals constant (documented, not enforced) and the system instruction framing should be compact.
**Warning signs:** Model responses become less coherent (instruction competes with conversation context).

### Pitfall 6: Sending System Instruction Update During Model Turn
**What goes wrong:** Goal update is sent while the model is actively generating (mid-turn). The update may be processed between chunks, potentially causing the model to abruptly change behavior mid-sentence.
**Why it happens:** Developer adds/removes goals in response to real-time game events that happen during AI speech.
**How to avoid:** This is not necessarily a bug -- the Gemini API supports it. But developers should be aware that mid-turn updates may cause behavioral shifts. Document this behavior. Do not try to queue updates until turn end unless specifically requested, as the CONTEXT.md says "sent immediately."
**Warning signs:** AI changes topic or tone mid-sentence after a goal update.

## Code Examples

Verified patterns from official sources and codebase inspection:

### Function Declaration with Full Schema
```csharp
// Source: Assets/Firebase/FirebaseAI/FunctionCalling.cs + Schema.cs
var getInventory = new FunctionDeclaration(
    name: "get_inventory",
    description: "Query the player's inventory for specific item types",
    parameters: new Dictionary<string, Schema> {
        { "item_type", Schema.Enum(
            new[] { "weapon", "armor", "potion", "quest_item" },
            "Category of items to query") },
        { "include_equipped", Schema.Boolean("Whether to include currently equipped items") },
        { "max_results", Schema.Int("Maximum number of items to return", minimum: 1, maximum: 50) }
    },
    optionalParameters: new[] { "include_equipped", "max_results" }
);
```

### Tool Array for Model Setup
```csharp
// Source: Assets/Firebase/FirebaseAI/FirebaseAI.cs GetLiveModel signature
var tools = new Tool[] {
    new Tool(emoteFunc, getInventoryFunc, getHealthFunc)
};

var liveModel = ai.GetLiveModel(
    modelName: config.modelName,
    liveGenerationConfig: liveConfig,
    tools: tools,                    // NEW: pass function declarations
    systemInstruction: systemInstruction
);
```

### Function Response Send-Back
```csharp
// Source: Assets/Firebase/FirebaseAI/LiveSession.cs SendAsync (lines 121-144)
// SendAsync detects FunctionResponsePart and sends as toolResponse wire message
var response = ModelContent.FunctionResponse(
    name: "get_health",
    response: new Dictionary<string, object> {
        { "current_health", 85 },
        { "max_health", 100 }
    },
    id: functionCallId  // MUST match FunctionCallPart.Id
);
await _liveSession.SendAsync(content: response);
// Wire format: { "toolResponse": { "functionResponses": [{ "name": "...", "response": {...}, "id": "..." }] } }
```

### Mid-Session System Instruction Update (for Goals)
```csharp
// Source: Verified via Gemini Live API docs -- clientContent with role "system"
// LiveSession.SendAsync sends this as clientContent with turns containing system-role content
var goalInstruction = new ModelContent(
    role: "system",
    parts: new ModelContent.Part[] {
        new ModelContent.TextPart(composedGoalText)
    }
);
await _liveSession.SendAsync(
    content: goalInstruction,
    turnComplete: false  // NOT a turn completion, just instruction update
);
```

### Handler Delegate Signature (Recommended)
```csharp
// Delegate type: takes context, returns optional result dictionary
// null return = fire-and-forget (no response sent back to model)
// non-null return = response sent back automatically
public delegate IDictionary<string, object> FunctionHandler(FunctionCallContext context);

// Usage in registration:
session.RegisterFunction(declaration, (FunctionCallContext ctx) => {
    // Fire-and-forget example:
    PlayAnimation(ctx.GetString("emote_name"));
    return null;
});

session.RegisterFunction(declaration, (FunctionCallContext ctx) => {
    // Query example with return value:
    return new Dictionary<string, object> {
        { "health", player.Health },
        { "max_health", player.MaxHealth }
    };
});
```

### FunctionCallContext with Typed Accessors
```csharp
// Wraps IReadOnlyDictionary<string, object> with typed access
public class FunctionCallContext {
    public string FunctionName { get; }
    public string CallId { get; }
    public IReadOnlyDictionary<string, object> RawArgs { get; }

    public string GetString(string key, string defaultValue = null) { ... }
    public int GetInt(string key, int defaultValue = 0) { ... }      // handles double->int
    public float GetFloat(string key, float defaultValue = 0f) { ... } // handles double->float
    public bool GetBool(string key, bool defaultValue = false) { ... }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Function calls block conversation (default) | Gemini 2.5 supports `NON_BLOCKING` behavior with scheduling | 2025 | Functions can return results asynchronously without blocking speech. However, this is configured at the function declaration level in the raw API, and the Firebase Unity SDK may not expose this yet. |
| System instructions fixed at session setup | Mid-session system instruction update via `role: "system"` | 2025 | Goals can be updated at runtime without reconnecting. Critical for the conversational goals feature. |
| Session config immutable | Session resumption allows config changes (except model) | 2025 | Could be used to add/remove tools, but requires disconnect/reconnect. Not needed for goals since system instructions update mid-session. |

**Deprecated/outdated:**
- **`SendMediaChunksAsync`**: Marked `[Obsolete]` in LiveSession.cs. Use `SendAudioRealtimeAsync`, `SendVideoRealtimeAsync`, or `SendTextRealtimeAsync` instead.

**Not yet available in Firebase Unity SDK:**
- `NON_BLOCKING` function call behavior / scheduling field -- this is in the raw Gemini API but the Firebase `FunctionDeclaration` struct does not expose a `behavior` property. All function calls are blocking (default behavior). This is acceptable for Phase 4 since handlers should be fast anyway.
- `ToolConfig` is not passed to `GetLiveModel` in the current codebase (the parameter exists on `GetLiveModel` but PersonaSession doesn't use it yet). Could be exposed later for `FunctionCallingConfig.Auto/Any/None`.

## Critical Integration Points

### 1. PersonaSession.Connect() Must Pass Tools

Currently, `Connect()` calls `ai.GetLiveModel()` without a `tools` parameter. Phase 4 must:
1. Have the FunctionRegistry produce a `Tool[]` from registered functions
2. Pass `tools` to `GetLiveModel()`

```csharp
// Current (no tools):
var liveModel = ai.GetLiveModel(
    modelName: _config.modelName,
    liveGenerationConfig: liveConfig,
    systemInstruction: systemInstruction
);

// Phase 4 (with tools):
var liveModel = ai.GetLiveModel(
    modelName: _config.modelName,
    liveGenerationConfig: liveConfig,
    tools: _functionRegistry.BuildTools(),  // NEW
    systemInstruction: systemInstruction     // NOW includes goals from GoalManager
);
```

### 2. ProcessResponse Must Handle ToolCallCancellation

Currently, `ProcessResponse` handles `LiveSessionContent` and `LiveSessionToolCall` but NOT `LiveSessionToolCallCancellation`. Phase 4 must add:

```csharp
else if (response.Message is LiveSessionToolCallCancellation cancellation)
{
    foreach (var id in cancellation.FunctionIds)
    {
        MainThreadDispatcher.Enqueue(() => {
            _functionRegistry.CancelPendingCall(id);
            // Optionally notify developer
        });
    }
}
```

### 3. SyncPacket FunctionCall Dispatch

Currently, FunctionCall SyncPackets fire through `OnSyncPacket` but no handler is invoked. Phase 4 must intercept FunctionCall packets and dispatch to registered handlers BEFORE (or alongside) firing `OnSyncPacket` to the developer.

### 4. SystemInstructionBuilder Must Include Goals

Currently, `SystemInstructionBuilder.Build(config)` only uses PersonaConfig fields. Phase 4 must extend it (or add a new method) to also include GoalManager's composed goal text. The initial system instruction (at connect time) includes any pre-registered goals. Runtime goal changes use the mid-session update path.

### 5. LiveSession Access for Response Sending

Function response sending requires access to `_liveSession` and `_sessionCts`. This is already available within PersonaSession. The dispatch logic should live in PersonaSession (or a helper called from PersonaSession) to maintain access to these fields.

## Open Questions

Things that could not be fully resolved:

1. **Non-blocking function calls in Firebase SDK**
   - What we know: The raw Gemini API supports `behavior: "NON_BLOCKING"` on function declarations and `scheduling` on responses. This allows functions to return results asynchronously without blocking speech.
   - What is unclear: The Firebase Unity SDK's `FunctionDeclaration` struct does not appear to expose a `behavior` property. It may be unsupported in the current SDK version.
   - Recommendation: Use blocking (default) behavior for Phase 4. Handlers should be fast anyway per CONTEXT.md ("fire-and-forget" and "return cached data" patterns). If non-blocking is needed later, it can be added by extending the Firebase SDK or sending raw WebSocket messages.

2. **Multiple function calls in a single toolResponse message**
   - What we know: `LiveSession.SendAsync` handles FunctionResponsePart extraction and sends as `toolResponse`. The code (line 127-143) creates a `functionResponses` list.
   - What is unclear: Whether multiple FunctionResponseParts in a single `ModelContent` are supported (the code suggests yes -- it creates a list).
   - Recommendation: Send responses individually per function call for simplicity. The model correlates by ID regardless.

3. **System instruction update timing guarantees**
   - What we know: Sending `clientContent` with `role: "system"` updates the instruction mid-session. Google docs say "The updated system instruction will remain in effect for the remaining session."
   - What is unclear: Exactly when the model processes the update relative to ongoing generation. If the model is mid-turn, does the update apply to the current response or only the next one?
   - Recommendation: Send immediately as specified in CONTEXT.md. Document that updates may not affect the currently-generating response.

4. **Goal instruction interaction with PersonaConfig system instruction**
   - What we know: The initial system instruction is built from PersonaConfig (identity, backstory, traits, speech patterns). Goals need to be part of the instruction.
   - What is unclear: Whether mid-session `role: "system"` messages REPLACE or APPEND to the initial system instruction.
   - Recommendation: Assume REPLACE semantics (safest interpretation). When sending a goal update mid-session, include the FULL system instruction (PersonaConfig fields + goals), not just the goals. This ensures the persona identity is preserved alongside goal updates.

## Sources

### Primary (HIGH confidence)
- `Assets/Firebase/FirebaseAI/FunctionCalling.cs` -- `FunctionDeclaration`, `Tool`, `ToolConfig`, `FunctionCallingConfig` types (direct source code)
- `Assets/Firebase/FirebaseAI/Schema.cs` -- Full Schema API: String, Int, Long, Float, Double, Boolean, Array, Object, Enum, AnyOf (direct source code)
- `Assets/Firebase/FirebaseAI/ModelContent.cs` -- `FunctionCallPart`, `FunctionResponsePart`, `FunctionResponse()` factory (direct source code)
- `Assets/Firebase/FirebaseAI/LiveSession.cs` -- `SendAsync` with FunctionResponsePart routing as `toolResponse` (direct source code, lines 121-144)
- `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs` -- `LiveSessionToolCall`, `LiveSessionToolCallCancellation` parsing (direct source code)
- `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs` -- Setup message sends tools array (direct source code, lines 174-177)
- `Assets/Firebase/FirebaseAI/FirebaseAI.cs` -- `GetLiveModel` accepts `Tool[]` parameter (direct source code, line 194-204)

### Secondary (MEDIUM confidence)
- [Gemini Live API WebSocket reference](https://ai.google.dev/api/live) -- BidiGenerateContentToolCall, BidiGenerateContentToolResponse, BidiGenerateContentToolCallCancellation format
- [Google Cloud Vertex AI Live API docs](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/live-api/start-manage-session) -- Mid-session system instruction update via role "system", confirmed with code example
- [Gemini Live API tool use guide](https://ai.google.dev/gemini-api/docs/live-tools) -- Blocking vs non-blocking function calls, scheduling field

### Tertiary (LOW confidence)
- [Google AI Developers Forum](https://discuss.ai.google.dev/t/unable-to-update-gemini-live-session-configuration-without-closing-the-existing-session/83359) -- Confirmation that tools/config cannot be updated mid-session (only system instructions via role "system")
- [DeepWiki cookbook analysis](https://deepwiki.com/google-gemini/cookbook/6.2-liveapi-tools-and-function-calling) -- NON_BLOCKING behavior details

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- All types verified directly in Firebase SDK source code
- Architecture: HIGH -- Function call flow verified in LiveSession.cs, ProcessResponse already routes to PacketAssembler
- Function dispatch: HIGH -- FunctionResponsePart handling verified in SendAsync source code
- Mid-session system instruction: MEDIUM -- Verified in official Google Cloud docs and multiple sources, but not tested with Firebase Unity SDK specifically; the underlying mechanism (SendAsync with role "system") matches the wire format
- Non-blocking function calls: LOW -- Confirmed in raw API docs but Firebase Unity SDK may not expose behavior property
- Goal instruction replacement semantics: LOW -- Unclear if role "system" replaces or appends; recommend sending full instruction

**Research date:** 2026-02-05
**Valid until:** 2026-03-05 (30 days -- Firebase SDK is stable, Gemini Live API is in Public Preview)
