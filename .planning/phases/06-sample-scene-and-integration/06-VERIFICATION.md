---
phase: 06-sample-scene-and-integration
verified: 2026-02-05T17:30:00Z
status: gaps_found
score: 5/7 must-haves verified
gaps:
  - truth: "AyaSampleController has references to PersonaSession, AyaChatUI, intro AudioSource"
    status: failed
    reason: "Scene YAML shows _chatUI and _introAudioSource are null (fileID: 0) on the AyaSampleController component"
    artifacts:
      - path: "Assets/Scenes/AyaSampleScene.unity"
        issue: "AyaSampleController._chatUI = {fileID: 0} (null), _introAudioSource = {fileID: 0} (null)"
    missing:
      - "AyaChatUI MonoBehaviour component must be added to the UIDocument GameObject in the scene"
      - "AyaSampleController._chatUI field must reference the AyaChatUI component"
      - "AyaSampleController._introAudioSource field must reference the second AudioSource on AyaSession"
  - truth: "AyaChatUI has references to UIDocument and PersonaSession"
    status: failed
    reason: "AyaChatUI MonoBehaviour does not exist as a component in the scene at all"
    artifacts:
      - path: "Assets/Scenes/AyaSampleScene.unity"
        issue: "UIDocument GameObject has only Transform + UIDocument components; no AyaChatUI MonoBehaviour attached"
    missing:
      - "Add AyaChatUI MonoBehaviour to UIDocument GameObject"
      - "Wire AyaChatUI._uiDocument to the UIDocument component on the same GameObject"
      - "Wire AyaChatUI._session to AyaSession's PersonaSession component"
---

# Phase 6: Sample Scene and Integration Verification Report

**Phase Goal:** Developer can install the package and run a sample scene that demonstrates the full pipeline -- persona talking with synchronized voice, text, and animation function calls -- in under 5 minutes
**Verified:** 2026-02-05T17:30:00Z
**Status:** gaps_found
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Package Manager shows "Aya Live Stream" sample with Import button | VERIFIED | package.json has samples array with displayName "Aya Live Stream" pointing to Samples~/AyaLiveStream |
| 2 | UI layout defines chat log, persona name header, speaking indicator, status label, and push-to-talk button | VERIFIED | AyaPanel.uxml has all named elements: root-container, header, speaking-indicator, persona-name, chat-log (ScrollView), footer, status-label, ptt-button |
| 3 | Sample scripts can reference AIEmbodiment, Firebase.AI, and Input System types without compile errors | VERIFIED | AyaLiveStream.asmdef references com.google.ai-embodiment, Firebase.AI, Unity.InputSystem; human-verified no compile errors |
| 4 | Three function calls (emote, start_movie, start_drawing) are registered before Connect with correct schemas | VERIFIED | AyaSampleController.RegisterFunctions() called from Start(), Connect() called from coroutine after intro; emote uses Schema.Enum with 17 animations |
| 5 | Conversational goal is injected after 3 exchange turns, not at session start | VERIFIED | HandleTurnComplete increments _exchangeCount, calls _session.AddGoal at count==3 with _goalActivated guard |
| 6 | AyaSampleController has references to PersonaSession, AyaChatUI, intro AudioSource | FAILED | Scene YAML: _session wired correctly, but _chatUI={fileID:0} (null) and _introAudioSource={fileID:0} (null) |
| 7 | AyaChatUI has references to UIDocument and PersonaSession | FAILED | AyaChatUI MonoBehaviour is not present as a component anywhere in AyaSampleScene.unity |

**Score:** 5/7 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Packages/com.google.ai-embodiment/package.json` | UPM samples array | VERIFIED | Valid JSON, samples array with "Aya Live Stream" entry, path "Samples~/AyaLiveStream" |
| `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaLiveStream.asmdef` | Assembly definition with 3 refs | VERIFIED | 18 lines, valid JSON, references com.google.ai-embodiment, Firebase.AI, Unity.InputSystem |
| `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/UI/AyaPanel.uxml` | UI Toolkit layout | VERIFIED | 17 lines, well-formed XML, all 6 named elements present |
| `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/UI/AyaPanel.uss` | Dark-themed styling | VERIFIED | 87 lines, 14 CSS selectors including .indicator--speaking, .msg-aya, .msg-user, .msg-system, .ptt-button states |
| `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaChatUI.cs` | UI Toolkit chat log controller | VERIFIED | 177 lines, substantive, queries all UXML elements by name, subscribes to all PersonaSession events, handles streaming transcription, speaking glow, PTT button, auto-scroll |
| `Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/AyaSampleController.cs` | Main controller | VERIFIED | 155 lines, substantive, 3 function registrations, intro coroutine, Keyboard.current PTT, goal injection at 3 exchanges |
| `Assets/AyaLiveStream/AyaPersonaConfig.asset` | PersonaConfig ScriptableObject | VERIFIED | Valid Unity YAML, displayName="Aya", archetype="The Bubbly Digital Artist", all personality data populated, voiceBackend=0 (GeminiNative), geminiVoiceName="Puck" |
| `Assets/Scenes/AyaSampleScene.unity` | Sample scene with components wired | PARTIAL | Scene exists (719 lines), has AyaSession (PersonaSession+AudioCapture+AudioPlayback+AudioSource), UIDocument, AyaSampleController -- but AyaChatUI component missing, two null references on AyaSampleController |
| `Assets/AyaLiveStream/` copies (asmdef, scripts, UI) | Copies for Editor testing | VERIFIED | All 6 files duplicated from Samples~ with .meta files |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| package.json | Samples~/AyaLiveStream | samples array path field | WIRED | "path": "Samples~/AyaLiveStream" present |
| AyaLiveStream.asmdef | com.google.ai-embodiment runtime | asmdef references array | WIRED | "com.google.ai-embodiment" in references |
| AyaChatUI.cs | AyaPanel.uxml | Q<> element queries by name | WIRED | Q<ScrollView>("chat-log"), Q<Label>("persona-name"), Q<Label>("status-label"), Q<Button>("ptt-button"), Q("speaking-indicator") all match UXML names |
| AyaChatUI.cs | PersonaSession events | event subscriptions in OnEnable | WIRED | OnOutputTranscription, OnInputTranscription, OnAISpeakingStarted, OnAISpeakingStopped, OnUserSpeakingStarted, OnUserSpeakingStopped, OnStateChanged, OnTurnComplete -- all exist on PersonaSession |
| AyaSampleController.cs | PersonaSession.RegisterFunction | function registration in Start() | WIRED | RegisterFunctions() called in Start(), 3 calls to _session.RegisterFunction with correct FunctionHandler delegate signature (returns IDictionary<string,object>) |
| AyaSampleController.cs | PersonaSession.AddGoal | goal injection after exchanges | WIRED | _session.AddGoal("life_story", ..., GoalPriority.Medium) with correct API signature match |
| AyaSampleController.cs | Keyboard.current.spaceKey | PTT polling in Update | WIRED | Keyboard.current null check, spaceKey.wasPressedThisFrame/wasReleasedThisFrame mapped to StartListening/StopListening |
| AyaChatUI.cs | PTT button | PointerDown/PointerUp callbacks | WIRED | _pttButton.RegisterCallback<PointerDownEvent> and <PointerUpEvent> calling StartListening/StopListening |
| Scene AyaSampleController | AyaChatUI | _chatUI SerializeField | NOT_WIRED | _chatUI = {fileID: 0} (null reference in scene) |
| Scene AyaSampleController | intro AudioSource | _introAudioSource SerializeField | NOT_WIRED | _introAudioSource = {fileID: 0} (null reference in scene) |
| Scene UIDocument | AyaChatUI component | MonoBehaviour attachment | NOT_WIRED | AyaChatUI component does not exist on the UIDocument GameObject |
| Scene PersonaSession | AyaPersonaConfig | _config SerializeField | WIRED | _config = {fileID: 11400000, guid: 35b572ed...} (references asset) |
| Scene PersonaSession | AudioCapture | _audioCapture SerializeField | WIRED | _audioCapture = {fileID: 406905699} (references component) |
| Scene PersonaSession | AudioPlayback | _audioPlayback SerializeField | WIRED | _audioPlayback = {fileID: 406905698} (references component) |
| Scene UIDocument | PanelSettings | m_PanelSettings | WIRED | {fileID: 11400000, guid: 8e4c288a...} (references asset) |
| Scene UIDocument | AyaPanel.uxml | sourceAsset | WIRED | {fileID: 9197481963319205126, guid: 330bca2a...} (references UXML asset) |

### Requirements Coverage

| Requirement | Status | Blocking Issue |
|-------------|--------|----------------|
| PKG-02: Sample scene demonstrates full pipeline -- persona talking with animation function calls | BLOCKED | Scene has incomplete wiring: AyaChatUI component missing, AyaSampleController._chatUI and _introAudioSource null. Without AyaChatUI, no chat log display, no speaking indicator, no status updates. |
| FUNC-04: Built-in emote function with animation name enum as reference implementation | SATISFIED | AyaSampleController registers "emote" function with Schema.Enum of 17 animation names and HandleEmote handler |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | - | - | - | No TODO, FIXME, placeholder, or stub patterns found in any sample code files |

### Human Verification Required

The user has already performed human verification as part of Plan 06-03 checkpoint. The user confirmed:
- Scene loads without compile errors
- Dark chat panel renders correctly (header, chat log, PTT button)
- Status label updates through intro -> connecting sequence
- No NullReferenceExceptions
- WebSocket error expected (Firebase credentials not configured)
- All component references wired in Inspector

**NOTE:** The human verification results CONTRADICT the scene YAML on disk. The scene file shows `_chatUI: {fileID: 0}` and no AyaChatUI component. For the status label to update and the chat panel to render as described, AyaChatUI must have been wired. This suggests one of:

1. The user wired the references in Unity Editor but the scene file on disk was not saved/updated after that point
2. The scene on disk is a different version than what was tested

**For reproducibility by another developer, the scene file on disk is what matters.** The current scene YAML would produce NullReferenceExceptions because AyaSampleController._chatUI is null and AyaSampleController calls `_chatUI.SetStatus()` and `_chatUI.LogSystemMessage()` in its intro coroutine.

### 1. Full Conversation Flow (With Firebase)

**Test:** Configure google-services.json with valid Firebase project, open AyaSampleScene, enter Play mode, hold spacebar, speak, release
**Expected:** User transcription appears in blue, Aya responds with purple text and synthesized voice, emote function calls appear as gray italic system messages, after 3 exchanges "[Goal activated: Steer toward character life stories]" appears
**Why human:** Requires live Firebase credentials, real-time audio I/O, and Gemini Live API connectivity

### 2. Scene Wiring Re-Verification

**Test:** Open AyaSampleScene in Unity Editor, select AyaSampleController GameObject, check Inspector fields
**Expected:** _session references PersonaSession, _chatUI references AyaChatUI, _introAudioSource references the second AudioSource
**Why human:** Need to confirm whether Editor state differs from scene YAML on disk; if mismatched, re-save the scene

## Gaps Summary

All six code/asset files (package.json, asmdef, UXML, USS, AyaChatUI.cs, AyaSampleController.cs) are substantive, well-implemented, and correctly wired at the code level. The API surface they consume (PersonaSession events, RegisterFunction, AddGoal, StartListening/StopListening) is verified to exist with matching signatures.

The sole gap is in the **Unity scene file on disk** (`Assets/Scenes/AyaSampleScene.unity`):
1. **AyaChatUI MonoBehaviour** is not attached to any GameObject in the scene
2. **AyaSampleController._chatUI** is null (`{fileID: 0}`)
3. **AyaSampleController._introAudioSource** is null (`{fileID: 0}`)

These are Editor-side wiring issues that the user may have already fixed in their Unity Editor session but not saved to disk. The fix requires:
- Adding AyaChatUI component to the UIDocument GameObject
- Wiring AyaChatUI._uiDocument and AyaChatUI._session
- Wiring AyaSampleController._chatUI and _introAudioSource
- Saving the scene (Ctrl+S)

Without these fixes, a fresh developer opening the scene from the repository would encounter NullReferenceExceptions on first Play.

---

_Verified: 2026-02-05T17:30:00Z_
_Verifier: Claude (gsd-verifier)_
