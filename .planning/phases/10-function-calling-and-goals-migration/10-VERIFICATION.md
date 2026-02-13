---
phase: 10-function-calling-and-goals-migration
verified: 2026-02-13T22:00:00Z
status: passed
score: 10/10 must-haves verified
gaps: []
---

# Phase 10: Function Calling and Goals Migration Verification Report

**Phase Goal:** AI-triggered function calls and conversational goals work over the WebSocket transport with the same developer-facing API, including both native toolCall and prompt-based fallback paths
**Verified:** 2026-02-13T22:00:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Developers register functions with typed declarations (name, description, typed parameters) before Connect() | VERIFIED | FunctionDeclaration.cs (225 lines): fluent builder with AddString/AddInt/AddFloat/AddBool/AddEnum, ToToolJson(), ToPromptText(). PersonaSession.RegisterFunction(name, declaration, handler) at line 310. Both AyaSampleController copies use the 3-param API with real FunctionDeclaration objects. |
| 2 | Tool declarations are sent as JSON in the WebSocket setup handshake when native mode is enabled | VERIFIED | PersonaSession.Connect() lines 179-186: when HasRegistrations and UseNativeFunctionCalling, builds ToolsJson via FunctionRegistry.BuildToolsJson() and assigns to liveConfig.ToolsJson. GeminiLiveClient.SendSetupMessage() lines 216-219: injects tools into setupInner when ToolsJson is non-null. FunctionRegistry.BuildToolsJson() lines 94-110: wraps declarations in [{functionDeclarations: [...]}] format. |
| 3 | Function call IDs from the server are captured and flow through events to enable response correlation | VERIFIED | GeminiLiveClient.HandleJsonMessage() line 472: FunctionId = fc["id"]?.ToString() in toolCall parsing. GeminiEvent.cs line 27: public string FunctionId field. PersonaSession.HandleFunctionCallEvent() line 639: passes ev.FunctionId to PacketAssembler.AddFunctionCall(). SyncPacket.FunctionId property carries it to DispatchFunctionCall(). FunctionCallContext.CallId carries it to the handler. |
| 4 | Function responses can be sent back to Gemini via toolResponse WebSocket messages | VERIFIED | GeminiLiveClient.SendToolResponse() lines 136-157: builds {toolResponse: {functionResponses: [{id, name, response}]}} JSON and sends via WebSocket. PersonaSession.SendFunctionResponse() lines 753-762: calls _client.SendToolResponse(callId, name, result). PersonaSession.DispatchFunctionCall() lines 743-747: calls SendFunctionResponse when handler returns non-null result and FunctionId is present. |
| 5 | Tool call cancellations from the server are parsed and enqueued as events | VERIFIED | GeminiLiveClient.HandleJsonMessage() lines 478-494: parses toolCallCancellation.ids array, enqueues FunctionCallCancellation events with FunctionId. GeminiEventType.FunctionCallCancellation exists at line 12 of GeminiEvent.cs. PersonaSession.HandleGeminiEvent() line 441: routes FunctionCallCancellation to _functionRegistry.MarkCancelled(). DispatchFunctionCall() line 719: checks IsCancelled before invoking handler. |
| 6 | RegisterFunction accepts a FunctionDeclaration and tool declarations flow into the setup handshake at Connect() time | VERIFIED | PersonaSession.RegisterFunction() line 310: takes (name, FunctionDeclaration, FunctionHandler). FunctionRegistry.Register() line 63: takes (name, declaration, handler), validates non-null declaration. Connect() builds ToolsJson and passes to liveConfig (native path) or builds functionInstructions for system prompt (prompt-based path). |
| 7 | AI-triggered function calls carry the function call ID through to the handler and back via toolResponse | VERIFIED | Full chain verified: GeminiLiveClient parses fc["id"] -> GeminiEvent.FunctionId -> PersonaSession.HandleFunctionCallEvent passes to PacketAssembler.AddFunctionCall -> SyncPacket.FunctionId -> DispatchFunctionCall creates FunctionCallContext(name, id, args) -> handler receives context.CallId -> SendFunctionResponse calls _client.SendToolResponse(callId, name, result). |
| 8 | Function call cancellations from the server cancel pending dispatches | VERIFIED | GeminiLiveClient parses toolCallCancellation -> enqueues FunctionCallCancellation event -> PersonaSession routes to _functionRegistry.MarkCancelled(ev.FunctionId) -> DispatchFunctionCall checks _functionRegistry.IsCancelled(packet.FunctionId) and returns early if cancelled. IsCancelled is one-shot (removes from set). |
| 9 | Prompt-based fallback: when enabled, function instructions are injected into system prompt and [CALL: ...] tags in transcription trigger handlers | VERIFIED | PersonaSession.Connect() lines 163-168: builds functionInstructions via BuildPromptInstructions() when !UseNativeFunctionCalling. SystemInstructionBuilder.Build(config, goalManager, functionInstructions) line 43-51: appends to system instruction. PersonaSession.HandleOutputTranscription() lines 518-523: when !UseNativeFunctionCalling, appends to _functionCallBuffer and calls ParsePromptFunctionCalls(). ParsePromptFunctionCalls() lines 531-577: regex matches [CALL: name {args}], dispatches through PacketAssembler. Buffer management: bounded (1000 char cap), cleared on turn complete/interrupted/disconnect. |
| 10 | Goal updates mid-session log a clear warning that changes apply on next connection | VERIFIED | PersonaSession.SendGoalUpdate() lines 767-777: when connected, logs Debug.Log informational message: "Goal updated. Mid-session system instruction updates are not supported by the Gemini Live API. Goals will take effect on next connection." AddGoal/RemoveGoal/ReprioritizeGoal all call SendGoalUpdate() (lines 337, 346, 359). Goals accumulate locally in GoalManager and are applied at Connect() time via SystemInstructionBuilder.Build(_config, _goalManager, ...). |

**Score:** 10/10 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Packages/com.google.ai-embodiment/Runtime/FunctionDeclaration.cs` | Typed builder for function schemas | VERIFIED (225 lines, no stubs, imported by FunctionRegistry + PersonaSession + AyaSampleController) | Full fluent builder with 5 Add methods, ToToolJson() producing valid Gemini API format, ToPromptText() for prompt path. Private ParameterDef inner class. |
| `Packages/com.google.ai-embodiment/Runtime/FunctionRegistry.cs` | Declaration storage, BuildToolsJson(), BuildPromptInstructions() | VERIFIED (202 lines, no stubs, used by PersonaSession) | Register(name, declaration, handler), BuildToolsJson() returns JArray, BuildPromptInstructions() returns formatted text, MarkCancelled/IsCancelled for cancellation tracking. |
| `Packages/com.google.ai-embodiment/Runtime/GeminiEvent.cs` | FunctionId field and FunctionCallCancellation event type | VERIFIED (29 lines, struct + enum) | FunctionId field at line 27. FunctionCallCancellation at line 12 in enum. |
| `Packages/com.google.ai-embodiment/Runtime/GeminiLiveConfig.cs` | ToolsJson field for passing tool declarations to setup | VERIFIED (21 lines, config class) | public JArray ToolsJson at line 19 with XML doc comment. |
| `Packages/com.google.ai-embodiment/Runtime/GeminiLiveClient.cs` | Tools in setup message, SendToolResponse, toolCallCancellation parsing | VERIFIED (507 lines, no stubs) | SendSetupMessage injects tools (lines 216-219). SendToolResponse method (lines 136-157). FunctionId capture (line 472). toolCallCancellation parsing (lines 478-494). |
| `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` | Full function calling wiring (native + prompt-based), goal update finalization | VERIFIED (873 lines, no stubs) | RegisterFunction with 3 params (line 310). Connect() wires ToolsJson and prompt instructions. HandleGeminiEvent routes FunctionCallCancellation. HandleFunctionCallEvent passes FunctionId. SendFunctionResponse calls _client.SendToolResponse. ParsePromptFunctionCalls regex parser. SendGoalUpdate logs informational message. |
| `Packages/com.google.ai-embodiment/Runtime/SystemInstructionBuilder.cs` | Prompt-based function instructions appended to system instruction | VERIFIED (115 lines, no stubs) | 3-parameter Build overload (config, goalManager, functionInstructions) at line 43. Appends functionInstructions when non-empty. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| FunctionDeclaration.cs | FunctionRegistry.cs | Register stores declaration, BuildToolsJson iterates declarations | WIRED | FunctionRegistry.RegistryEntry stores Declaration. BuildToolsJson() calls entry.Value.Declaration.ToToolJson() at line 100. BuildPromptInstructions() calls entry.Value.Declaration.ToPromptText() at line 132. |
| FunctionRegistry.cs | GeminiLiveClient.cs | GeminiLiveConfig.ToolsJson carries declarations to setup message | WIRED | PersonaSession.Connect() calls _functionRegistry.BuildToolsJson() and assigns to liveConfig.ToolsJson (line 184). GeminiLiveClient.SendSetupMessage() includes tools when ToolsJson non-null (lines 216-219). |
| GeminiLiveClient.cs | GeminiEvent.cs | toolCall parsing captures FunctionId, toolCallCancellation enqueues events | WIRED | Line 472: FunctionId = fc["id"]?.ToString(). Lines 478-494: toolCallCancellation parsing enqueues FunctionCallCancellation events with FunctionId. |
| PersonaSession.cs | GeminiLiveClient.cs | Connect passes ToolsJson; SendFunctionResponse calls SendToolResponse | WIRED | liveConfig.ToolsJson assignment (line 184). _client.SendToolResponse(callId, name, result) call (line 761). |
| PersonaSession.cs | FunctionRegistry.cs | RegisterFunction passes declaration; Connect builds tools JSON | WIRED | _functionRegistry.Register(name, declaration, handler) at line 312. _functionRegistry.BuildToolsJson() at line 184. _functionRegistry.BuildPromptInstructions() at line 166. _functionRegistry.Freeze() at line 181. _functionRegistry.MarkCancelled at line 441. _functionRegistry.IsCancelled at line 719. |
| PersonaSession.cs | SystemInstructionBuilder.cs | Connect builds system instruction with optional function text | WIRED | SystemInstructionBuilder.Build(_config, _goalManager, functionInstructions) at line 168. |
| PersonaSession.cs | PacketAssembler.cs | HandleFunctionCallEvent passes FunctionId to AddFunctionCall | WIRED | _packetAssembler?.AddFunctionCall(ev.FunctionName, args, ev.FunctionId) at line 639. Prompt-based path also uses _packetAssembler?.AddFunctionCall(funcName, args, null) at line 554. |

### Requirements Coverage

| Requirement | Status | Evidence |
|-------------|--------|----------|
| MIG-04: Function calling works via WebSocket-native tool declarations and function call/response messages | SATISFIED | Full pipeline verified: RegisterFunction with FunctionDeclaration -> BuildToolsJson in setup handshake -> FunctionId capture -> handler dispatch -> SendToolResponse back to Gemini. Both AyaSampleController copies updated to new 3-param API. |
| MIG-05: Conversational goals and mid-session system instruction updates work via WebSocket messages | SATISFIED | Goals accumulate locally via GoalManager (AddGoal/RemoveGoal/ReprioritizeGoal). Applied at Connect() via SystemInstructionBuilder.Build. Mid-session SendGoalUpdate logs informational message about API limitation. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| PacketAssembler.cs | 112 | "Stub for Phase 4 full implementation" in doc comment | Info | Stale doc comment referencing Phase 4 -- the method IS fully implemented (not a stub). AddFunctionCall creates and releases a real SyncPacket. Cosmetic only, no functional impact. |

### Human Verification Required

### 1. Native Function Calling End-to-End

**Test:** Connect to Gemini Live with registered functions (e.g., emote function), ask the AI to perform an emote, verify the function handler fires with correct arguments and the toolResponse is sent back.
**Expected:** AI triggers function call, handler receives FunctionCallContext with correct args and CallId, toolResponse sent, AI continues conversation.
**Why human:** Requires live Gemini API connection and real AI interaction to verify the full round-trip.

### 2. Prompt-Based Fallback Path

**Test:** Set PersonaSession.UseNativeFunctionCalling = false, connect, verify function instructions appear in system prompt, ask the AI to call a function, verify [CALL: name {args}] tags are parsed from transcription and handler fires.
**Expected:** Function instructions appended to system prompt, AI outputs [CALL:] tags, regex parser matches and dispatches to handler.
**Why human:** Requires live Gemini API with audio-only model to verify prompt-based tag generation and parsing.

### 3. Mid-Session Goal Warning

**Test:** Connect, then call AddGoal mid-session, check Unity console for the informational log message.
**Expected:** Debug.Log message appears: "PersonaSession: Goal updated. Mid-session system instruction updates are not supported by the Gemini Live API. Goals will take effect on next connection."
**Why human:** Requires Unity runtime to observe Debug.Log output.

### Gaps Summary

No gaps found. All 10 must-haves from both plans (10-01 and 10-02) verified at all three levels: existence, substantive implementation, and correct wiring.

The function calling infrastructure is complete across all layers:
- **Data types:** FunctionDeclaration builder with flat-primitive parameters, FunctionCallContext with typed accessors
- **Registry:** FunctionRegistry stores declarations alongside handlers, dual-path output (native JSON + prompt text), cancellation tracking
- **Transport:** GeminiLiveClient sends tools in setup, captures FunctionId, sends toolResponse, parses toolCallCancellation
- **Session wiring:** PersonaSession wires the full lifecycle (register -> setup -> dispatch -> response) with both native and prompt-based paths
- **Goal system:** Goals accumulate locally, applied at Connect(), mid-session logs informational message

One cosmetic note: PacketAssembler.cs line 112 has a stale doc comment "Stub for Phase 4 full implementation" but the method is fully implemented. This is informational only.

---

_Verified: 2026-02-13T22:00:00Z_
_Verifier: Claude (gsd-verifier)_
