---
phase: 10-function-calling-and-goals-migration
plan: 01
subsystem: api
tags: [function-calling, gemini-live, websocket, json, newtonsoft]

# Dependency graph
requires:
  - phase: 08-personasession-migration-and-dependency-removal
    provides: FunctionRegistry with handler storage and cancellation tracking, GeminiLiveClient with toolCall parsing
provides:
  - Typed FunctionDeclaration builder with flat-primitive parameter support
  - FunctionRegistry dual-path output (native JSON + prompt text)
  - GeminiEvent.FunctionId for response correlation
  - GeminiLiveConfig.ToolsJson for tool declarations in setup
  - GeminiLiveClient.SendToolResponse for function call responses
  - toolCallCancellation parsing in GeminiLiveClient
affects: [10-02-function-calling-and-goals-wiring]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "FunctionDeclaration fluent builder for typed function schemas"
    - "Dual-path function calling: native JSON tools + prompt-based fallback text"
    - "toolResponse wire format: {toolResponse: {functionResponses: [{id, name, response}]}}"

key-files:
  created:
    - "Packages/com.google.ai-embodiment/Runtime/FunctionDeclaration.cs"
  modified:
    - "Packages/com.google.ai-embodiment/Runtime/FunctionRegistry.cs"
    - "Packages/com.google.ai-embodiment/Runtime/GeminiEvent.cs"
    - "Packages/com.google.ai-embodiment/Runtime/GeminiLiveConfig.cs"
    - "Packages/com.google.ai-embodiment/Runtime/GeminiLiveClient.cs"
    - "Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs"
    - "Assets/AyaLiveStream/AyaSampleController.cs"
    - "Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs"
    - "README.md"

key-decisions:
  - "FunctionDeclaration uses inner ParameterDef class (not public struct) to encapsulate parameter storage"
  - "AddEnum uses STRING type with enum constraint (matching Gemini API schema convention)"
  - "ToToolJson omits parameters key entirely when zero parameters defined"
  - "BuildPromptInstructions returns null (not empty string) when no registrations"
  - "SendToolResponse takes IDictionary<string,object> and converts via JObject.FromObject"

patterns-established:
  - "FunctionDeclaration fluent builder: AddType() methods return this for chaining"
  - "FunctionRegistry.RegistryEntry bundles Handler + Declaration as private inner class"
  - "BuildToolsJson wraps declarations in [{functionDeclarations: [...]}] array format"

# Metrics
duration: 4min
completed: 2026-02-13
---

# Phase 10 Plan 01: Function Calling Infrastructure Summary

**Typed FunctionDeclaration builder with dual-path output (native JSON + prompt text), FunctionId on events, tools in setup handshake, SendToolResponse, and toolCallCancellation parsing**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-13T21:33:05Z
- **Completed:** 2026-02-13T21:37:04Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- FunctionDeclaration builder class with AddString/AddInt/AddFloat/AddBool/AddEnum fluent API and dual output (ToToolJson + ToPromptText)
- FunctionRegistry upgraded to store declarations alongside handlers, with BuildToolsJson and BuildPromptInstructions for native and prompt-based paths
- GeminiLiveClient transport upgrades: tools in setup message, FunctionId capture, SendToolResponse method, toolCallCancellation parsing

## Task Commits

Each task was committed atomically:

1. **Task 1: FunctionDeclaration builder and FunctionRegistry upgrade** - `dfba533` (feat)
2. **Task 2: GeminiEvent, GeminiLiveConfig, and GeminiLiveClient transport upgrades** - `6a1329b` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/FunctionDeclaration.cs` - NEW: Typed builder for Gemini function declarations with flat-primitive parameters
- `Packages/com.google.ai-embodiment/Runtime/FunctionRegistry.cs` - Upgraded to store declarations, added BuildToolsJson() and BuildPromptInstructions()
- `Packages/com.google.ai-embodiment/Runtime/GeminiEvent.cs` - Added FunctionId field and FunctionCallCancellation enum value
- `Packages/com.google.ai-embodiment/Runtime/GeminiLiveConfig.cs` - Added ToolsJson field for tool declarations
- `Packages/com.google.ai-embodiment/Runtime/GeminiLiveClient.cs` - Tools in setup, FunctionId capture, SendToolResponse, toolCallCancellation parsing
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` - Updated RegisterFunction signature to accept FunctionDeclaration
- `Assets/AyaLiveStream/AyaSampleController.cs` - Updated to new 3-parameter RegisterFunction with FunctionDeclaration objects
- `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs` - Updated to new 3-parameter RegisterFunction with FunctionDeclaration objects
- `README.md` - Updated function calling example to new FunctionDeclaration builder API

## Decisions Made
- FunctionDeclaration uses inner ParameterDef class (not public struct) to encapsulate parameter storage -- keeps API surface clean
- AddEnum uses STRING type with enum constraint (matching Gemini API schema convention where enums are strings with allowed values)
- ToToolJson omits parameters key entirely when zero parameters defined -- cleaner JSON for parameterless functions
- BuildToolsJson and BuildPromptInstructions return null (not empty) when no registrations -- caller can use null check
- SendToolResponse takes IDictionary<string,object> and converts via JObject.FromObject -- flexible for any response shape

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated sample controllers and README for new RegisterFunction signature**
- **Found during:** Task 2 (transport upgrades)
- **Issue:** FunctionRegistry.Register signature changed from 2 params to 3 params, breaking AyaSampleController callers in Samples~ and Assets/
- **Fix:** Updated both AyaSampleController.cs files to use proper FunctionDeclaration objects with the 3-parameter RegisterFunction. Updated README example to use new builder API.
- **Files modified:** Assets/AyaLiveStream/AyaSampleController.cs, Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs, README.md
- **Verification:** All callers now use correct 3-parameter signature
- **Committed in:** 6a1329b (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Auto-fix necessary to maintain compilability after signature change. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All function calling infrastructure is in place for Plan 10-02 to wire into PersonaSession
- FunctionDeclaration builder, dual-path registry output, FunctionId flow, SendToolResponse, and cancellation parsing are all ready
- Plan 10-02 can focus on: PersonaSession wiring (native flag, tools in config, function response sending, goal update logic)

---
*Phase: 10-function-calling-and-goals-migration*
*Completed: 2026-02-13*
