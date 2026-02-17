---
phase: 13-chat-bot-system
plan: 02
subsystem: chat
tags: [unity, gemini-rest, structured-output, async-awaitable, push-to-talk, dynamic-responses]

# Dependency graph
requires:
  - phase: 12-foundation-and-data-model
    provides: GeminiTextClient REST wrapper, ChatBotConfig ScriptableObjects, ChatMessage data model, LivestreamUI with AddMessage()
  - phase: 13-chat-bot-system
    plan: 01
    provides: ChatBotManager with scripted burst loop, TrackedChatMessage wrapper
provides:
  - BotReaction deserialization class for Gemini structured output
  - Dynamic Gemini response path in ChatBotManager (HandleUserSpeechAsync, BuildDynamicPrompt, FindBotByName)
  - PersonaSession event wiring for user push-to-talk transcript accumulation
  - Rapid PTT guard with transcript queuing
affects: [13-03-migrate-response-patterns, 14-narrative-director, 16-polish]

# Tech tracking
tech-stack:
  added: []
  patterns: [gemini-structured-output-batching, event-driven-async-response, transcript-accumulation, name-normalization]

key-files:
  created:
    - Assets/AyaLiveStream/BotReaction.cs
  modified:
    - Assets/AyaLiveStream/ChatBotManager.cs

key-decisions:
  - "Single batched Gemini call returns 1-3 BotReaction objects (not one call per bot)"
  - "Dynamic responses trigger ONLY from user push-to-talk (OnUserSpeakingStopped), not from Aya or other bots"
  - "Transcript accumulation via direct assignment (OnInputTranscription provides full text, not deltas)"
  - "Rapid PTT queuing: second transcript stored in _queuedTranscript, processed after first completes"
  - "FindBotByName uses underscore/space normalization + case-insensitive matching for Gemini name variations"

patterns-established:
  - "Event-driven async response: subscribe to PersonaSession events, fire-and-forget async Awaitable"
  - "In-flight guard pattern: _dynamicResponseInFlight bool with _queuedTranscript fallback"
  - "Name normalization: Replace('_', ' ').ToLowerInvariant() for fuzzy bot name matching"
  - "Gemini structured output schema: static readonly JObject with UPPERCASE type constants"

# Metrics
duration: 3min
completed: 2026-02-17
---

# Phase 13 Plan 02: Dynamic Gemini Responses Summary

**Batched Gemini structured output for 1-3 bot reactions to user push-to-talk speech with staggered timing, personality matching, and rapid PTT guard**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-17T22:44:47Z
- **Completed:** 2026-02-17T22:47:21Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- BotReaction deserialization class for Gemini structured output (botName, message, delay fields)
- Dynamic response path in ChatBotManager triggered by PersonaSession push-to-talk events
- Single batched Gemini REST call via GeminiTextClient.GenerateAsync<BotReaction[]> with DynamicResponseSchema
- BuildDynamicPrompt includes all 6 bot personalities so Gemini picks natural responders
- FindBotByName with case-insensitive, underscore-normalized matching for Gemini name variations
- Rapid PTT guard prevents duplicate in-flight Gemini calls (queues second transcript)
- Clean event lifecycle: subscribe in StartBursts, unsubscribe in StopBursts and OnDestroy

## Task Commits

Each task was committed atomically:

1. **Task 1: Create BotReaction deserialization class** - `c09022e` (feat)
2. **Task 2: Extend ChatBotManager with dynamic Gemini response path** - `801eca8` (feat)

**Plan metadata:** (see final commit)

## Files Created/Modified
- `Assets/AyaLiveStream/BotReaction.cs` - Serializable deserialization target for Gemini structured output with botName, message, and delay fields
- `Assets/AyaLiveStream/ChatBotManager.cs` - Extended with dynamic response path: PersonaSession event wiring, GeminiTextClient lifecycle, HandleUserSpeechAsync, BuildDynamicPrompt, FindBotByName, DynamicResponseSchema, rapid PTT guard

## Decisions Made
- Single batched Gemini call returns all bot reactions (1-3) per user speech event -- not one call per bot (CONTEXT.md requirement, cost-efficient)
- Dynamic responses trigger ONLY from OnUserSpeakingStopped, never from Aya's dialogue or other bots (explicit CONTEXT.md boundary)
- OnInputTranscription provides full accumulated text each invocation -- direct assignment, not concatenation
- Rapid PTT queuing stores second transcript in _queuedTranscript and processes after first completes (Pitfall 6 mitigation)
- FindBotByName normalizes underscores to spaces and lowercases for case-insensitive matching (Pitfall 4 mitigation)
- GeminiTextClient created in StartBursts and disposed in StopBursts/OnDestroy for clean lifecycle

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added missing `using AIEmbodiment` directive**
- **Found during:** Task 2 (ChatBotManager extension)
- **Issue:** PersonaSession and AIEmbodimentSettings are in the AIEmbodiment namespace, not AIEmbodiment.Samples -- without the using directive the file would not compile
- **Fix:** Added `using AIEmbodiment;` to the imports
- **Files modified:** Assets/AyaLiveStream/ChatBotManager.cs
- **Verification:** Confirmed other files in AIEmbodiment.Samples (AyaSampleController.cs, AyaChatUI.cs) use the same import
- **Committed in:** 801eca8 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Essential for compilation. No scope creep.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. GeminiTextClient uses the existing API key from AIEmbodimentSettings.

## Next Phase Readiness
- ChatBotManager now has both scripted burst loop (Plan 01) AND dynamic Gemini response path running simultaneously
- PersonaSession must be assigned in Inspector for dynamic responses to activate (null-checked gracefully)
- Plan 03 (MigrateResponsePatterns) will populate messageAlternatives to expand scripted message pools
- Phase 14 (narrative director) can consume TrackedChatMessage from both scripted and dynamic responses via GetUnrespondedMessages()

---
*Phase: 13-chat-bot-system*
*Completed: 2026-02-17*
