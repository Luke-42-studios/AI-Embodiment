---
phase: 16-integration-experience-loop
verified: 2026-02-17T22:30:00Z
status: passed
score: 4/4 must-haves verified
must_haves:
  truths:
    - "Developer can open LivestreamSample scene, enter Play Mode, and experience full loop without manual intervention beyond PTT"
    - "Chat bots act as narrative catalysts -- bot questions steer Aya toward the next beat"
    - "User push-to-talk questions about upcoming content accelerate the narrative arc"
    - "Cross-system coherence -- Aya acknowledges bot messages, bots react to what Aya said, no bot asserts unmentioned facts"
  artifacts:
    - path: "Assets/AyaLiveStream/LivestreamController.cs"
      provides: "Top-level orchestrator with connection-wait gate and subsystem wiring"
    - path: "Assets/AyaLiveStream/FactTracker.cs"
      provides: "Shared fact dictionary for cross-system coherence"
    - path: "Assets/AyaLiveStream/AyaTranscriptBuffer.cs"
      provides: "Ring buffer of Aya transcript turns for prompt enrichment"
    - path: "Assets/AyaLiveStream/ChatBotManager.cs"
      provides: "Context-enriched dynamic prompts and 25% catalyst message selection"
    - path: "Assets/AyaLiveStream/NarrativeDirector.cs"
      provides: "Beat lifecycle with FactTracker integration and topicKeywords skip-ahead"
    - path: "Assets/AyaLiveStream/NarrativeBeatConfig.cs"
      provides: "catalystGoal, catalystMessages, topicKeywords fields"
    - path: "Assets/AyaLiveStream/LivestreamUI.cs"
      provides: "SetLoadingState, ShowGoingLive, ShowThinkingIndicator UI extensions"
    - path: "Assets/AyaLiveStream/Editor/CreateBeatAssets.cs"
      provides: "3-beat narrative arc with catalyst content authored per beat"
    - path: "Assets/AyaLiveStream/Editor/CreateLivestreamScene.cs"
      provides: "One-click scene setup wiring all SerializeField references"
    - path: "Assets/AyaLiveStream/SceneTransitionHandler.cs"
      provides: "Clean scene exit to movie clip on OnAllBeatsComplete"
    - path: "Assets/AyaLiveStream/PushToTalkController.cs"
      provides: "Finish-first PTT state machine with transcript review"
  key_links:
    - from: "LivestreamController.cs"
      to: "ChatBotManager.SetContextProviders"
      via: "Start() method call on line 61"
    - from: "LivestreamController.cs"
      to: "NarrativeDirector.SetFactTracker"
      via: "Start() method call on line 62"
    - from: "LivestreamController.cs"
      to: "ChatBotManager.SetCurrentBeat"
      via: "OnBeatStarted subscription on line 65"
    - from: "ChatBotManager.BuildDynamicPrompt"
      to: "AyaTranscriptBuffer.GetRecentTurns"
      via: "line 519 in BuildDynamicPrompt"
    - from: "ChatBotManager.BuildDynamicPrompt"
      to: "FactTracker.GetFactsSummary"
      via: "line 531 in BuildDynamicPrompt"
    - from: "NarrativeDirector.ExecuteBeatTransition"
      to: "FactTracker.SetFact"
      via: "lines 300-304 in ExecuteBeatTransition"
    - from: "NarrativeDirector.CheckSkipKeywords"
      to: "topicKeywords"
      via: "lines 175-195 iterating future beats"
    - from: "ChatBotManager.PickMessage"
      to: "catalystMessages"
      via: "lines 308-317 with 25% random check"
    - from: "SceneTransitionHandler"
      to: "NarrativeDirector.OnAllBeatsComplete"
      via: "OnEnable subscription on line 32"
human_verification:
  - test: "Open Unity, run Create Demo Beat Assets then Create Livestream Scene, enter Play Mode"
    expected: "Loading overlay -> GOING LIVE! -> Aya greets -> bots post messages (some catalysts) -> beats progress -> movie scene loads"
    why_human: "Requires Unity Editor with Gemini API key, scene setup, and real-time observation of 10-minute experience"
  - test: "During Play Mode, use push-to-talk (SPACE) and mention movie or film"
    expected: "Narrative jumps ahead via topicKeywords match, Aya transitions toward reveal faster"
    why_human: "Requires live speech input and observation of narrative acceleration"
  - test: "Observe chat feed during Play Mode for ~2 minutes"
    expected: "Roughly 1 in 4 burst messages are catalyst messages matching current beat goal"
    why_human: "25% rate is statistical, needs human observation over time"
  - test: "Observe bot dynamic responses after PTT input"
    expected: "Bot responses reference what Aya recently said and do not contradict established facts"
    why_human: "Requires Gemini API response evaluation and coherence judgment"
---

# Phase 16: Integration & Experience Loop Verification Report

**Phase Goal:** The complete livestream sample scene runs as a cohesive 10-minute experience -- Aya hosts, bots chat, user talks, narrative builds, and the movie clip reveals -- with cross-system context injection ensuring coherence
**Verified:** 2026-02-17T22:30:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Developer can open LivestreamSample scene, enter Play Mode, and experience full loop from Aya greeting through narrative progression to movie clip reveal without manual intervention beyond PTT | VERIFIED | LivestreamController.cs (307 lines) orchestrates full lifecycle: InitializeAndStart() polls for connection, shows "GOING LIVE!", calls StartBursts() and StartNarrative(), MonitorDeadAir() runs background. CreateLivestreamScene.cs (294 lines) provides one-click scene setup wiring all 6 subsystem SerializeField references. SceneTransitionHandler.cs subscribes to OnAllBeatsComplete and calls session.Disconnect() + SceneManager.LoadSceneAsync(). |
| 2 | Chat bots act as narrative catalysts -- bot questions steer Aya toward next beat | VERIFIED | ChatBotManager.PickMessage() lines 308-317: checks `_currentBeat.catalystMessages`, rolls `Random.value < 0.25f`, returns catalyst message bypassing per-bot pool. CreateBeatAssets.cs authors 3/4/5 catalyst messages per beat with escalating urgency ("ooh what are you drawing" -> "omg are you gonna show us the thing??"). SetCurrentBeat() called via LivestreamController OnBeatStarted subscription (line 65). |
| 3 | User PTT questions about upcoming content accelerate narrative arc | VERIFIED | NarrativeDirector.CheckSkipKeywords() lines 172-195: iterates future beats (`_currentBeatIndex + 1` to `_beats.Length - 1`), checks topicKeywords with case-insensitive IndexOf, sets `_currentBeatIndex = i - 1` and `_beatGoalMet = true` to trigger early beat exit. Beat 2 has topicKeywords ["character", "design", "drawing", "sketch", "art style"]. Beat 3 has topicKeywords ["movie", "film", "clip", "video", "animation", "reveal", "show"]. Existing skipKeywords mechanism on Beat 2 preserved for fast-forward-to-finale. |
| 4 | Cross-system coherence -- Aya acknowledges bot messages, bots react to what Aya said, no bot asserts unmentioned facts | VERIFIED | Three-layer coherence system: (a) AyaTranscriptBuffer (76 lines) accumulates Aya speech via OnOutputTranscription/OnTurnComplete, injected into ChatBotManager.BuildDynamicPrompt() as "Aya (the host) has been saying:" (line 519-525). (b) FactTracker (59 lines) records beat-level facts via NarrativeDirector.ExecuteBeatTransition() (lines 298-304) and RunBeatLoop() (line 274), injected into BuildDynamicPrompt() as "Established facts (do NOT contradict these):" (lines 529-537). (c) TrackedChatMessage system (via GetUnrespondedMessages()) prevents Aya from double-acknowledging bot messages in AyaChecksChat scenes. |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Assets/AyaLiveStream/LivestreamController.cs` | Top-level orchestrator | VERIFIED (307 lines) | Substantive: 7 SerializeField refs, Start() creates context objects + wires subsystems + subscribes events, InitializeAndStart() with connection-wait gate + "going live" transition, MonitorDeadAir() background loop, HandleNarrativeComplete() orderly shutdown, OnDestroy() full unsubscription. No stubs. |
| `Assets/AyaLiveStream/FactTracker.cs` | Shared fact dictionary | VERIFIED (59 lines) | SetFact/HasFact/GetFactsSummary all implemented. Logs on change. Dictionary<string,bool> backing store. |
| `Assets/AyaLiveStream/AyaTranscriptBuffer.cs` | Ring buffer of Aya turns | VERIFIED (76 lines) | AppendText/CompleteTurn/GetRecentTurns all implemented. Correct maxTurns trim logic. |
| `Assets/AyaLiveStream/ChatBotManager.cs` | Context-enriched prompts + catalyst selection | VERIFIED (582 lines) | SetContextProviders (line 101), SetCurrentBeat (line 111), BuildDynamicPrompt includes Aya transcript (line 517-525) and facts (line 529-537), PickMessage has 25% catalyst (lines 308-317). |
| `Assets/AyaLiveStream/NarrativeDirector.cs` | FactTracker + topicKeywords skip-ahead | VERIFIED (523 lines) | SetFactTracker (line 137), ExecuteBeatTransition sets facts (lines 298-304), RunBeatLoop records completion facts (line 274), CheckSkipKeywords has topicKeywords section (lines 172-195) BEFORE existing skipKeywords section. |
| `Assets/AyaLiveStream/NarrativeBeatConfig.cs` | Catalyst authoring fields | VERIFIED (146 lines) | catalystGoal (string, line 137), catalystMessages (string[], line 141), topicKeywords (string[], line 144) under [Header("Catalyst")] on line 134. |
| `Assets/AyaLiveStream/LivestreamUI.cs` | Loading/going-live/thinking UI | VERIFIED (354 lines) | SetLoadingState (line 293), ShowGoingLive (line 314), ShowThinkingIndicator (line 336). Element references stored: _loadingOverlay, _loadingText, _thinkingIndicator. Queried in OnEnable (lines 65-67). |
| `Assets/AyaLiveStream/UI/LivestreamPanel.uxml` | Loading overlay + thinking indicator | VERIFIED (78 lines) | loading-overlay with loading-text label (lines 13-15), thinking-indicator label in aya-header (line 55). |
| `Assets/AyaLiveStream/UI/LivestreamPanel.uss` | Loading/thinking CSS classes | VERIFIED (316 lines) | .loading-overlay (lines 276-286), .loading-overlay--hidden (288-290), .loading-text (292-295), .loading-text--live (297-301), .thinking-indicator (304-311), .thinking-indicator--visible (313-315). |
| `Assets/AyaLiveStream/Editor/CreateBeatAssets.cs` | Beat assets with catalyst content | VERIFIED (285 lines) | All 3 beats have catalystGoal, catalystMessages, topicKeywords. Beat 1: 3 catalysts, empty topicKeywords. Beat 2: 4 catalysts, 5 topicKeywords. Beat 3: 5 catalysts, 7 topicKeywords. |
| `Assets/AyaLiveStream/Editor/CreateLivestreamScene.cs` | One-click scene setup | VERIFIED (294 lines) | Swaps AyaSampleController for LivestreamController + all subsystems, wires all SerializeField references via SerializedObject/SerializedProperty, adds to Build Settings. |
| `Assets/AyaLiveStream/SceneTransitionHandler.cs` | Clean scene exit to movie | VERIFIED (67 lines) | Subscribes to OnAllBeatsComplete, calls Disconnect() then LoadSceneAsync with LoadSceneMode.Single. Build Settings validation. |
| `Assets/AyaLiveStream/PushToTalkController.cs` | Finish-first PTT state machine | VERIFIED (246 lines) | 5-state machine (Idle/WaitingForAya/Recording/Reviewing/Submitted), finish-first pattern, transcript review overlay, auto-submit timer, Enter/Escape handling. |
| `Assets/AyaLiveStream/AnimationConfig.cs` | Animation function call config | VERIFIED (42 lines) | AnimationEntry array, GetAnimationNames() for FunctionDeclaration.AddEnum. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| LivestreamController.Start() | ChatBotManager.SetContextProviders | Direct call (line 61) | WIRED | `_chatBotManager?.SetContextProviders(_ayaTranscriptBuffer, _factTracker)` |
| LivestreamController.Start() | NarrativeDirector.SetFactTracker | Direct call (line 62) | WIRED | `_narrativeDirector?.SetFactTracker(_factTracker)` |
| LivestreamController.Start() | ChatBotManager.SetCurrentBeat | OnBeatStarted subscription (line 65) | WIRED | `_onBeatStarted = beat => _chatBotManager?.SetCurrentBeat(beat)` on NarrativeDirector.OnBeatStarted |
| LivestreamController.Start() | PersonaSession events | Event subscriptions (lines 78-84) | WIRED | OnOutputTranscription, OnTurnComplete, OnAISpeakingStarted, OnFunctionError all subscribed with named handler refs |
| LivestreamController.Start() | NarrativeDirector.OnAllBeatsComplete | Event subscription (line 89) | WIRED | HandleNarrativeComplete stops bots and hides thinking indicator |
| LivestreamController.InitializeAndStart() | session.Connect + subsystem start | Async sequence (lines 162-219) | WIRED | Poll for Connected -> SetLoadingState(false) -> ShowGoingLive() -> 1.5s wait -> StartBursts() -> StartNarrative() |
| ChatBotManager.BuildDynamicPrompt | AyaTranscriptBuffer.GetRecentTurns | Direct call (line 519) | WIRED | `_ayaTranscriptBuffer.GetRecentTurns(3)` with null check, injected into prompt as "Aya (the host) has been saying:" |
| ChatBotManager.BuildDynamicPrompt | FactTracker.GetFactsSummary | Direct call (line 531) | WIRED | `_factTracker.GetFactsSummary()` with null check, injected as "Established facts (do NOT contradict these):" |
| ChatBotManager.PickMessage | NarrativeBeatConfig.catalystMessages | Random selection (line 312) | WIRED | `Random.value < 0.25f` -> pick random from `_currentBeat.catalystMessages`, bypasses per-bot tracking |
| NarrativeDirector.ExecuteBeatTransition | FactTracker.SetFact | Direct calls (lines 300-303) | WIRED | `SetFact($"beat_{beat.beatId}_started")` + conditional `SetFact("approaching_reveal")` for final beat |
| NarrativeDirector.RunBeatLoop | FactTracker.SetFact | Direct call (line 274) | WIRED | `SetFact($"beat_{beat.beatId}_completed")` after OnBeatEnded |
| NarrativeDirector.CheckSkipKeywords | topicKeywords | Iteration (lines 175-195) | WIRED | Iterates from `_currentBeatIndex + 1` to `_beats.Length - 1`, IndexOf case-insensitive, sets `_currentBeatIndex = i - 1` and `_beatGoalMet = true` |
| SceneTransitionHandler.OnEnable | NarrativeDirector.OnAllBeatsComplete | Event subscription (line 32) | WIRED | HandleAllBeatsComplete calls Disconnect() + LoadSceneAsync |
| LivestreamController.OnDestroy | All subscriptions | Unsubscription (lines 281-304) | WIRED | All 6 event handlers unsubscribed with null checks |

### Requirements Coverage

| Requirement | Status | Evidence |
|-------------|--------|----------|
| LIVE-01: Livestream sample scene with Aya as AI host, chat feed, and user push-to-talk | SATISFIED | LivestreamController orchestrates PersonaSession (AI host), LivestreamUI (chat feed via ListView), PushToTalkController (SPACE to talk). CreateLivestreamScene.cs provides one-click setup. |
| NAR-04: Chat bots as narrative catalysts (bot questions help steer Aya toward the goal) | SATISFIED | ChatBotManager.PickMessage() has 25% catalyst rate from NarrativeBeatConfig.catalystMessages. 3 beats with escalating catalyst content authored in CreateBeatAssets.cs. |
| NAR-05: User questions can accelerate the narrative arc | SATISFIED | NarrativeDirector.CheckSkipKeywords() checks topicKeywords on future beats, enabling jump to specific beats when user mentions relevant topics. Beat 2 has 5 topic keywords, Beat 3 has 7 topic keywords. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| NarrativeDirector.cs | 357 | "UserChoice not implemented in Phase 14 per scope" | Info | UserChoice scenes are skipped with Debug.Log. Not a blocker -- no UserChoice scenes are authored in the 3-beat demo. |
| NarrativeDirector.cs | 444-448 | "Phase 15 placeholder for action execution" | Info | AyaAction scenes only log. Not a blocker -- Phase 15 implemented animation function calls via a different mechanism (LivestreamController.RegisterFunctions with AnimationConfig), and no AyaAction scenes are authored in the 3-beat demo. |
| LivestreamController.cs | 126 | `return null; // fire-and-forget` | Info | HandlePlayAnimation returns null (no response needed for fire-and-forget function calls). This is the expected pattern for animation triggers. |

No blocker or warning-level anti-patterns found.

### Human Verification Required

### 1. Full Experience Loop in Unity Play Mode
**Test:** Open Unity, run "AI Embodiment > Samples > Create Demo Beat Assets" then "Create Livestream Scene". Open LivestreamSampleScene, enter Play Mode with a valid Gemini API key.
**Expected:** Loading overlay appears -> "GOING LIVE!" transition -> Aya greets the audience -> bots post messages (some are catalyst messages matching current beat) -> beats progress through warm-up/art process/characters -> movie scene loads.
**Why human:** Requires Unity Editor with Gemini API key, real-time observation of AI responses and timing over 10 minutes.

### 2. Push-to-Talk Narrative Acceleration
**Test:** During Play Mode, hold SPACE and mention "movie" or "film" or "reveal" while on Beat 1 or 2.
**Expected:** NarrativeDirector logs topic keyword match, narrative jumps to the characters beat or triggers the finale faster than waiting for time budget expiration.
**Why human:** Requires live speech input via microphone and observation of narrative state change.

### 3. Catalyst Message Frequency
**Test:** Observe the chat feed during Play Mode for several burst cycles (2-3 minutes).
**Expected:** Roughly 1 in 4 burst messages are catalyst messages (e.g., "ooh what are you drawing today??" during warm-up, "omg are you gonna show us the thing??" during characters beat).
**Why human:** 25% rate is statistical and needs human observation over time.

### 4. Cross-System Coherence of Bot Responses
**Test:** After using PTT, observe dynamic bot responses in the chat feed.
**Expected:** Bot responses reference what Aya has been saying (context from AyaTranscriptBuffer) and do not contradict established facts (constrained by FactTracker in prompt).
**Why human:** Requires evaluating Gemini-generated text for semantic coherence.

### Gaps Summary

No gaps found. All four must-have truths are verified through code-level structural analysis:

1. **Full experience loop:** LivestreamController provides the complete orchestration lifecycle from connection through "going live" to subsystem startup, dead air monitoring, and orderly shutdown triggering scene transition. The CreateLivestreamScene editor script provides one-click setup.

2. **Catalyst messages:** The 25% catalyst injection in PickMessage is a clean, non-intrusive mechanism. Beat assets have appropriate catalyst content with escalating urgency across the 3-beat arc.

3. **PTT narrative acceleration:** The two-tier skip system (topicKeywords for specific beats, skipKeywords for finale) enables granular narrative acceleration based on user speech.

4. **Cross-system coherence:** The three-layer system (AyaTranscriptBuffer for Aya-to-bot context, FactTracker for fact consistency, TrackedChatMessage for acknowledgment tracking) provides structural guarantees against incoherence. The prompt injection in BuildDynamicPrompt explicitly tells Gemini what Aya has said and what facts are established.

All automated checks pass. The remaining verification items require human testing in Unity Play Mode with a live Gemini API connection.

---

_Verified: 2026-02-17T22:30:00Z_
_Verifier: Claude (gsd-verifier)_
