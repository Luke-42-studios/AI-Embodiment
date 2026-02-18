# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-17)

**Core value:** Developers can drop an AI character into their Unity scene and have it talking -- with synchronized voice, text, and animation events -- in minutes, not weeks.
**Current focus:** Phase 15.1 - Audio2Animation Pipeline

## Current Position

Phase: 15.1 (Audio2Animation Pipeline - INSERTED)
Plan: 1 of 2 in current phase
Status: In progress
Last activity: 2026-02-17 - Completed 15.1-01-PLAN.md (Audio2Animation pipeline core)

Progress: [█████████████░░] 13/17 plans (76%)

## Performance Metrics

**Velocity:**
- v1 MVP: 17 plans in ~1 day
- v0.8 WebSocket Migration: 14 plans in ~5 days
- Total plans completed: 42
- v1.0 Livestream Experience: 17 plans planned

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 12 | 3 | - | - |
| 13 | 3 | - | - |
| 14 | 4 | - | - |
| 15 | 2 | - | - |
| 15.1 | 1 | 2min | 2min |
| 16 | 3 | - | - |

## Accumulated Context

### Decisions

Full decision log in PROJECT.md Key Decisions table.

- Hybrid chat bots: scripted + Gemini structured output (low latency ambient, dynamic for user interactions)
- Finish-first priority: Aya completes current response before addressing user input
- Dual Gemini path: Live WebSocket for Aya, REST generateContent for chat bots
- Zero package modifications: all new code in Samples~/LivestreamSample/
- SendText director notes for mid-session narrative steering (Live API cannot update system instructions)
- Clean exit scene transition for movie clip (LoadSceneMode.Single, explicit Disconnect before load -- supersedes additive loading decision)
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
- Instant cut scene transition (no fade, no crossfade) per Phase 15 CONTEXT.md
- Application.CanStreamedLevelBeLoaded pre-check with Debug.LogError for missing Build Settings entry
- Single play_animation function with enum parameter (not one function per animation) from AnimationConfig ScriptableObject
- HandleFunctionError decoupled from AyaChatUI, uses Debug.LogError directly
- SetFrameCallback on IAnimationModel (not event) for constructor-time wiring from Audio2Animation
- Tick() excluded from IAnimationModel interface -- implementation detail of time-based models
- Cancel() preserves frame index -- next audio resumes from where playback left off

### Roadmap Evolution

- Phase 15.1 inserted after Phase 15: Audio2Animation pipeline in package runtime (URGENT) -- audio-to-blendshape sync driver with fake model, streams pre-recorded animation JSON synchronized to TTS audio chunks

### Pending Todos

None.

### Blockers/Concerns

- RSA.ImportPkcs8PrivateKey may fail on Android IL2CPP (editor-only for now)
- SendText director note reliability needs early validation in Phase 14
- ListView dynamic height verified working in Play Mode (user approved visual checkpoint)
- Context window growth over 10-15 min session with bot message injection -- monitor in Phase 16

## Session Continuity

Last session: 2026-02-17
Stopped at: Completed 15.1-01-PLAN.md (Audio2Animation pipeline core)
Resume file: None
