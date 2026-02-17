---
phase: 14-narrative-director-user-interaction
verified: 2026-02-17T23:59:00Z
status: passed
score: 5/5 must-haves verified
---

# Phase 14: Narrative Director & User Interaction Verification Report

**Phase Goal:** Aya drives a time-based narrative arc through beat/scene structure, responds to user push-to-talk with finish-first priority (completing her current thought before addressing the user), and the dual-queue system orchestrates chat scenes and Aya scenes in parallel
**Verified:** 2026-02-17T23:59:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Aya progresses through a multi-beat narrative arc (warm-up, art process, characters) driven by time and goal escalation | VERIFIED | NarrativeDirector.RunBeatLoop (line 186-242) iterates _beats with time budgets (180/240/180s). CreateBeatAssets.cs defines 3 beats with escalating GoalUrgency (Low/Medium/High). ExecuteBeatTransition sends director notes via SendText. MarkGoalMet and CheckSkipKeywords enable early-exit. |
| 2 | Dual-queue system runs chat scenes in parallel with Aya scenes -- chat does not block Aya and Aya does not block chat | VERIFIED | ChatBotManager.ScriptedBurstLoop runs independently (async Awaitable). NarrativeDirector.ExecuteBeatScenes runs Aya queue scenes sequentially. ChatBurst scene type is explicitly a no-op in NarrativeDirector (line 299-301: "ChatBotManager burst loop handles this"). ChatBotManager pacing integration (GetBurstLullDuration, GetMaxBotsForBurst) slows but never blocks chat. |
| 3 | When user presses push-to-talk while Aya is speaking, visual acknowledgment appears within 500ms and Aya completes her current response before addressing user input | VERIFIED | PushToTalkController.EnterRecording (line 128) checks NarrativeDirector.IsAyaSpeaking. If true: enters WaitingForAya state, calls ShowPTTAcknowledgment(true) -- synchronous CSS class toggle (same frame, <500ms). Does NOT call StartListening (finish-first). HandleTurnComplete (line 71) transitions WaitingForAya -> Recording with StartListening call. |
| 4 | User can review transcribed speech and approve/edit before sending (QueuedResponse pattern) | VERIFIED | EnterReviewing (line 155) shows transcript overlay with SetTranscriptText and 3-second auto-submit countdown. UpdateReviewState (line 172) handles Enter (immediate submit), Escape (cancel/discard), and auto-submit timer. SubmitTranscript has idempotent guard (line 206). User message posted to chat feed via ChatMessage.FromUser (line 213). |
| 5 | Scene transitions fire on conditional triggers (TimedOut, QuestionsAnswered) advancing narrative from one beat to the next | VERIFIED | WaitForCondition (line 403) handles TimedOut (elapsed timer with 1s polling), QuestionsAnswered (_questionsAnsweredCount vs requiredValue with 0.5s polling), and Always (immediate). isConditional flag on NarrativeSceneConfig gates conditional waits. _questionsAnsweredCount resets per beat (line 194). |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Assets/AyaLiveStream/NarrativeBeatConfig.cs` | SO with 3 enums, NarrativeSceneConfig class | VERIFIED | 134 lines. SceneType (5 values), ConditionType (3 values), GoalUrgency (3 values), NarrativeSceneConfig [Serializable] class, NarrativeBeatConfig SO with CreateAssetMenu. All fields have Inspector attributes. No stubs. |
| `Assets/AyaLiveStream/Editor/CreateBeatAssets.cs` | Editor script generating 3 beat assets | VERIFIED | 255 lines. Creates Beat_WarmUp (Low, 180s), Beat_ArtProcess (Medium, 240s), Beat_Characters (High, 180s) with full scene configs via ScriptableObject.CreateInstance + AssetDatabase.CreateAsset. |
| `Assets/AyaLiveStream/NarrativeDirector.cs` | Beat lifecycle, SendText steering, scene execution, dual-queue events | VERIFIED | 470 lines. RunBeatLoop, ExecuteBeatScenes, ExecuteScene (switch on SceneType), ExecuteAyaDialogue, ExecuteAyaChecksChat, WaitForCondition, WaitForAyaIdle, WaitForTurnComplete. All events (OnBeatStarted/Ended/Transition, OnAllBeatsComplete). Clean OnDestroy unsubscription. |
| `Assets/AyaLiveStream/PushToTalkController.cs` | 5-state PTT machine with finish-first logic | VERIFIED | 246 lines. PTTState enum (Idle, WaitingForAya, Recording, Reviewing, Submitted). EnterRecording with IsAyaSpeaking check, deferred StartListening, HandleTurnComplete transitions, transcript review, auto-submit timer, idempotent submit guard. Clean OnDestroy. |
| `Assets/AyaLiveStream/ChatBotManager.cs` | Modified with NarrativeDirector pacing | VERIFIED | 521 lines. _narrativeDirector field (line 35), GetBurstLullDuration (2x min, 1.5x max when Aya speaks, line 202-208), GetMaxBotsForBurst (half when Aya speaks, line 214-218), HandleBeatTransition/ResumeAfterTransition (5s pause, lines 178-196), _pausedForTransition check in ScriptedBurstLoop (lines 239-242). |
| `Assets/AyaLiveStream/LivestreamUI.cs` | Extended with transcript overlay and acknowledgment API | VERIFIED | 254 lines. ShowPTTAcknowledgment, ShowTranscriptOverlay, SetTranscriptText, UpdateAutoSubmitProgress methods. Element queries for transcript-overlay, transcript-text, auto-submit-fill, ptt-ack in OnEnable. |
| `Assets/AyaLiveStream/UI/LivestreamPanel.uxml` | Transcript overlay and ptt-ack elements | VERIFIED | 69 lines. transcript-overlay container with transcript-label, transcript-text, transcript-actions, auto-submit-bar, auto-submit-fill. ptt-ack with ptt-ack-text. |
| `Assets/AyaLiveStream/UI/LivestreamPanel.uss` | Styles for overlay, countdown, acknowledgment | VERIFIED | 253 lines. .ptt-ack, .ptt-ack--hidden/visible, .ptt-ack-text. .transcript-overlay (position absolute, dark theme), .transcript-overlay--hidden/visible, .transcript-label/text/hint, .auto-submit-bar/fill. .chat-panel has position: relative. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| NarrativeDirector | PersonaSession.SendText | Director note at beat transition + AyaDialogue scenes | WIRED | ExecuteBeatTransition (line 256) calls _session.SendText(beat.directorNote). ExecuteAyaDialogue (line 332) calls _session.SendText(dialogue). ExecuteAyaChecksChat (line 381) calls _session.SendText(sb.ToString()). |
| NarrativeDirector | PersonaSession.OnAISpeakingStarted/Stopped | Event subscription for IsAyaSpeaking tracking | WIRED | StartNarrative (lines 95-105) subscribes lambda handlers that set _isAyaSpeaking and update UI. OnDestroy (lines 459-466) unsubscribes via stored references. |
| NarrativeDirector | PersonaSession.OnTurnComplete | Safe director note sending + scene turn tracking | WIRED | HandleTurnComplete (line 137) sets _turnComplete=true and checks _pendingBeatTransition. Subscribed in StartNarrative (line 115). |
| NarrativeDirector | NarrativeBeatConfig.urgency | Urgency-aware beat pacing | WIRED | CurrentBeat property (line 44) exposes beat including urgency. ChatBotManager and downstream systems access CurrentBeat.urgency. |
| NarrativeDirector | ChatBotManager.GetUnrespondedMessages | AyaChecksChat scene execution | WIRED | ExecuteAyaChecksChat (line 348) calls _chatBotManager.GetUnrespondedMessages(). User messages prioritized via IsFromUser check (line 360). |
| ChatBotManager | NarrativeDirector.IsAyaSpeaking | Burst pacing check | WIRED | GetBurstLullDuration (line 204) and GetMaxBotsForBurst (line 216) check _narrativeDirector.IsAyaSpeaking. |
| ChatBotManager | NarrativeDirector.OnBeatTransition | Beat transition pause | WIRED | StartBursts (line 113) subscribes HandleBeatTransition. StopBursts and OnDestroy unsubscribe. |
| PushToTalkController | NarrativeDirector.IsAyaSpeaking | Finish-first check on PTT press | WIRED | EnterRecording (line 131) checks _narrativeDirector.IsAyaSpeaking. |
| PushToTalkController | PersonaSession.StartListening/StopListening | Microphone control, deferred if Aya speaking | WIRED | StartListening called in EnterRecording (line 147, immediate) and HandleTurnComplete (line 74, deferred). StopListening in EnterReviewing (line 158). |
| PushToTalkController | LivestreamUI.ShowTranscriptOverlay | Transcript review UI | WIRED | EnterReviewing (line 163), SubmitTranscript (line 217), CancelTranscript (line 233) all call ShowTranscriptOverlay. |
| PushToTalkController | PersonaSession.OnTurnComplete | Deferred recording trigger + Submitted reset | WIRED | HandleTurnComplete (line 69) handles both WaitingForAya -> Recording and Submitted -> Idle transitions. |

### Requirements Coverage

| Requirement | Status | Blocking Issue |
|-------------|--------|----------------|
| LIVE-02: Beat/scene narrative structure | SATISFIED | All 5 scene types defined, ordered scenes per beat, conditional transitions |
| LIVE-03: Dual-queue system | SATISFIED | Chat queue (ChatBotManager) and Aya queue (NarrativeDirector scenes) run in parallel |
| LIVE-04: Scene types | SATISFIED | AyaDialogue, ChatBurst, AyaChecksChat, AyaAction (placeholder for P15), UserChoice (logged, P14 scope) |
| NAR-01: Time-based narrative arc | SATISFIED | 3 beats: warm-up (180s) -> art process (240s) -> characters (180s) with time-based progression |
| NAR-02: SendText director notes | SATISFIED | Director notes sent at beat transitions and AyaDialogue scenes, guarded by WaitForAyaIdle |
| NAR-03: Goal chain with escalating urgency | SATISFIED | GoalUrgency enum (Low/Medium/High), MarkGoalMet for early exit, urgency accessible via CurrentBeat |
| NAR-06: Conditional scene transitions | SATISFIED | TimedOut, QuestionsAnswered, Always conditions implemented in WaitForCondition |
| USR-01: Push-to-talk with finish-first | SATISFIED | WaitingForAya state defers StartListening until OnTurnComplete |
| USR-02: Visual acknowledgment within 500ms | SATISFIED | Synchronous CSS class toggle in same frame as key press |
| USR-03: Transcript review and approval | SATISFIED | Reviewing state with Enter/Escape/auto-submit, transcript overlay UI |
| MIG-02: Migrate narrative beat/scene data | SATISFIED | Beat structure, scene ordering, scripted messages, cue timing preserved. Simplified from nevatars (5 vs 6 scene types, 3 vs 8 condition types). Deliberate redesign, not migration. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| NarrativeDirector.cs | 304 | "UserChoice not implemented in Phase 14 per scope" | Info | Expected -- UserChoice is scoped to future work |
| NarrativeDirector.cs | 391-395 | "Phase 15 placeholder" for AyaAction | Info | Expected -- AyaAction execution is explicitly Phase 15 scope. Logs the action description, which is correct behavior for Phase 14. |
| NarrativeBeatConfig.cs | 71 | "Placeholder for Phase 15 action description" tooltip | Info | Tooltip on field explaining its Phase 15 purpose, not a code stub |

No blocker or warning-level anti-patterns found. All items are informational and explicitly scoped per the phase plan.

### Human Verification Required

### 1. Visual Appearance of Transcript Overlay
**Test:** Enter Play Mode, hold SPACE to record, release SPACE, observe the transcript overlay sliding up from the bottom of the chat panel.
**Expected:** Dark semi-transparent overlay (rgba(24,24,40,0.95)) with "Your message:" label, transcript text, "ENTER to send | ESC to cancel" hint, and a blue countdown bar that shrinks over 3 seconds.
**Why human:** Visual layout, transition animations, and theme consistency cannot be verified programmatically.

### 2. Visual Appearance of PTT Acknowledgment
**Test:** While Aya is actively speaking (green indicator lit), press SPACE. Observe the "Aya heard you -- finishing her thought..." indicator.
**Expected:** Blue-tinted indicator (rgba(100,200,255,0.15) background) appears within 500ms in the Aya panel, above the PTT area. Italic text in light blue.
**Why human:** Timing perception and visual appearance require human observation.

### 3. Finish-First Recording Flow
**Test:** Press SPACE while Aya is speaking. Hold SPACE. Observe that recording starts only after Aya finishes. Then release SPACE and verify transcript overlay appears.
**Expected:** Acknowledgment shows immediately. Recording state ("Recording...") activates only after Aya's current turn completes. Releasing SPACE then shows transcript for review.
**Why human:** Requires live PersonaSession with actual Gemini connection to verify real-time behavior.

### 4. Narrative Beat Progression
**Test:** Start the livestream experience. Observe Aya's dialogue topics over 10 minutes.
**Expected:** Aya shifts from casual greeting (warm-up) to discussing art process to sharing character stories. Topic shifts are driven by director notes at beat transitions.
**Why human:** Evaluating natural topic shifts from an LLM requires human judgment.

### 5. Chat Pacing During Aya Speech
**Test:** Observe chat burst frequency during and between Aya's speaking turns.
**Expected:** Fewer, slower chat bursts when Aya is speaking (2x min lull, 1.5x max lull, half bot count). Normal frequency when Aya is silent.
**Why human:** Timing perception and organic feel require human observation.

### Gaps Summary

No gaps found. All 5 observable truths are verified. All 8 required artifacts exist, are substantive (134-521 lines), and are properly wired. All 11 key links are confirmed connected. All 11 requirements mapped to this phase are satisfied. The 3 anti-pattern findings are informational -- explicitly scoped placeholders for Phase 15 (AyaAction) and future work (UserChoice), which are correct per the phase plan.

The phase delivers a complete implementation of:
- **NarrativeBeatConfig data model** with 3 enums and inspector-friendly authoring
- **3 beat assets** via editor menu with escalating urgency
- **NarrativeDirector** with beat lifecycle, scene execution, SendText steering, and dual-queue coordination
- **ChatBotManager pacing integration** responsive to Aya's speaking state
- **PushToTalkController** with finish-first state machine and transcript review
- **LivestreamUI extensions** with transcript overlay and acknowledgment indicator

---

_Verified: 2026-02-17T23:59:00Z_
_Verifier: Claude (gsd-verifier)_
