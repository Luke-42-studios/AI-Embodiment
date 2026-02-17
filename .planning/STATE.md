# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-17)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** Phase 14 - Narrative Director & User Interaction

## Current Position

Phase: 14 of 16 (Narrative Director & User Interaction)
Plan: 0 of 4 in current phase
Status: Ready to plan
Last activity: 2026-02-17 -- Phase 13 complete

Progress: [██████░░░░░░░░░] 6/15 plans (40%)

## Performance Metrics

**Velocity:**
- v1 MVP: 17 plans in ~1 day
- v0.8 WebSocket Migration: 14 plans in ~5 days
- Total plans completed: 37
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

### Pending Todos

None.

### Blockers/Concerns

- RSA.ImportPkcs8PrivateKey may fail on Android IL2CPP (editor-only for now)
- SendText director note reliability needs early validation in Phase 14
- ListView dynamic height verified working in Play Mode (user approved visual checkpoint)
- Context window growth over 10-15 min session with bot message injection -- monitor in Phase 16

## Session Continuity

Last session: 2026-02-17T23:00:00Z
Stopped at: Phase 13 complete (Chat Bot System)
Resume file: None
