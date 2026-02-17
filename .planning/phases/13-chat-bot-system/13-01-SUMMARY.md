---
phase: 13-chat-bot-system
plan: 01
subsystem: chat
tags: [unity, monobehaviour, async-awaitable, fisher-yates, burst-timing, chat-bot]

# Dependency graph
requires:
  - phase: 12-foundation-and-data-model
    provides: ChatBotConfig ScriptableObjects, ChatMessage data model, LivestreamUI with AddMessage()
provides:
  - ChatBotManager MonoBehaviour with scripted burst loop
  - TrackedChatMessage wrapper with AyaHasResponded tracking
  - GetUnrespondedMessages() query API for downstream phases
affects: [13-02-dynamic-responses, 14-narrative-director, 16-polish]

# Tech tracking
tech-stack:
  added: []
  patterns: [async-awaitable-loops, fisher-yates-shuffle, per-bot-index-tracking, personality-transforms]

key-files:
  created:
    - Assets/AyaLiveStream/ChatBotManager.cs
    - Assets/AyaLiveStream/TrackedChatMessage.cs
  modified: []

key-decisions:
  - "Combined scriptedMessages + messageAlternatives into single pool for PickMessage"
  - "Used array index tracking (not hash-based) for message deduplication"
  - "Emoji set as static readonly array for ApplyPersonality transforms"

patterns-established:
  - "Burst loop pattern: async Awaitable with destroyCancellationToken and try/catch OperationCanceledException"
  - "Per-bot used-index tracking: Dictionary<ChatBotConfig, List<int>> with full-cycle reset"
  - "Personality transforms: capsFrequency and emojiFrequency applied as independent random rolls"

# Metrics
duration: 2min
completed: 2026-02-17
---

# Phase 13 Plan 01: Scripted Burst Loop & Tracking Summary

**ChatBotManager with Fisher-Yates shuffled burst scheduling, per-bot personality transforms, and TrackedChatMessage tracking system**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-17T22:38:59Z
- **Completed:** 2026-02-17T22:40:25Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- TrackedChatMessage wrapper class with AyaHasResponded flag and PostedAtTime for recency filtering
- ChatBotManager MonoBehaviour with organic burst-and-lull scripted message posting (8-18s lull, 0.8-3.0s stagger)
- Fisher-Yates shuffle for random bot selection (1-4 per burst) and per-bot message picking with full-cycle dedup
- Per-bot personality transforms (CAPS, emoji append) driven by ChatBotConfig behavior fields
- GetUnrespondedMessages() and AllTrackedMessages query API ready for Phase 14/16 consumption

## Task Commits

Each task was committed atomically:

1. **Task 1: Create TrackedChatMessage wrapper class** - `3484249` (feat)
2. **Task 2: Create ChatBotManager with scripted burst loop** - `d2bdb83` (feat)

**Plan metadata:** (see final commit)

## Files Created/Modified
- `Assets/AyaLiveStream/TrackedChatMessage.cs` - Thin wrapper around ChatMessage with AyaHasResponded tracking and PostedAtTime
- `Assets/AyaLiveStream/ChatBotManager.cs` - MonoBehaviour orchestrating scripted burst loop with organic timing, Fisher-Yates shuffle, personality transforms, and tracked message system

## Decisions Made
- Combined scriptedMessages and messageAlternatives into a single indexed pool for PickMessage, with scriptedMessages occupying indices 0..N-1 and messageAlternatives occupying N..N+M-1
- Used simple array index tracking (Dictionary<ChatBotConfig, List<int>>) rather than hash-based deduplication -- simpler, deterministic, no collision risk
- Emoji set defined as static readonly string array (fire, heart, star, sparkles, raised hands, heart eyes, 100, party) for ApplyPersonality transforms

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- ChatBotManager ready for Inspector wiring: assign LivestreamUI reference and 6 ChatBotConfig assets
- StartBursts()/StopBursts() API ready for AyaSampleController to call when session goes live
- TrackedChatMessage and GetUnrespondedMessages() API ready for Plan 02 (dynamic responses) and Phase 14 (narrative director)
- No GeminiTextClient or PersonaSession wiring yet -- reserved for Plan 02

---
*Phase: 13-chat-bot-system*
*Completed: 2026-02-17*
