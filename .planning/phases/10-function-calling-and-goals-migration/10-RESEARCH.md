# Phase 10: Function Calling and Goals Migration - Research

**Researched:** 2026-02-13
**Domain:** Gemini Live WebSocket tool protocol, function declaration schemas, mid-session instruction updates
**Confidence:** HIGH

## Summary

Phase 10 wires the existing function calling and conversational goals APIs to the direct WebSocket transport (GeminiLiveClient), replacing the Firebase SDK intermediary removed in Phase 8. The developer-facing API (RegisterFunction, AddGoal, RemoveGoal, ReprioritizeGoal) stays the same. The wire protocol changes from Firebase-managed to hand-built JSON over WebSocket.

The Gemini Live API supports tool declarations in the setup handshake via a `tools` array containing `functionDeclarations`. Function calls arrive as top-level `toolCall` server messages (already partially parsed in GeminiLiveClient.HandleJsonMessage). Responses go back as top-level `toolResponse` client messages. Cancellations arrive as `toolCallCancellation` with an `ids` array. The current codebase already parses `toolCall` and enqueues `FunctionCall` events, but does NOT capture the function call `id` field (GeminiEvent lacks FunctionId), does NOT send `toolResponse` messages back, and does NOT handle `toolCallCancellation`. These are the three gaps Phase 10 closes.

Mid-session system instruction updates are NOT supported by the Gemini Live API. System instructions are immutable after the setup handshake. The CONTEXT.md decision ("best-effort send, log warning if not supported") anticipated this. Goal updates will accumulate locally and apply at next Connect(). The `clientContent` mechanism can inject user-role text but NOT system instructions. A possible workaround is to send goal text as a user-role `clientContent` message, but the CONTEXT.md decision explicitly says "Goal text stays in system instruction, not user-role messages." The correct behavior per CONTEXT.md is: try sending, if not supported, log warning and goals apply on next connection.

Additionally, per CONTEXT.md, both a native toolCall path AND a prompt-based fallback path must be implemented, since audio-native Gemini models may not reliably support native function calling. The official docs do state that `gemini-2.5-flash-native-audio` "supports function calling" with AUDIO-only responseModalities, but the CONTEXT.md decision to implement both paths is a hedge against real-world reliability issues.

**Primary recommendation:** Add FunctionId to GeminiEvent, build FunctionDeclaration builder class, add tools to setup message, implement toolResponse sending on GeminiLiveClient, handle toolCallCancellation, implement prompt-based fallback path, and implement best-effort goal update with warning log.

## Standard Stack

### Core (all hand-built, no external dependencies beyond Newtonsoft.Json)
| Component | Purpose | Why Standard |
|-----------|---------|--------------|
| Newtonsoft.Json (JObject/JArray) | Build tool declaration JSON, parse toolCall args, build toolResponse | Already the project's JSON library (v0.8 decision) |
| GeminiLiveClient | WebSocket transport for setup, toolResponse, receive toolCall | Already exists, needs extension |
| FunctionRegistry | Maps function name -> (declaration, handler) pairs | Already exists, needs declaration parameter added |
| SystemInstructionBuilder | Composes persona + goals text for system instruction | Already exists, already includes goal support |

### New Types (to be created)
| Type | Purpose | When to Use |
|------|---------|-------------|
| `FunctionDeclaration` (builder class) | Typed builder for function schema (name, description, parameters) | Registration before Connect() |
| `FunctionParameter` or inner builder | Defines individual parameter (type, description, enum values) | Inside FunctionDeclaration builder |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Typed FunctionDeclaration builder | Raw JObject construction | Builder enforces flat-primitive constraint from CONTEXT.md, prevents invalid schemas |
| Static bool flag for native vs prompt-based | ScriptableObject field | CONTEXT.md says "not buried in ScriptableObject UI" -- static field is simpler |

## Architecture Patterns

### Recommended Project Structure (changes only)
```
Packages/com.google.ai-embodiment/Runtime/
    FunctionDeclaration.cs       # NEW: typed builder for tool schemas
    FunctionRegistry.cs          # MODIFY: add declaration parameter, BuildToolsJson(), BuildPromptInstructions()
    GeminiLiveClient.cs          # MODIFY: accept tools in setup, add SendToolResponse(), handle toolCallCancellation
    GeminiEvent.cs               # MODIFY: add FunctionId field
    PersonaSession.cs            # MODIFY: wire declaration to Register, implement SendFunctionResponse, SendGoalUpdate
    SystemInstructionBuilder.cs  # MODIFY: add method to include function prompt instructions for prompt-based path
```

### Pattern 1: Tool Declaration in Setup Handshake (Native Path)

**What:** FunctionRegistry builds a JArray of tool declarations from registered FunctionDeclaration objects. This array is injected into the setup message alongside model, generationConfig, and systemInstruction.

**When to use:** When the native function calling flag is enabled.

**Setup message with tools:**
```json
{
  "setup": {
    "model": "models/gemini-2.5-flash-native-audio-preview-12-2025",
    "generationConfig": {
      "responseModalities": ["AUDIO"],
      "speechConfig": { ... }
    },
    "systemInstruction": {
      "parts": [{ "text": "..." }]
    },
    "tools": [
      {
        "functionDeclarations": [
          {
            "name": "play_emote",
            "description": "Play a character animation",
            "parameters": {
              "type": "OBJECT",
              "properties": {
                "emote_name": {
                  "type": "STRING",
                  "description": "Animation to play",
                  "enum": ["wave", "bow", "laugh"]
                }
              },
              "required": ["emote_name"]
            }
          }
        ]
      }
    ]
  }
}
```

### Pattern 2: toolCall Parsing with Function ID Capture

**What:** GeminiLiveClient already parses `toolCall.functionCalls` but does NOT capture the `id` field. Phase 10 adds `FunctionId` to GeminiEvent and captures it from each function call in the array.

**Server message format:**
```json
{
  "toolCall": {
    "functionCalls": [
      {
        "id": "func-call-abc123",
        "name": "play_emote",
        "args": {
          "emote_name": "wave"
        }
      }
    ]
  }
}
```

**Current code gap (GeminiLiveClient.cs line 428-439):**
```csharp
// CURRENT: captures name and args, but NOT id
Enqueue(new GeminiEvent
{
    Type = GeminiEventType.FunctionCall,
    FunctionName = name,
    FunctionArgsJson = args
    // MISSING: FunctionId = fc["id"]?.ToString()
});
```

### Pattern 3: toolResponse Sending

**What:** After a function handler returns a non-null result, PersonaSession sends a `toolResponse` message back to Gemini via GeminiLiveClient. The response must include the matching function call `id`.

**Client message format:**
```json
{
  "toolResponse": {
    "functionResponses": [
      {
        "id": "func-call-abc123",
        "name": "play_emote",
        "response": {
          "result": "ok"
        }
      }
    ]
  }
}
```

**New method on GeminiLiveClient:**
```csharp
public void SendToolResponse(string callId, string name, IDictionary<string, object> response)
{
    if (!IsConnected || string.IsNullOrEmpty(callId)) return;

    var payload = new JObject
    {
        ["toolResponse"] = new JObject
        {
            ["functionResponses"] = new JArray
            {
                new JObject
                {
                    ["id"] = callId,
                    ["name"] = name,
                    ["response"] = JObject.FromObject(response)
                }
            }
        }
    };
    _ = SendJsonAsync(payload);
}
```

### Pattern 4: toolCallCancellation Handling

**What:** When the user interrupts during a function call, the server sends a `toolCallCancellation` message with an array of function call IDs to cancel. GeminiLiveClient must parse this and enqueue cancellation events. FunctionRegistry already has MarkCancelled/IsCancelled methods.

**Server message format:**
```json
{
  "toolCallCancellation": {
    "ids": ["func-call-abc123", "func-call-def456"]
  }
}
```

**Implementation in HandleJsonMessage:**
```csharp
var toolCallCancellation = msg["toolCallCancellation"] as JObject;
if (toolCallCancellation != null)
{
    var ids = toolCallCancellation["ids"] as JArray;
    if (ids != null)
    {
        foreach (var id in ids)
        {
            Enqueue(new GeminiEvent
            {
                Type = GeminiEventType.FunctionCallCancellation,
                FunctionId = id.ToString()
            });
        }
    }
}
```

### Pattern 5: Prompt-Based Function Calling Fallback

**What:** When the native toolCall path is disabled (flag = false), FunctionRegistry builds system prompt instructions instead of tool JSON. The AI is instructed to output structured trigger phrases that are parsed from transcription.

**System prompt injection:**
```
AVAILABLE FUNCTIONS:
When you want to call a function, output EXACTLY this format on its own line:
[CALL: function_name {"param": "value"}]

Functions:
- play_emote(emote_name: string [wave|bow|laugh]) - Play a character animation
- get_health() - Get the player's current health, responds with result

IMPORTANT: Output the [CALL: ...] tag exactly as shown. Do not explain or narrate the function call.
```

**Transcription parsing (regex):**
```csharp
// Match [CALL: functionName {"arg": "value"}] or [CALL: functionName {}]
private static readonly Regex FunctionCallPattern =
    new Regex(@"\[CALL:\s*(\w+)\s*(\{[^}]*\})\]", RegexOptions.Compiled);
```

### Pattern 6: Goal Update (Best-Effort Mid-Session)

**What:** Per CONTEXT.md decisions, goals accumulate locally and are always included in the system instruction at Connect() time. Mid-session, a best-effort attempt is made to update the system instruction. Since the Gemini Live API does NOT support mid-session system instruction updates, the implementation logs a warning and the goals take effect on the next connection.

**Implementation:**
```csharp
private void SendGoalUpdate()
{
    if (_client == null || !_client.IsConnected || State != SessionState.Connected)
        return;

    // Gemini Live API does not support mid-session system instruction updates.
    // Goals accumulate locally and will be applied at next Connect().
    Debug.Log(
        "PersonaSession: Goal updated. Mid-session system instruction updates are not supported " +
        "by the Gemini Live API. Goals will take effect on next connection.");
}
```

### Anti-Patterns to Avoid

- **Sending toolResponse via clientContent:** GitHub issue #906 shows some developers tried wrapping function responses in `clientContent` as a workaround. The official protocol uses top-level `toolResponse` messages. Use `toolResponse`, not `clientContent` with `functionResponse` parts.

- **Forgetting the function call ID:** Every `toolResponse` MUST include the `id` from the corresponding `toolCall`. Without it, the model cannot correlate responses. The ID flows: `toolCall.functionCalls[].id` -> `GeminiEvent.FunctionId` -> `SyncPacket.FunctionId` -> `toolResponse.functionResponses[].id`.

- **Sending response for cancelled calls:** Always check `FunctionRegistry.IsCancelled(callId)` before sending `toolResponse`. The cancellation/completion race condition is real.

- **Trying to update system instructions mid-session:** The API does NOT support this. Do not send system-role `clientContent` messages -- the API only accepts "user" and "model" roles in `clientContent.turns`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization of tool declarations | Manual string concatenation | `JObject`/`JArray` from Newtonsoft.Json | Handles escaping, nesting, formatting correctly |
| OpenAPI parameter schema | Custom schema validator | Simple builder that produces `{type, description, enum, required}` objects | Gemini uses a subset of OpenAPI; builder enforces flat-primitive constraint from CONTEXT.md |
| Function call ID tracking | Custom ID generation | Use IDs from server `toolCall` messages | Server generates unique IDs; client just echoes them back |
| Regex for prompt-based parsing | Character-by-character parser | `Regex` with `@"\[CALL:\s*(\w+)\s*(\{[^}]*\})\]"` | Simple enough for flat-primitive args; regex handles whitespace variations |

**Key insight:** The wire protocol is simple JSON. Newtonsoft.Json's JObject/JArray is sufficient for all construction and parsing. No additional serialization libraries needed.

## Common Pitfalls

### Pitfall 1: Function Call ID Missing from GeminiEvent
**What goes wrong:** GeminiEvent currently has no FunctionId field. Without it, toolResponse cannot be sent (requires matching ID).
**Why it happens:** Phase 7/8 deferred ID capture to Phase 10 (documented in STATE.md decisions).
**How to avoid:** Add `public string FunctionId;` to GeminiEvent struct. Capture from `fc["id"]?.ToString()` in HandleJsonMessage.
**Warning signs:** toolResponse sends with null ID, server rejects or ignores the response.

### Pitfall 2: toolResponse Format Ambiguity
**What goes wrong:** Multiple formats have been discussed in the community. Some used `clientContent` with `functionResponse` parts, others used top-level `toolResponse`.
**Why it happens:** Early documentation was unclear; GitHub issue #906 documents the confusion.
**How to avoid:** Use the official `toolResponse` top-level message format per the Vertex AI reference docs. Structure: `{"toolResponse": {"functionResponses": [{"id": "...", "name": "...", "response": {...}}]}}`.
**Warning signs:** WebSocket connection closes with error 1008 or 1011 after sending response.

### Pitfall 3: Batch Function Calls
**What goes wrong:** The model may issue multiple function calls in a single `toolCall` message. If only one response is sent, the model waits indefinitely for the others.
**Why it happens:** Developers assume one function call per message.
**How to avoid:** The existing code already iterates `functionCalls` array in HandleJsonMessage. Each call becomes a separate GeminiEvent. All responses must be sent. Individual `toolResponse` messages per call is fine (no need to batch responses).
**Warning signs:** Model hangs after first function call in a batch.

### Pitfall 4: Prompt-Based Fallback -- Transcription Fragmentation
**What goes wrong:** Output transcription arrives in fragments (word-by-word or phrase-by-phrase). A `[CALL: ...]` trigger phrase may be split across multiple transcription events.
**Why it happens:** Gemini streams transcription incrementally.
**How to avoid:** Buffer transcription text and scan the accumulated buffer for complete `[CALL: ...]` patterns. Clear matched patterns from the buffer. Use the existing PacketAssembler text buffering as a model.
**Warning signs:** Function calls never trigger, or trigger with partial/garbled arguments.

### Pitfall 5: Prompt-Based Fallback -- AI Speaks the Trigger Phrase
**What goes wrong:** The AI reads the `[CALL: ...]` tag aloud as part of its speech, so the user hears "call play emote wave."
**Why it happens:** Audio-native models produce speech for all text including control sequences.
**How to avoid:** Include explicit instructions in the system prompt: "Do NOT speak the [CALL: ...] tag aloud. Output it silently." Also, strip matched trigger phrases from text before forwarding to OnOutputTranscription. However, with AUDIO-only modality, the model may still vocalize; this is an inherent limitation of the prompt-based approach.
**Warning signs:** User hears function call syntax spoken aloud.

### Pitfall 6: Goal Update Timing Expectations
**What goes wrong:** Developer calls AddGoal and expects immediate AI behavior change, but goals only take effect on next Connect().
**Why it happens:** The Gemini Live API does not support mid-session system instruction updates.
**How to avoid:** Document this limitation clearly. The warning log from SendGoalUpdate makes it visible. Goals added before Connect() work immediately.
**Warning signs:** Developer reports goals have no effect during active session.

## Code Examples

### FunctionDeclaration Builder
```csharp
// Source: CONTEXT.md decisions -- typed builder, flat primitives only
public class FunctionDeclaration
{
    public string Name { get; }
    public string Description { get; }
    private readonly List<ParameterDef> _parameters = new List<ParameterDef>();

    public FunctionDeclaration(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public FunctionDeclaration AddString(string name, string description, bool required = true)
    { ... }

    public FunctionDeclaration AddInt(string name, string description, bool required = true)
    { ... }

    public FunctionDeclaration AddFloat(string name, string description, bool required = true)
    { ... }

    public FunctionDeclaration AddBool(string name, string description, bool required = true)
    { ... }

    public FunctionDeclaration AddEnum(string name, string description, string[] values, bool required = true)
    { ... }

    /// <summary>Builds the Gemini API JSON for native toolCall path.</summary>
    public JObject ToToolJson() { ... }

    /// <summary>Builds system prompt text for prompt-based fallback path.</summary>
    public string ToPromptText() { ... }
}
```

### Registration Flow
```csharp
// Developer code (before Connect)
var emoteDecl = new FunctionDeclaration("play_emote", "Play a character animation")
    .AddEnum("emote_name", "Animation to play", new[] { "wave", "bow", "laugh" });

session.RegisterFunction("play_emote", emoteDecl, (FunctionCallContext ctx) => {
    string emoteName = ctx.GetString("emote_name");
    animator.Play(emoteName);
    return null; // fire-and-forget
});

session.Connect(); // tools sent in setup handshake
```

### FunctionDeclaration.ToToolJson() Output
```csharp
// Produces:
// {
//   "name": "play_emote",
//   "description": "Play a character animation",
//   "parameters": {
//     "type": "OBJECT",
//     "properties": {
//       "emote_name": {
//         "type": "STRING",
//         "description": "Animation to play",
//         "enum": ["wave", "bow", "laugh"]
//       }
//     },
//     "required": ["emote_name"]
//   }
// }
```

### FunctionDeclaration.ToPromptText() Output
```csharp
// Produces:
// "- play_emote(emote_name: string [wave|bow|laugh]) - Play a character animation"
```

### FunctionRegistry.BuildToolsJson() (Native Path)
```csharp
public JArray BuildToolsJson()
{
    var declarations = new JArray();
    foreach (var entry in _entries)
    {
        declarations.Add(entry.Value.Declaration.ToToolJson());
    }
    return new JArray
    {
        new JObject
        {
            ["functionDeclarations"] = declarations
        }
    };
}
```

### FunctionRegistry.BuildPromptInstructions() (Prompt-Based Path)
```csharp
public string BuildPromptInstructions()
{
    var sb = new StringBuilder();
    sb.AppendLine("AVAILABLE FUNCTIONS:");
    sb.AppendLine("When you want to call a function, output EXACTLY this format:");
    sb.AppendLine("[CALL: function_name {\"param\": \"value\"}]");
    sb.AppendLine();
    sb.AppendLine("Functions:");
    foreach (var entry in _entries)
    {
        sb.AppendLine(entry.Value.Declaration.ToPromptText());
    }
    sb.AppendLine();
    sb.AppendLine("IMPORTANT: Output the [CALL: ...] tag exactly. Do not narrate it.");
    return sb.ToString();
}
```

### GeminiLiveClient Setup Message with Tools
```csharp
// In SendSetupMessage(), after existing generationConfig and systemInstruction:
if (toolsJson != null && toolsJson.Count > 0)
{
    setupInner["tools"] = toolsJson;
}
```

### GeminiLiveClient.SendToolResponse()
```csharp
public void SendToolResponse(string callId, string name, IDictionary<string, object> response)
{
    if (!IsConnected || string.IsNullOrEmpty(callId)) return;

    var responseObj = response != null ? JObject.FromObject(response) : new JObject();
    var payload = new JObject
    {
        ["toolResponse"] = new JObject
        {
            ["functionResponses"] = new JArray
            {
                new JObject
                {
                    ["id"] = callId,
                    ["name"] = name,
                    ["response"] = responseObj
                }
            }
        }
    };
    _ = SendJsonAsync(payload);
}
```

### PersonaSession.SendFunctionResponse() (Updated)
```csharp
private void SendFunctionResponse(string name, IDictionary<string, object> result, string callId)
{
    if (_client == null || !_client.IsConnected) return;
    if (string.IsNullOrEmpty(callId))
    {
        Debug.LogWarning($"PersonaSession: Cannot send function response for '{name}' -- no call ID.");
        return;
    }
    _client.SendToolResponse(callId, name, result);
}
```

## State of the Art

| Old Approach (v1/Firebase) | Current Approach (v0.8/WebSocket) | Impact |
|---|---|---|
| Firebase `FunctionDeclaration` + `Schema` types | Hand-built `FunctionDeclaration` builder class with JObject output | Must build JSON manually; CONTEXT.md specifies flat primitives only, so simpler than Firebase's full OpenAPI subset |
| Firebase `Tool` struct passed to `GetLiveModel` | `tools` JArray injected into setup message JSON | Direct control over wire format |
| Firebase `LiveSession.SendAsync` with `FunctionResponsePart` | `GeminiLiveClient.SendToolResponse()` sending `toolResponse` JSON | Direct WebSocket send; no SDK intermediary |
| Firebase `LiveSessionToolCallCancellation` | Parse `toolCallCancellation.ids` in HandleJsonMessage | Must implement parsing (was handled by SDK) |
| Mid-session system instruction via Firebase `SendAsync` with role "system" | NOT supported by raw Gemini Live API | Goals accumulate locally, apply at next Connect() |
| `NON_BLOCKING` function behavior (raw API) | Not implemented (blocking default) | Acceptable for Phase 10; handlers should be fast per CONTEXT.md |

**Key protocol change:** The Firebase SDK's `SendAsync` had special-case routing that detected `FunctionResponsePart` and sent it as `toolResponse` wire messages. Without Firebase, we must build the `toolResponse` JSON explicitly.

**Mid-session instruction update reality check:** The Phase 4 research stated "Gemini Live API supports mid-session system instruction updates via clientContent with role: 'system'." This was based on Firebase SDK behavior where `SendAsync` with role "system" may have worked through SDK-specific handling. Research for Phase 10 confirms that the raw WebSocket API does NOT support mid-session system instruction changes -- `clientContent` only accepts "user" and "model" roles, and setup configuration is immutable after connection. The CONTEXT.md decision to use best-effort with warning log is the correct approach.

## Open Questions

1. **toolResponse format stability**
   - What we know: The Vertex AI reference docs specify `{"toolResponse": {"functionResponses": [{"id", "name", "response"}]}}`. GitHub issue #906 documented early confusion but the format has stabilized.
   - What's unclear: Whether there are edge cases with the `response` object format (must it be `{"result": value}` or can it be any object?).
   - Recommendation: Use `JObject.FromObject(result)` where `result` is the handler's return `IDictionary<string, object>`. This matches the flexibility of the Gemini API.

2. **Prompt-based fallback reliability with audio-native models**
   - What we know: Audio-native models generate speech directly. They have output transcription enabled. The [CALL: ...] tag would appear in the transcription stream.
   - What's unclear: Whether the model will consistently produce structured trigger phrases in transcription, or if it will garble/omit them. Also unclear if the model will vocalize the tags.
   - Recommendation: Implement the regex parser and explicit system prompt instructions. Test with actual model. The flag makes switching to native path easy when Google improves support.

3. **Native tool calling with audio-only responseModalities**
   - What we know: Official docs state `gemini-2.5-flash-native-audio` "supports function calling." Examples show `responseModalities: ["AUDIO"]` with tools.
   - What's unclear: Whether it works reliably in practice. The CONTEXT.md user explicitly expressed uncertainty ("Audio-native Gemini models may not support native function calling").
   - Recommendation: Implement native path first (it's simpler and official). The flag lets developers switch to prompt-based if native doesn't work in practice.

4. **GeminiLiveConfig extension for tools**
   - What we know: GeminiLiveConfig currently has ApiKey, Model, SystemInstruction, VoiceName, AudioInputSampleRate, AudioOutputSampleRate.
   - What's unclear: Best way to pass tools -- add a JArray ToolsJson property to GeminiLiveConfig, or pass tools as a parameter to ConnectAsync().
   - Recommendation: Add `public JArray ToolsJson;` to GeminiLiveConfig. This keeps configuration centralized and matches the existing pattern.

## Sources

### Primary (HIGH confidence)
- [Vertex AI Gemini Live API Reference](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/model-reference/multimodal-live) -- Authoritative BidiGenerateContentSetup, toolCall, toolResponse, toolCallCancellation JSON structures
- [Gemini Live API WebSocket Reference](https://ai.google.dev/api/live) -- Protocol specification, message types, setup format
- [Tool use with Live API](https://ai.google.dev/gemini-api/docs/live-tools) -- Function declaration format, NON_BLOCKING behavior, scheduling parameter
- [Gemini Function Calling Guide](https://ai.google.dev/gemini-api/docs/function-calling) -- FunctionDeclaration schema with parameters (OpenAPI subset), supported types
- Codebase: GeminiLiveClient.cs, FunctionRegistry.cs, PersonaSession.cs, GeminiEvent.cs, SystemInstructionBuilder.cs -- Current implementation state

### Secondary (MEDIUM confidence)
- [GitHub Issue #906 - Cookbook](https://github.com/google-gemini/cookbook/issues/906) -- toolResponse format clarification, clientContent workaround discussion
- [Google AI Forum - Tool Response for Websockets](https://discuss.ai.google.dev/t/live-api-tool-response-for-websockets/98933) -- Confirmed toolResponse JSON structure
- [Live API Session Management](https://ai.google.dev/gemini-api/docs/live-session) -- Session lifetime, context window compression, configuration immutability

### Tertiary (LOW confidence)
- [Gemini 2.5 Native Audio Blog Post](https://blog.google/products/gemini/gemini-audio-model-updates/) -- "sharper function calling" claim for native audio models
- [Google AI Forum - Tool Calling Issues](https://discuss.ai.google.dev/t/gemini-live-api-tool-calling-issues-inconsistent-behavior-and-empty-tool-responses/85288) -- Community reports of inconsistent tool calling behavior
- Phase 4 Research (04-RESEARCH.md) -- Historical context on Firebase SDK approach, mid-session instruction claim (now corrected)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- Newtonsoft.Json is established; wire protocol verified against Vertex AI reference docs
- Tool declaration format: HIGH -- Verified against official function calling guide + Vertex AI reference
- toolResponse format: HIGH -- Verified against Vertex AI reference and community confirmation
- toolCallCancellation: HIGH -- Verified against Vertex AI reference, simple ids array
- Mid-session instruction limitation: HIGH -- Multiple sources confirm immutable after setup; Phase 4's claim was based on Firebase SDK intermediary behavior
- Prompt-based fallback: MEDIUM -- Design is sound but real-world behavior with audio-native models is untested
- Native tool calling with audio models: MEDIUM -- Officially documented as supported, but community reports suggest inconsistencies

**Research date:** 2026-02-13
**Valid until:** 2026-03-13 (30 days -- Gemini Live API is evolving but core protocol is stable)
