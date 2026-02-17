# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-17)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** Phase 14 - Narrative Director & User Interaction

## Current Position

Phase: 14 of 16 (Narrative Director & User Interaction)
Plan: 4 of 4 in current phase
Status: In progress
Last activity: 2026-02-17 -- Completed 14-04-PLAN.md

Progress: [██████████░░░░░] 10/15 plans (67%)

## Performance Metrics

**Velocity:**
- v1 MVP: 17 plans in ~1 day
- v0.8 WebSocket Migration: 14 plans in ~5 days
- Total plans completed: 41
- v1.0 Livestream Experience: 15 plans planned

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 12 | 3 | - | - |
| 13 | 3 | - | - |
| 14 | 4 | - | - |
| 15 | 2 | - | - |
| 16 | 3 | - | - |

## Accumulated Context

### Decisions

Full decision log in PROJECT.md Key Decisions table.

- Hybrid chat bots: scripted + Gemini structured output (low latency ambient, dynamic for user interactions)
- Finish-first priority: Aya completes current response before addressing user input
- Dual Gemini path: Live WebSocket for Aya, REST generateContent for chat bots
- Zero package modifications: all new code in Samples~/LivestreamSample/
- SendText director notes for mid-session narrative steering (Live API cannot update system instructions)
- Additive scene loading for movie clip (preserves WebSocket + chat state)
- Combined scriptedMessages + messageAlternatives as single indexed pool in ChatBotManager.PickMessage
- Burst loop pattern: async Awaitable with destroyCancellationToken, try/catch OperationCanceledException
- Keyword-hit scoring for nevatars message categorization across 6 bot personalities
- Lurker matched by message length (1-3 words), not keywords
- Single batched Gemini call for dynamic bot reactions (not one call per bot)
- Dynamic responses trigger ONLY from user PTT (OnUserSpeakingStopped), never from Aya or bots
- Rapid PTT guard: _dynamicResponseInFlight with _queuedTranscript queuing
- Editor script approach for beat asset generation (avoids GUID issues, follows MigrateChatBotConfigs pattern)
- GoalUrgency enum (Low/Medium/High) for escalating narrative intensity across beats
- Pending beat queue: director notes queued for OnTurnComplete when Aya is speaking (Pitfall 3 guard)
- Event-based NarrativeDirector coordination: IsAyaSpeaking, OnBeatTransition (never direct field manipulation)
- ChatBotManager pacing: 2x min lull, 1.5x max, half bots when Aya speaks; 5s pause at beat transitions
- Scene execution within beat time budget: sequential scenes with early exit on budget expiry or goal-met
- AyaChecksChat summary-based injection: StringBuilder director note, not raw messages (Pitfall 8: context window)
- User messages always prioritized over bot messages in AyaChecksChat (foreach, not LINQ)
- Per-beat _questionsAnsweredCount for QuestionsAnswered conditional transitions
- WaitingForAya PTT sub-state defers mic until Aya finishes (prevents Gemini audio interruption)
- Idempotent SubmitTranscript guard prevents Enter + auto-submit timer race (Pitfall 6)
- ChatBotManager NOT paused during PTT (only Aya pauses, chat keeps flowing)

### Pending Todos

None.

### Blockers/Concerns

- RSA.ImportPkcs8PrivateKey may fail on Android IL2CPP (editor-only for now)
- SendText director note reliability needs early validation in Phase 14
- ListView dynamic height verified working in Play Mode (user approved visual checkpoint)
- Context window growth over 10-15 min session with bot message injection -- monitor in Phase 16

## Session Continuity

Last session: 2026-02-17T23:52:42Z
Stopped at: Completed 14-03-PLAN.md (Scene Execution) and 14-04-PLAN.md (Push-to-Talk Controller)
Resume file: None
