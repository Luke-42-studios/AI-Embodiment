# Phase 6: Sample Scene and Integration - Research

**Researched:** 2026-02-05
**Domain:** Unity UPM sample scene, UI Toolkit runtime UI, Firebase AI function calling, Input System
**Confidence:** HIGH

## Summary

Phase 6 builds a UPM sample scene that demonstrates the full AI Embodiment pipeline end-to-end. The sample lives in the `Samples~/` folder of the package and gets imported by developers via Package Manager. It features Aya, a bubbly digital artist persona, with three function calls (emote, start_movie, start_drawing), a scrolling chat log UI, push-to-talk via spacebar and on-screen button, a pre-recorded audio intro, and a runtime-injected conversational goal.

The research confirms: the existing `PersonaSession` API surface already supports everything needed. The sample is a consumer of the library, not an extension of it. The main technical domains are (1) UPM Samples~ folder convention, (2) UI Toolkit for the runtime chat panel, (3) Input System for push-to-talk, and (4) function declaration wiring using the Firebase Schema/FunctionDeclaration API.

**Primary recommendation:** Build the sample as a single MonoBehaviour controller (`AyaSampleController`) that wires up PersonaSession events to a UI Toolkit chat log, handles push-to-talk via Input System keyboard polling, and registers three function declarations before Connect. Use UI Toolkit (not uGUI) since the project has `com.unity.modules.uielements` available and UI Toolkit is Unity 6's recommended runtime UI approach.

## Standard Stack

The sample scene is a consumer of the existing AI Embodiment package. No new external libraries needed.

### Core (Already Available in Project)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| AI Embodiment Runtime | 0.1.0 | PersonaSession, AudioCapture, AudioPlayback, etc. | The package we are demonstrating |
| Firebase.AI | (bundled) | FunctionDeclaration, Schema, Tool | Already used by PersonaSession for live session |
| Input System | 1.18.0 | Keyboard.current.spaceKey for push-to-talk | Already in project manifest, activeInputHandler=1 (new only) |
| UI Toolkit | (com.unity.modules.uielements) | Runtime UI: UIDocument, UXML, USS | Unity 6 recommended runtime UI, already available |

### Supporting (No Additional Installs)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| com.unity.ugui | 2.0.0 | Legacy UI (fallback) | NOT recommended -- use UI Toolkit instead |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| UI Toolkit | uGUI (Canvas + Text) | uGUI is legacy; UI Toolkit is recommended for Unity 6 runtime |
| Input System direct polling | Input Action asset | Action asset adds complexity; direct `Keyboard.current` polling is simpler for a single key |

**Installation:** No additional packages needed. Everything is already in the project.

## Architecture Patterns

### Recommended Sample Folder Structure

```
Samples~/
  AyaLiveStream/
    AyaLiveStream.unity          # The scene file
    AyaLiveStream.asmdef         # Assembly def referencing package runtime + Firebase.AI
    AyaPersonaConfig.asset       # Pre-configured PersonaConfig ScriptableObject
    AyaSampleController.cs       # Main controller MonoBehaviour
    AyaChatUI.cs                 # UI Toolkit chat log controller
    AyaIntro.wav                 # Pre-recorded intro audio clip (~5-10 seconds)
    UI/
      AyaPanel.uxml             # UI layout: chat log, name label, push-to-talk button
      AyaPanel.uss              # Styling: dark background, glow effects, colors
    PanelSettings.asset          # UI Toolkit panel settings for runtime rendering
```

### Pattern 1: Single Controller Wiring Pattern

**What:** One MonoBehaviour (`AyaSampleController`) owns all lifecycle: registers functions, subscribes to events, manages intro/live transition, injects goals.
**When to use:** Sample scenes where simplicity matters more than reusability.
**Example:**

```csharp
// AyaSampleController.cs -- high-level structure
public class AyaSampleController : MonoBehaviour
{
    [SerializeField] private PersonaSession _session;
    [SerializeField] private AudioSource _introAudioSource;  // Separate from AI voice
    [SerializeField] private AudioClip _introClip;
    [SerializeField] private AyaChatUI _chatUI;

    private bool _liveMode = false;
    private int _exchangeCount = 0;

    void Start()
    {
        // Register function declarations BEFORE Connect
        RegisterFunctions();

        // Play pre-recorded intro first
        StartCoroutine(PlayIntroThenGoLive());
    }

    void RegisterFunctions()
    {
        // emote(animation_name) -- enum parameter
        _session.RegisterFunction("emote",
            new FunctionDeclaration("emote",
                "Express an emotion or action visually. Call this to animate yourself.",
                new Dictionary<string, Schema> {
                    { "animation_name", Schema.Enum(
                        new[] { "idle", "wave", "think", "talk", "laugh", "shrug",
                                "fidgets", "nods_emphatically", "leans_forward",
                                "takes_deep_breath", "groans", "holds_up_hands",
                                "covers_face", "rolls_eyes", "stretches", "beams",
                                "puts_hand_over_heart" },
                        "The animation to play") }
                }),
            HandleEmote);

        // start_movie() -- no parameters
        _session.RegisterFunction("start_movie",
            new FunctionDeclaration("start_movie",
                "Cut away to show the movie scene. Use when telling a story that should be shown.",
                new Dictionary<string, Schema>()),
            HandleStartMovie);

        // start_drawing() -- no parameters
        _session.RegisterFunction("start_drawing",
            new FunctionDeclaration("start_drawing",
                "Return to drawing on stream. Use when going back to creating art.",
                new Dictionary<string, Schema>()),
            HandleStartDrawing);
    }

    IDictionary<string, object> HandleEmote(FunctionCallContext ctx)
    {
        string animName = ctx.GetString("animation_name", "idle");
        _chatUI.LogSystemMessage($"[Emote: {animName}]");
        return null; // fire-and-forget
    }

    IDictionary<string, object> HandleStartMovie(FunctionCallContext ctx)
    {
        _chatUI.LogSystemMessage("[Movie mode started]");
        return null;
    }

    IDictionary<string, object> HandleStartDrawing(FunctionCallContext ctx)
    {
        _chatUI.LogSystemMessage("[Drawing mode started]");
        return null;
    }

    IEnumerator PlayIntroThenGoLive()
    {
        _introAudioSource.clip = _introClip;
        _introAudioSource.Play();
        _chatUI.LogSystemMessage("Aya's intro playing...");

        yield return new WaitWhile(() => _introAudioSource.isPlaying);

        // Transition to live mode
        _liveMode = true;
        _session.Connect();
        _chatUI.LogSystemMessage("Live session started! Hold SPACE to talk.");
    }

    void Update()
    {
        if (!_liveMode) return;

        // Push-to-talk: spacebar
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                _session.StartListening();
            if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                _session.StopListening();
        }
    }
}
```

### Pattern 2: UI Toolkit Chat Log Controller

**What:** Separate MonoBehaviour managing the UIDocument, querying elements by name, appending messages to a ScrollView.
**When to use:** When UI logic is complex enough to warrant separation from game logic.
**Example:**

```csharp
// AyaChatUI.cs
public class AyaChatUI : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private PersonaSession _session;

    private ScrollView _chatLog;
    private Label _nameLabel;
    private Button _pttButton;
    private VisualElement _speakingIndicator;

    void OnEnable()
    {
        var root = _uiDocument.rootVisualElement;
        _chatLog = root.Q<ScrollView>("chat-log");
        _nameLabel = root.Q<Label>("persona-name");
        _pttButton = root.Q<Button>("ptt-button");
        _speakingIndicator = root.Q("speaking-indicator");

        _nameLabel.text = _session.Config.displayName;

        // Subscribe to PersonaSession events
        _session.OnOutputTranscription += HandleAIText;
        _session.OnInputTranscription += HandleUserText;
        _session.OnAISpeakingStarted += () => SetSpeakingGlow(true);
        _session.OnAISpeakingStopped += () => SetSpeakingGlow(false);
        _session.OnStateChanged += HandleStateChange;

        // Push-to-talk button
        _pttButton.RegisterCallback<PointerDownEvent>(e => _session.StartListening());
        _pttButton.RegisterCallback<PointerUpEvent>(e => _session.StopListening());
    }

    public void AppendMessage(string sender, string text, string cssClass)
    {
        var msg = new Label($"{sender}: {text}");
        msg.AddToClassList(cssClass);
        _chatLog.Add(msg);

        // Auto-scroll to bottom
        _chatLog.schedule.Execute(() =>
        {
            _chatLog.scrollOffset = new Vector2(0, _chatLog.contentContainer.layout.height);
        });
    }
}
```

### Pattern 3: Pre-recorded Intro with Separate AudioSource

**What:** Use a dedicated AudioSource for the intro clip, separate from AudioPlayback's AudioSource (which uses OnAudioFilterRead for streaming).
**When to use:** Always, when mixing pre-recorded clips with streaming AI audio.
**Why:** AudioPlayback's OnAudioFilterRead replaces the AudioSource's audio data entirely. Pre-recorded clips must play on a separate AudioSource to avoid conflict.

### Pattern 4: Runtime Goal Injection After Warm-up

**What:** Count exchange turns or wait for elapsed time, then call `_session.AddGoal()` at runtime.
**When to use:** Demonstrating dynamic conversational goal injection.
**Example:**

```csharp
// Inside AyaSampleController, subscribe to OnTurnComplete
void OnEnable()
{
    _session.OnTurnComplete += HandleTurnComplete;
}

void HandleTurnComplete()
{
    _exchangeCount++;
    if (_exchangeCount == 3) // After 3 exchanges
    {
        _session.AddGoal(
            "life_story",
            "Steer the conversation toward talking about the life story behind your characters and what inspired you to start drawing them",
            GoalPriority.Medium
        );
        _chatUI.LogSystemMessage("[Goal activated: Steer toward character life stories]");
    }
}
```

### Anti-Patterns to Avoid

- **Putting sample code in Runtime/:** Sample scripts must live in Samples~/, not in the package runtime. They are consumer code, not library code.
- **Using AudioPlayback for the intro clip:** AudioPlayback uses OnAudioFilterRead with a ring buffer; playing a pre-recorded AudioClip through it would require manually feeding PCM data. Use a separate plain AudioSource instead.
- **Registering functions after Connect():** FunctionRegistry freezes on BuildTools() which is called inside Connect(). All RegisterFunction calls must happen before Connect().
- **Polling Input in the sample asmdef without referencing Input System:** The sample asmdef must reference `Unity.InputSystem` to use `Keyboard.current`.
- **Hardcoding Firebase config in sample code:** The sample should work with whatever Firebase project the developer has configured. Do not include google-services.json in the sample.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Chat log UI | Custom IMGUI or Canvas | UI Toolkit ScrollView + Label elements | UI Toolkit handles scrolling, layout, styling natively |
| Push-to-talk input | Legacy Input.GetKeyDown | `Keyboard.current.spaceKey.wasPressedThisFrame` | Project uses new Input System exclusively (activeInputHandler=1) |
| Function schema definition | Custom JSON | `FunctionDeclaration` + `Schema.Enum()` | Firebase SDK provides the exact API for this |
| Pre-recorded audio playback | Custom PCM feeding | AudioSource.Play() with AudioClip | Standard Unity AudioSource handles this perfectly |
| ScrollView auto-scroll | Manual offset tracking | `scrollView.scrollOffset = new Vector2(0, contentHeight)` | Built-in UI Toolkit ScrollView property |
| PersonaConfig creation | Manual field assignment | ScriptableObject asset in Samples~/ folder | Pre-configured asset serialized at authoring time |

**Key insight:** The sample is pure consumer code. Every feature it demonstrates already exists in the package API. The sample's job is wiring, not implementing.

## Common Pitfalls

### Pitfall 1: Sample asmdef Missing References

**What goes wrong:** Sample scripts cannot find AIEmbodiment namespace or Firebase.AI types.
**Why it happens:** Samples~ folder is outside the package Runtime asmdef scope. Scripts in Samples~ need their own asmdef.
**How to avoid:** Create `AyaLiveStream.asmdef` in the sample folder with references to `com.google.ai-embodiment` and `Firebase.AI` and `Unity.InputSystem`.
**Warning signs:** "The type or namespace 'AIEmbodiment' could not be found" compile errors after importing the sample.

### Pitfall 2: Input System Not Referenced in Sample asmdef

**What goes wrong:** `Keyboard.current` is undefined; `UnityEngine.InputSystem` namespace not found.
**Why it happens:** The Input System package provides its types through the `Unity.InputSystem` assembly, which must be explicitly referenced.
**How to avoid:** Add `"Unity.InputSystem"` to the sample asmdef references array.
**Warning signs:** Compile error on `using UnityEngine.InputSystem;`.

### Pitfall 3: Pre-recorded Intro Conflicts with AudioPlayback

**What goes wrong:** Pre-recorded intro audio doesn't play, or plays garbled, or interferes with AI voice.
**Why it happens:** AudioPlayback creates a silent dummy clip and uses OnAudioFilterRead to replace audio data. If the intro plays on the same AudioSource, OnAudioFilterRead will overwrite it.
**How to avoid:** Use a completely separate AudioSource GameObject for the intro clip. The AudioPlayback AudioSource is exclusively for AI streaming voice.
**Warning signs:** Intro audio is silent or sounds like static.

### Pitfall 4: Functions Registered After Connect

**What goes wrong:** Function tools are not sent to Gemini; AI never calls any functions.
**Why it happens:** `PersonaSession.Connect()` calls `_functionRegistry.BuildTools()` which freezes the registry. Any `RegisterFunction()` calls after this throw `InvalidOperationException`.
**How to avoid:** Always register all functions in `Start()` or `Awake()`, before calling `Connect()`.
**Warning signs:** Functions appear to be registered but AI never calls them; or InvalidOperationException in console.

### Pitfall 5: ScrollView Not Auto-Scrolling

**What goes wrong:** New messages appear below the visible area; user must manually scroll down.
**Why it happens:** Adding elements to ScrollView doesn't automatically scroll to the bottom. The layout needs one frame to compute before scrollOffset can be set correctly.
**How to avoid:** Use `_chatLog.schedule.Execute()` to defer the scroll offset update by one frame after adding content.
**Warning signs:** Messages accumulate but visible area stays at the top.

### Pitfall 6: UPM Sample Path Mismatch

**What goes wrong:** Package Manager doesn't show the Import button for samples.
**Why it happens:** The `path` field in package.json doesn't match the actual folder name on disk.
**How to avoid:** The folder on disk is `Samples~/AyaLiveStream`. The package.json `samples[].path` should be `"Samples~/AyaLiveStream"`. Since the project already has a `Samples~` folder (with tilde), use the tilde in the path field. The Unity 6 package manifest reference (6000.3 docs) confirms `Samples~/` path format.
**Warning signs:** No "Samples" tab visible in Package Manager details.

### Pitfall 7: Missing PanelSettings Asset for UI Toolkit

**What goes wrong:** UIDocument shows nothing at runtime; blank screen.
**Why it happens:** UIDocument needs a PanelSettings asset assigned (or one must exist in the project for auto-discovery). Samples import into Assets/ and may not find the panel settings.
**How to avoid:** Include a `PanelSettings.asset` in the sample folder and assign it to the UIDocument in the scene. This way it gets imported alongside the scene.
**Warning signs:** Console warning "UIDocument has no PanelSettings" and no UI visible.

### Pitfall 8: Keyboard.current is Null

**What goes wrong:** NullReferenceException when checking spacebar.
**Why it happens:** `Keyboard.current` can be null if no keyboard is connected or on mobile platforms.
**How to avoid:** Always null-check: `if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)`.
**Warning signs:** NullReferenceException in Update() on platforms without keyboard.

### Pitfall 9: PersonaConfig Asset Not Linked After Import

**What goes wrong:** PersonaSession's `_config` field is null; Connect() logs "No PersonaConfig assigned."
**Why it happens:** ScriptableObject references in scene files use GUIDs. When the sample is imported, the GUID of the PersonaConfig asset must match what the scene expects.
**How to avoid:** Ensure the PersonaConfig asset is in the same sample folder and referenced by the scene. Unity preserves internal references when the entire sample folder is imported together.
**Warning signs:** "No PersonaConfig assigned" error on Connect.

## Code Examples

### Function Declaration with Schema.Enum (Emote Function)

```csharp
// Source: Firebase.AI.Schema class + FunctionDeclaration constructor
// Verified from Assets/Firebase/FirebaseAI/Schema.cs and FunctionCalling.cs

var emoteDeclaration = new FunctionDeclaration(
    "emote",
    "Express an emotion or action visually. Call this to animate yourself.",
    new Dictionary<string, Schema>
    {
        {
            "animation_name",
            Schema.Enum(
                new[] {
                    "idle", "wave", "think", "talk", "laugh", "shrug",
                    "fidgets", "nods_emphatically", "leans_forward",
                    "takes_deep_breath", "groans", "holds_up_hands",
                    "covers_face", "rolls_eyes", "stretches", "beams",
                    "puts_hand_over_heart"
                },
                "The animation to play"
            )
        }
    }
);
```

### No-Parameter Function Declaration

```csharp
// start_movie and start_drawing have no parameters
// FunctionDeclaration requires IDictionary<string, Schema> -- pass empty dictionary

var startMovieDeclaration = new FunctionDeclaration(
    "start_movie",
    "Cut away to show the movie scene. Use when telling a story.",
    new Dictionary<string, Schema>()
);
```

### Push-to-Talk with Input System (New)

```csharp
// Source: Unity Input System 1.18.0 -- Keyboard.current direct polling
// Project uses activeInputHandler=1 (new Input System only)
using UnityEngine.InputSystem;

void Update()
{
    if (Keyboard.current != null)
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            _session.StartListening();
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
            _session.StopListening();
    }
}
```

### UI Toolkit ScrollView Auto-Scroll

```csharp
// Source: Unity UI Toolkit docs -- ScrollView element
// Deferred scroll ensures layout is computed before setting offset

public void AppendMessage(string text, string cssClass)
{
    var label = new Label(text);
    label.AddToClassList(cssClass);
    _chatLog.Add(label);

    // Defer scroll to next frame so layout is recalculated
    _chatLog.schedule.Execute(() =>
    {
        _chatLog.scrollOffset = new Vector2(
            0, _chatLog.contentContainer.layout.height);
    });
}
```

### Sample asmdef with Required References

```json
{
    "name": "com.google.ai-embodiment.samples.ayalivestream",
    "rootNamespace": "AIEmbodiment.Samples",
    "references": [
        "com.google.ai-embodiment",
        "Firebase.AI",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### package.json Samples Array Entry

```json
{
    "samples": [
        {
            "displayName": "Aya Live Stream",
            "description": "Full pipeline demo: AI persona with voice, chat log, function calls, and conversational goals.",
            "path": "Samples~/AyaLiveStream"
        }
    ]
}
```

### Pre-recorded Intro Coroutine

```csharp
// Use a separate AudioSource from the AI voice AudioSource
IEnumerator PlayIntroThenGoLive()
{
    _introAudioSource.clip = _introClip;
    _introAudioSource.Play();
    _chatUI.SetStatus("Aya's intro playing...");

    yield return new WaitWhile(() => _introAudioSource.isPlaying);

    // Now start live session
    _session.Connect();
    _chatUI.SetStatus("Live! Hold SPACE to talk.");
}
```

### UXML Structure for Chat Panel

```xml
<!-- AyaPanel.uxml -->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="root-container" class="root">
        <!-- Header: persona name + speaking indicator -->
        <ui:VisualElement name="header" class="header">
            <ui:VisualElement name="speaking-indicator" class="indicator" />
            <ui:Label name="persona-name" text="Aya" class="name-label" />
        </ui:VisualElement>

        <!-- Chat log: scrolling message area -->
        <ui:ScrollView name="chat-log" class="chat-log"
            vertical-scroller-visibility="Auto"
            horizontal-scroller-visibility="Hidden" />

        <!-- Status bar + push-to-talk -->
        <ui:VisualElement name="footer" class="footer">
            <ui:Label name="status-label" text="Connecting..." class="status" />
            <ui:Button name="ptt-button" text="Hold to Talk" class="ptt-button" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

### USS Styling (Dark Theme, Speaking Glow)

```css
/* AyaPanel.uss */
.root {
    flex-grow: 1;
    background-color: rgb(24, 24, 32);
    padding: 12px;
}

.header {
    flex-direction: row;
    align-items: center;
    margin-bottom: 8px;
}

.indicator {
    width: 12px;
    height: 12px;
    border-radius: 6px;
    background-color: rgb(80, 80, 80);
    margin-right: 8px;
}

.indicator--speaking {
    background-color: rgb(100, 255, 100);
    /* USS does not support box-shadow; use border-color for glow effect */
    border-width: 2px;
    border-color: rgb(100, 255, 100);
}

.name-label {
    font-size: 20px;
    color: rgb(220, 180, 255);
    -unity-font-style: bold;
}

.chat-log {
    flex-grow: 1;
    background-color: rgb(16, 16, 24);
    border-radius: 8px;
    padding: 8px;
    margin-bottom: 8px;
}

.msg-aya {
    color: rgb(200, 170, 255);
    margin-bottom: 4px;
    white-space: normal;
}

.msg-user {
    color: rgb(170, 220, 255);
    margin-bottom: 4px;
    white-space: normal;
}

.msg-system {
    color: rgb(120, 120, 140);
    margin-bottom: 4px;
    -unity-font-style: italic;
    white-space: normal;
}

.footer {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
}

.status {
    color: rgb(140, 140, 160);
    font-size: 12px;
}

.ptt-button {
    background-color: rgb(80, 60, 120);
    color: rgb(255, 255, 255);
    border-radius: 6px;
    padding: 8px 16px;
    font-size: 14px;
    -unity-font-style: bold;
}

.ptt-button:hover {
    background-color: rgb(100, 80, 140);
}

.ptt-button:active {
    background-color: rgb(120, 100, 160);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| uGUI Canvas + Text for game UI | UI Toolkit (UXML + USS + UIDocument) | Unity 2021+ (production ready Unity 6) | Samples should use modern approach |
| Legacy Input Manager (Input.GetKeyDown) | Input System package (Keyboard.current) | Project configured activeInputHandler=1 | MUST use new Input System -- legacy API unavailable |
| Samples/ path in package.json | Samples~/ path in package.json | Unity 6 docs show tilde path | Use `Samples~/AyaLiveStream` in path field |

**Deprecated/outdated:**
- `Input.GetKeyDown(KeyCode.Space)`: Will not compile -- project is set to new Input System only.
- `Canvas` + `Text` for simple UI: Still works but UI Toolkit is recommended for new projects in Unity 6.

## Open Questions

Things that could not be fully resolved:

1. **Pre-recorded intro audio asset**
   - What we know: The sample needs a .wav file as the intro clip. This must be authored/recorded separately.
   - What's unclear: Whether a real recording will be available, or if a placeholder silent/short clip should be used.
   - Recommendation: Create a short placeholder .wav (2-3 seconds of silence or a tone) that can be replaced later with real Aya intro audio. The code structure is the same regardless.

2. **UPM Samples path: Samples/ vs Samples~/ in package.json**
   - What we know: Two Unity doc pages give conflicting guidance. The "create samples" page says use `Samples/` (without tilde, Unity renames during export). The package manifest reference page (6000.3) shows `Samples~/` in the path.
   - What's unclear: Which is correct for an embedded development package that already has `Samples~/` on disk.
   - Recommendation: Use `Samples~/AyaLiveStream` in package.json since the folder on disk is already named `Samples~/`. This matches the Unity 6 package manifest reference. If the Import button doesn't appear, try without tilde as fallback.

3. **FUNC-04 (built-in emote function) still pending from Phase 4**
   - What we know: FUNC-04 was marked pending -- "Built-in emote function with animation name enum as a reference implementation." The sample is the natural place to fulfill this requirement.
   - What's unclear: Whether FUNC-04 should be a reusable helper in Runtime/ or sample-only code.
   - Recommendation: Fulfill FUNC-04 as sample code in AyaSampleController. The emote function declaration with Schema.Enum is the reference implementation. If a reusable helper is desired later, it can be extracted to Runtime/ in a future phase.

4. **Exact warm-up exchange count before goal activation**
   - What we know: CONTEXT.md says goal activates "after warm-up, not from the start."
   - What's unclear: Exact number of exchanges.
   - Recommendation: Use 3 exchanges (OnTurnComplete count) as the trigger. This is in Claude's discretion per CONTEXT.md.

## Sources

### Primary (HIGH confidence)
- `Assets/Firebase/FirebaseAI/FunctionCalling.cs` -- FunctionDeclaration constructor signature verified: `(string name, string description, IDictionary<string, Schema> parameters, IEnumerable<string> optionalParameters = null)`
- `Assets/Firebase/FirebaseAI/Schema.cs` -- Schema.Enum() API verified: `Schema.Enum(IEnumerable<string> values, string description, bool nullable)`
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` -- Full API surface verified: RegisterFunction, Connect, StartListening, StopListening, AddGoal, event signatures
- `Packages/com.google.ai-embodiment/Runtime/FunctionRegistry.cs` -- Freeze-on-BuildTools behavior verified
- `Packages/com.google.ai-embodiment/Runtime/FunctionCallContext.cs` -- GetString, GetInt accessor methods verified
- `Packages/manifest.json` -- com.unity.inputsystem 1.18.0, com.unity.modules.uielements confirmed
- `ProjectSettings/ProjectSettings.asset` -- activeInputHandler: 1 (new Input System only) confirmed
- `/home/cachy/workspaces/projects/persona/samples/personas/aya.json` -- Aya persona definition with animations list verified

### Secondary (MEDIUM confidence)
- [Unity 6 Package Manifest Reference](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-manifestPkg.html) -- `Samples~/` path format
- [Unity 6 Create Samples for Packages](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-samples.html) -- Sample folder convention
- [Unity 6 Get Started with Runtime UI](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-get-started-with-runtime-ui.html) -- UIDocument, PanelSettings, UXML/USS pattern
- [Unity Input System Keyboard docs](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.0/manual/Keyboard.html) -- Keyboard.current.spaceKey API
- [Unity ScrollView docs](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-uxml-element-ScrollView.html) -- scrollOffset property for auto-scrolling

### Tertiary (LOW confidence)
- None -- all findings verified with primary or secondary sources.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries already in project, verified via manifest and project settings
- Architecture: HIGH -- patterns derived directly from existing PersonaSession API and Firebase SDK source code
- Pitfalls: HIGH -- derived from reading actual source code (FunctionRegistry freeze, AudioPlayback OnAudioFilterRead, Input System config)
- UI Toolkit patterns: MEDIUM -- based on official Unity 6 docs, not tested in this specific project
- UPM Samples path: MEDIUM -- conflicting documentation, but Unity 6 manifest reference is authoritative

**Research date:** 2026-02-05
**Valid until:** 2026-03-05 (stable -- all APIs are production, no fast-moving dependencies)
