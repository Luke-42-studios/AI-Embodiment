# Phase 10: Function Calling and Goals Migration - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire existing function calling and conversational goals APIs to the WebSocket transport. Developer-facing API (RegisterFunction, AddGoal, RemoveGoal, ReprioritizeGoal) stays the same. The wire protocol changes from Firebase to Gemini Live native. Both native toolCall and prompt-based function calling paths must be implemented due to audio model uncertainty.

</domain>

<decisions>
## Implementation Decisions

### Function schema API
- Typed `FunctionDeclaration` builder class, not raw JObject
- Builder contains name, description, and parameters all together (one object passed to Register)
- Flat primitive parameter types only: string, int, float, bool, enum — no nested objects
- `RegisterFunction(name, declaration, handler)` — declaration is the builder object
- Registry still freezes at connect time (current behavior preserved)

### Goal update strategy
- Best-effort send when goals change mid-session — try sending updated system instruction via WebSocket
- If protocol doesn't support mid-session system instruction updates, log warning; goals apply on next connection
- Silent best-effort — no return value or callback about whether the update took effect
- Goal text stays in system instruction (current SystemInstructionBuilder approach), not user-role messages
- AddGoal/RemoveGoal always work regardless of session state — goals accumulate locally, applied at next Connect()

### Audio model compatibility
- Implement BOTH native toolCall path AND prompt-based fallback
- Audio-native Gemini models (gemini-2.5-flash-native-audio) may not support native function calling — AUDIO-only modality, no TEXT
- Prompt-based fallback: structured trigger phrases in system prompt (e.g., `[CALL: functionName {"arg": "value"}]`) parsed from transcription stream
- Simple flag in code (const bool or static field) to switch between native and prompt-based — easy for developer to flip
- FunctionRegistry builds either toolCall JSON for setup handshake (native) or system prompt instructions (prompt-based) based on flag
- Hope is Google fixes native toolCall for audio models eventually; flag makes it easy to switch back

### Claude's Discretion
- Exact structured trigger format for prompt-based function calling
- How to parse function calls from transcription (regex, string split, etc.)
- toolCallCancellation handling implementation details
- Error handling when function handler throws (current behavior: log + skip response)

</decisions>

<specifics>
## Specific Ideas

- "Gemini Live no longer supports text" — audio-native models are AUDIO-only, which may break native tool use
- The flag should be "just a param that's easy to change for the developer" — not buried in ScriptableObject UI
- Both paths should use the same FunctionDeclaration builder — native path converts to JSON schema, prompt-based path converts to system prompt text

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 10-function-calling-and-goals-migration*
*Context gathered: 2026-02-13*
