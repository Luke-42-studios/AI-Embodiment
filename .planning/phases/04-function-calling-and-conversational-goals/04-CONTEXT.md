# Phase 4: Function Calling and Conversational Goals - Context

**Gathered:** 2026-02-05
**Status:** Ready for planning

<domain>
## Phase Boundary

AI can trigger game actions via function calls with registered C# delegate handlers, and developers can define conversational goals that steer the AI's behavior with urgency-based prioritization. No built-in functions ship with the SDK -- developers register everything themselves.

</domain>

<decisions>
## Implementation Decisions

### No Built-in Functions
- No emote, goal_reached, or any other function ships pre-registered
- The SDK provides the registration infrastructure only
- Developers register all functions themselves, including any goal completion signaling

### Function Registration API
- Functions registered before Connect() -- schema is fixed for the session lifetime
- Registration API design is Claude's discretion, guided by Unity-idiomatic patterns (schema object + delegate style preferred over fluent builder)
- Full JSON Schema support for parameters (nested objects, arrays, enums) -- matches Gemini's full function calling spec
- Optional return values -- handlers CAN return a value that gets sent back to the AI automatically as a function response (enables query-type functions like getHealth)
- Fire-and-forget functions also supported (no return value needed)

### Function Dispatch
- Function call handlers fire on the main thread via SyncPacket processing -- they are part of the synchronization timeline
- This means function calls are naturally synchronized with audio/text (e.g., an emote function fires at the right moment in speech)
- Automatic response flow -- when a handler returns a value, SDK sends it back to Gemini without developer intervention

### Function Error Handling
- If a handler throws an exception, surface it to the developer via an OnFunctionError event
- Conversation continues despite handler errors -- errors are reported, not fatal

### Goal Definition
- Goals are simple: natural language description + priority level (low, medium, high)
- No structured success criteria, timeouts, or tags -- keep it minimal
- Example: session.AddGoal("convince player to visit the blacksmith", Priority.High)

### Goal Lifecycle
- Goals can be added and removed at runtime
- Priority is mutable -- developer can reprioritize goals as game state changes
- Goal completion is developer-managed: developer registers their own function (e.g., "goal_reached") and handles completion in their callback
- No SDK-provided completion mechanism

### Goal Instruction Sync
- When goals are added, removed, or reprioritized at runtime, the updated system instruction is sent immediately to the live session
- AI adjusts behavior right away, no waiting for next turn

### Claude's Discretion
- Exact registration API shape (schema object + delegate preferred, but implementation details flexible)
- Handler delegate signature design (typed context object vs raw dictionary)
- How priority levels translate to urgency framing in system instructions
- Internal goal storage and system instruction rebuilding strategy

</decisions>

<specifics>
## Specific Ideas

- Functions should sync with audio via SyncPackets -- "if we're using the emote function it should sync with audio, but some functions might need more data" (return values solve the data-fetching case)
- Keep it easy for developers to add functions and have them called -- low ceremony registration

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 04-function-calling-and-conversational-goals*
*Context gathered: 2026-02-05*
