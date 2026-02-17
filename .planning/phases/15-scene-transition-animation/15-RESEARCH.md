# Phase 15: Scene Transition & Animation - Research

**Researched:** 2026-02-17
**Domain:** Gemini function call registration for animations, ScriptableObject-based animation config, toast/notification UI in UI Toolkit, clean scene loading via SceneManager
**Confidence:** HIGH

## Summary

Phase 15 adds two capabilities to the livestream sample: (1) animation function calls that Gemini can trigger during conversation, with a ScriptableObject defining available animations and a toast UI showing the trigger, and (2) a clean scene transition from the livestream to a movie clip scene when the NarrativeDirector signals all beats are complete.

The existing codebase provides everything needed. The function calling system (`PersonaSession.RegisterFunction`, `FunctionDeclaration`, `FunctionCallContext`, `FunctionRegistry`) is mature and well-documented. The existing `AyaSampleController.RegisterFunctions()` already demonstrates the exact pattern -- registering an "emote" function with a string parameter. The ScriptableObject pattern from `ChatBotConfig` and `NarrativeBeatConfig` provides the template for an `AnimationConfig` ScriptableObject. The NarrativeDirector (Phase 14) will expose `OnAllBeatsComplete` event which signals the scene transition. `PersonaSession.Disconnect()` and `PersonaSession.OnDestroy()` already handle clean WebSocket teardown on scene unload.

The user has simplified the original requirements significantly: no additive scene loading (clean exit instead), no pre-loading, instant cut transition (no fade), and the animation handlers are toast-based placeholders (not actual Animator integration). This makes the phase straightforward.

**Primary recommendation:** Create an `AnimationConfig` ScriptableObject listing available animations (name + description pairs), register them as a single `play_animation` function call with an enum parameter, show a toast Label in the LivestreamUI when triggered, and load the movie scene via `SceneManager.LoadSceneAsync` with `LoadSceneMode.Single` when `NarrativeDirector.OnAllBeatsComplete` fires.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| PersonaSession.RegisterFunction | local package | Register animation function calls before Connect() | Already proven in AyaSampleController; FunctionRegistry freezes at connect time |
| FunctionDeclaration.AddEnum | local package | Constrain animation parameter to known names | Prevents hallucinated animation names; Gemini sees exact valid values |
| ScriptableObject | Unity 6 | AnimationConfig data definition | Consistent with ChatBotConfig, NarrativeBeatConfig, PersonaConfig patterns |
| UI Toolkit (Label) | Unity 6 | Toast notification display | Already used by LivestreamUI; no new UI framework needed |
| SceneManager.LoadSceneAsync | Unity 6 | Clean scene transition to movie clip | Standard Unity scene loading; LoadSceneMode.Single destroys current scene |
| NarrativeDirector.OnAllBeatsComplete | Phase 14 | Trigger for scene transition | Already designed in 14-02-PLAN with this exact event |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Awaitable.WaitForSecondsAsync | Unity 6 | Toast auto-dismiss timing | Delay before removing toast Label from UI tree |
| destroyCancellationToken | Unity 6 | Cancel toast dismiss on scene unload | Prevents MissingReferenceException if scene unloads during toast |
| PersonaSession.Disconnect | local package | Clean WebSocket teardown before scene load | Call before LoadSceneAsync to avoid dangling connections |
| Debug.Log | Unity | Console logging of animation triggers | Required by CONTEXT.md: "log to console AND show toast" |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Single `play_animation` with enum | Individual function per animation (wave, laugh, etc.) | Enum approach: one registration, Gemini sees all options at once, easier to extend via ScriptableObject. Individual functions: more registrations but simpler handlers. Enum wins for extensibility. |
| Custom toast VisualElement | Unity App UI Toast class | App UI is a separate package dependency not in the project; a simple Label with USS animation is sufficient and keeps zero new dependencies |
| SceneManager.LoadSceneAsync | SceneManager.LoadScene (sync) | Async avoids frame stutter during load; user said brief loading moment is acceptable but we should not freeze the frame |

## Architecture Patterns

### Recommended Project Structure
```
Assets/AyaLiveStream/
  AnimationConfig.cs              # NEW -- ScriptableObject defining available animations
  SceneTransitionHandler.cs       # NEW -- listens to OnAllBeatsComplete, loads movie scene
  AyaSampleController.cs          # MODIFIED -- register animations from AnimationConfig, show toast
  LivestreamUI.cs                 # MODIFIED -- add ShowToast() method
  UI/
    LivestreamPanel.uxml          # MODIFIED -- add toast container element
    LivestreamPanel.uss           # MODIFIED -- add toast styles
  Data/
    AnimationConfig.asset         # NEW -- default animation set (wave, point, laugh, think, nod)
```

### Pattern 1: ScriptableObject Animation Registry
**What:** An `AnimationConfig` ScriptableObject holds an array of animation entries (name + description). At registration time, the controller reads this config and registers a single `play_animation` function with an enum parameter containing all animation names.
**When to use:** Always -- this is the data-driven animation definition pattern.
**Example:**
```csharp
// Source: follows ChatBotConfig pattern (Assets/AyaLiveStream/ChatBotConfig.cs)
// and FunctionDeclaration.AddEnum (Packages/.../Runtime/FunctionDeclaration.cs line 94-98)

[CreateAssetMenu(fileName = "AnimationConfig", menuName = "AI Embodiment/Samples/Animation Config")]
public class AnimationConfig : ScriptableObject
{
    [Serializable]
    public class AnimationEntry
    {
        [Tooltip("Animation name passed to function call (e.g. 'wave').")]
        public string name;

        [Tooltip("Description for Gemini to understand when to use this animation.")]
        public string description;
    }

    [Header("Available Animations")]
    [Tooltip("Animations Aya can trigger via function calls.")]
    public AnimationEntry[] animations;

    /// <summary>
    /// Returns animation names as a string array for FunctionDeclaration.AddEnum.
    /// </summary>
    public string[] GetAnimationNames()
    {
        if (animations == null) return System.Array.Empty<string>();
        var names = new string[animations.Length];
        for (int i = 0; i < animations.Length; i++)
            names[i] = animations[i].name;
        return names;
    }

    /// <summary>
    /// Builds a description string that includes all animation names and their purposes.
    /// </summary>
    public string GetFunctionDescription()
    {
        return "Play a character animation or gesture during conversation.";
    }
}
```

### Pattern 2: Data-Driven Function Registration
**What:** Instead of hardcoding function calls in RegisterFunctions(), read from the AnimationConfig ScriptableObject. This allows producers to add/remove animations without code changes.
**When to use:** Always -- replaces the current hardcoded emote registration.
**Example:**
```csharp
// Source: based on AyaSampleController.RegisterFunctions() pattern (line 41-53)
// Modified to use ScriptableObject data

[SerializeField] private AnimationConfig _animationConfig;

private void RegisterAnimationFunctions()
{
    if (_animationConfig == null || _animationConfig.animations == null
        || _animationConfig.animations.Length == 0)
    {
        Debug.LogWarning("No AnimationConfig assigned or empty.");
        return;
    }

    string[] animNames = _animationConfig.GetAnimationNames();

    var decl = new FunctionDeclaration(
            "play_animation",
            _animationConfig.GetFunctionDescription())
        .AddEnum("animation_name", "Name of the animation to play", animNames);

    _session.RegisterFunction("play_animation", decl, HandlePlayAnimation);
}

private IDictionary<string, object> HandlePlayAnimation(FunctionCallContext ctx)
{
    string animName = ctx.GetString("animation_name", "idle");
    Debug.Log($"[Animation] Triggered: {animName}");
    _livestreamUI.ShowToast($"Aya: *{animName}*");
    return null; // fire-and-forget
}
```

### Pattern 3: Toast Notification in UI Toolkit
**What:** A Label element positioned absolutely at the top or bottom of the UI that appears when an animation is triggered, then auto-dismisses after a delay. Simple USS class toggle for show/hide with opacity transition.
**When to use:** For all animation trigger feedback and optionally for scene transition status.
**Example:**
```csharp
// Source: LivestreamUI pattern (Assets/AyaLiveStream/LivestreamUI.cs)
// Uses the same schedule.Execute pattern for deferred UI operations

// In LivestreamUI.cs:
private Label _toastLabel;

// In OnEnable, after binding other elements:
_toastLabel = root.Q<Label>("toast-label");

public async void ShowToast(string message)
{
    _toastLabel.text = message;
    _toastLabel.AddToClassList("toast--visible");

    try
    {
        await Awaitable.WaitForSecondsAsync(3f, destroyCancellationToken);
    }
    catch (OperationCanceledException) { return; }

    _toastLabel.RemoveFromClassList("toast--visible");
}
```

```xml
<!-- In LivestreamPanel.uxml, inside root-container, after main-content -->
<ui:Label name="toast-label" text="" class="toast-label" />
```

```css
/* In LivestreamPanel.uss */
.toast-label {
    position: absolute;
    bottom: 60px;
    left: 50%;
    translate: -50% 0;
    background-color: rgba(40, 40, 60, 0.9);
    color: rgb(200, 170, 255);
    padding: 8px 16px;
    border-radius: 6px;
    font-size: 14px;
    -unity-font-style: italic;
    opacity: 0;
    transition: opacity 0.3s ease;
}

.toast--visible {
    opacity: 1;
}
```

### Pattern 4: Clean Scene Exit via NarrativeDirector Signal
**What:** A `SceneTransitionHandler` MonoBehaviour listens to `NarrativeDirector.OnAllBeatsComplete`. When fired, it calls `PersonaSession.Disconnect()`, then `SceneManager.LoadSceneAsync("MovieScene", LoadSceneMode.Single)`. The current scene is fully unloaded. No WebSocket preservation, no additive loading.
**When to use:** When the narrative arc completes (all 3 beats done).
**Example:**
```csharp
// Source: PersonaSession.Disconnect() (line 818-871), SceneManager API

using UnityEngine.SceneManagement;

public class SceneTransitionHandler : MonoBehaviour
{
    [SerializeField] private NarrativeDirector _narrativeDirector;
    [SerializeField] private PersonaSession _session;
    [SerializeField] private string _movieSceneName = "MovieScene";

    private void OnEnable()
    {
        if (_narrativeDirector != null)
            _narrativeDirector.OnAllBeatsComplete += HandleAllBeatsComplete;
    }

    private void OnDisable()
    {
        if (_narrativeDirector != null)
            _narrativeDirector.OnAllBeatsComplete -= HandleAllBeatsComplete;
    }

    private void HandleAllBeatsComplete()
    {
        Debug.Log("[SceneTransition] All beats complete. Transitioning to movie scene.");

        // Clean disconnect -- WebSocket closes, audio stops, TTS disposes
        if (_session != null)
            _session.Disconnect();

        // Load movie scene (destroys current scene entirely)
        SceneManager.LoadSceneAsync(_movieSceneName, LoadSceneMode.Single);
    }
}
```

### Anti-Patterns to Avoid
- **Registering functions after Connect():** FunctionRegistry.Freeze() is called at connect time (PersonaSession.Connect line 182). All RegisterFunction calls must happen before Connect(). Registration in Start() before the intro coroutine calls Connect() is the correct pattern.
- **Using SceneManager.LoadScene (sync) instead of LoadSceneAsync:** Synchronous load causes a frame freeze. Even though the user said a loading moment is acceptable, freezing the frame is jarring. LoadSceneAsync provides the same behavior but without the stall.
- **Keeping WebSocket alive across scene transitions:** The user explicitly decided on clean exit. PersonaSession.OnDestroy() already handles teardown, but calling Disconnect() explicitly before scene load is cleaner and avoids relying on destruction order.
- **Hand-rolling a toast system with multiple elements:** A single Label with USS opacity transition is sufficient. No need for a toast queue, multiple simultaneous toasts, or a toast manager class.
- **Registering individual functions per animation:** Creates N function registrations instead of 1. The enum parameter approach is cleaner, more extensible (add animations to ScriptableObject without code changes), and gives Gemini better context about available options.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Animation function calling | Custom message parsing from transcription | PersonaSession.RegisterFunction + FunctionDeclaration.AddEnum | The function calling system handles both native and prompt-based paths, with cancellation and error handling built in |
| WebSocket cleanup on scene exit | Manual WebSocket.CloseAsync | PersonaSession.Disconnect() | Already handles CTS cancellation, audio stop, TTS dispose, PacketAssembler reset, and client close with 2-second timeout |
| Scene loading | Custom async loading wrapper | SceneManager.LoadSceneAsync with LoadSceneMode.Single | Unity built-in; handles unloading current scene automatically |
| Toast auto-dismiss | Coroutine timer or manual Update tracking | Awaitable.WaitForSecondsAsync + destroyCancellationToken | Consistent with project's async pattern; auto-cancels on destroy |
| Animation config validation | Runtime name checking with string matching | FunctionDeclaration.AddEnum constraining valid values | Gemini can only call with valid enum values; bad names are prevented at the API level |

**Key insight:** The existing function calling system is the most mature part of the codebase (FunctionRegistry, FunctionDeclaration, FunctionCallContext, PersonaSession dispatch pipeline). Phase 15 should leverage it as-is with zero modifications to the package. The only new code is the ScriptableObject config, the handler that shows a toast, and the scene transition trigger.

## Common Pitfalls

### Pitfall 1: Registering Functions After Connect
**What goes wrong:** FunctionRegistry throws InvalidOperationException: "Cannot register functions after session has connected."
**Why it happens:** PersonaSession.Connect() calls `_functionRegistry.Freeze()` (line 182). Any RegisterFunction call after this point throws.
**How to avoid:** Register all functions in Start() or Awake(), before the intro sequence calls Connect(). The current AyaSampleController does this correctly (RegisterFunctions() at line 30 in Start(), Connect() at line 98 inside the coroutine).
**Warning signs:** InvalidOperationException in console at connect time.

### Pitfall 2: Toast Remaining Visible After Scene Unload
**What goes wrong:** If the toast auto-dismiss Awaitable is pending when the scene transitions, it could fail with MissingReferenceException.
**Why it happens:** The Awaitable completes after the MonoBehaviour is destroyed.
**How to avoid:** Use `destroyCancellationToken` in all Awaitable calls within the toast method. The OperationCanceledException catch returns early, preventing access to destroyed UI elements.
**Warning signs:** MissingReferenceException in console during scene transition.

### Pitfall 3: Movie Scene Not in Build Settings
**What goes wrong:** SceneManager.LoadSceneAsync fails silently or throws an exception because the target scene is not registered in Build Settings.
**Why it happens:** New Unity scenes must be added to File > Build Settings > Scenes In Build before they can be loaded by name.
**How to avoid:** Document that the movie scene must be added to Build Settings. Include a runtime warning if the scene name is not found.
**Warning signs:** "Scene 'MovieScene' couldn't be loaded" error in console.

### Pitfall 4: PersonaSession.OnDestroy Race with SceneManager
**What goes wrong:** If Disconnect() is not called explicitly and the scene unloads, OnDestroy may fire in an unpredictable order relative to other GameObjects being destroyed.
**Why it happens:** Unity does not guarantee destruction order within a scene.
**How to avoid:** Always call `_session.Disconnect()` explicitly before calling `SceneManager.LoadSceneAsync()`. This ensures the WebSocket is closed cleanly before any GameObjects start getting destroyed.
**Warning signs:** "WebSocketException" or "ObjectDisposedException" in console during scene transitions.

### Pitfall 5: Multiple Toasts Overlapping
**What goes wrong:** If Gemini triggers two animation calls in quick succession, two ShowToast calls overlap and the second one clears the first prematurely.
**Why it happens:** The second ShowToast call adds the visible class (no-op since it is already visible) but then the first Awaitable completes and removes the class while the second is still timing.
**How to avoid:** Track a toast counter or timestamp. Simplest: each ShowToast increments a counter, and the dismiss only removes the class if its counter matches the current value.
**Warning signs:** Toasts disappearing too quickly after rapid animation triggers.

## Code Examples

### Complete AnimationConfig ScriptableObject
```csharp
// Source: follows ChatBotConfig pattern (Assets/AyaLiveStream/ChatBotConfig.cs)
using System;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    [CreateAssetMenu(fileName = "AnimationConfig",
        menuName = "AI Embodiment/Samples/Animation Config")]
    public class AnimationConfig : ScriptableObject
    {
        [Serializable]
        public class AnimationEntry
        {
            public string name;
            [TextArea(1, 2)]
            public string description;
        }

        [Header("Available Animations")]
        public AnimationEntry[] animations;

        public string[] GetAnimationNames()
        {
            if (animations == null) return Array.Empty<string>();
            var names = new string[animations.Length];
            for (int i = 0; i < animations.Length; i++)
                names[i] = animations[i].name;
            return names;
        }
    }
}
```

### Registration and Handler in AyaSampleController
```csharp
// Source: AyaSampleController.RegisterFunctions() pattern (line 41-53)
// Enhanced with AnimationConfig ScriptableObject

[SerializeField] private AnimationConfig _animationConfig;
[SerializeField] private LivestreamUI _livestreamUI;

private void RegisterFunctions()
{
    // Animation function -- data-driven from ScriptableObject
    if (_animationConfig != null && _animationConfig.animations.Length > 0)
    {
        string[] animNames = _animationConfig.GetAnimationNames();
        var animDecl = new FunctionDeclaration(
                "play_animation",
                "Play a character animation or gesture. Use this to add expressiveness to conversation.")
            .AddEnum("animation_name", "Name of the animation to play", animNames);
        _session.RegisterFunction("play_animation", animDecl, HandlePlayAnimation);
    }

    // Scene transition function -- Gemini can trigger the movie reveal
    _session.RegisterFunction("start_movie",
        new FunctionDeclaration("start_movie", "Transition to the movie clip reveal scene"),
        HandleStartMovie);
}

private IDictionary<string, object> HandlePlayAnimation(FunctionCallContext ctx)
{
    string animName = ctx.GetString("animation_name", "idle");
    Debug.Log($"[Animation] play_animation triggered: {animName}");
    _livestreamUI?.ShowToast($"*{animName}*");
    return null; // fire-and-forget
}

private IDictionary<string, object> HandleStartMovie(FunctionCallContext ctx)
{
    Debug.Log("[SceneTransition] start_movie function called by Gemini");
    // Actual transition is handled by SceneTransitionHandler listening to OnAllBeatsComplete
    // This function call is informational -- Gemini signals intent, director controls timing
    return null;
}
```

### Toast UI in LivestreamUI
```csharp
// Source: LivestreamUI.cs (Assets/AyaLiveStream/LivestreamUI.cs)
// New method added to existing class

private Label _toastLabel;
private int _toastCounter;

// In OnEnable(), after existing bindings:
_toastLabel = root.Q<Label>("toast-label");

public async void ShowToast(string message, float duration = 3f)
{
    _toastLabel.text = message;
    _toastLabel.AddToClassList("toast--visible");
    int myCounter = ++_toastCounter;

    try
    {
        await Awaitable.WaitForSecondsAsync(duration, destroyCancellationToken);
    }
    catch (OperationCanceledException) { return; }

    // Only dismiss if no newer toast has been shown
    if (_toastCounter == myCounter)
    {
        _toastLabel.RemoveFromClassList("toast--visible");
    }
}
```

### Scene Transition Handler
```csharp
// Source: PersonaSession.Disconnect() + SceneManager.LoadSceneAsync

using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIEmbodiment.Samples
{
    public class SceneTransitionHandler : MonoBehaviour
    {
        [SerializeField] private NarrativeDirector _narrativeDirector;
        [SerializeField] private PersonaSession _session;
        [SerializeField] private string _movieSceneName = "MovieScene";
        [SerializeField] private LivestreamUI _livestreamUI;

        private bool _transitioning;

        private void OnEnable()
        {
            if (_narrativeDirector != null)
                _narrativeDirector.OnAllBeatsComplete += HandleAllBeatsComplete;
        }

        private void OnDisable()
        {
            if (_narrativeDirector != null)
                _narrativeDirector.OnAllBeatsComplete -= HandleAllBeatsComplete;
        }

        private void HandleAllBeatsComplete()
        {
            if (_transitioning) return;
            _transitioning = true;

            Debug.Log("[SceneTransition] Narrative complete. Loading movie scene.");
            _livestreamUI?.ShowToast("The story continues...");

            // Clean disconnect first
            if (_session != null)
                _session.Disconnect();

            // Verify scene is in build settings
            if (Application.CanStreamedLevelBeLoaded(_movieSceneName))
            {
                SceneManager.LoadSceneAsync(_movieSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError(
                    $"[SceneTransition] Scene '{_movieSceneName}' not found in Build Settings. " +
                    "Add it via File > Build Settings > Scenes In Build.");
            }
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Individual function per action (emote, start_movie, start_drawing) | Single function with enum parameter from ScriptableObject | Phase 15 redesign | More extensible, producer-editable, fewer registrations |
| Hardcoded animation names in code | ScriptableObject AnimationConfig with Inspector-editable entries | Phase 15 redesign | Producers can add/remove animations without code changes |
| Additive scene loading (preserve WebSocket) | Clean exit (full unload, Disconnect, LoadSceneMode.Single) | User simplified in Phase 15 CONTEXT.md | Much simpler implementation, no dual-AudioListener management |
| Pre-load with allowSceneActivation=false | No pre-load, brief loading moment acceptable | User simplified in Phase 15 CONTEXT.md | Removes complexity of managing deferred scene activation |

**Superseded from original roadmap:**
- ANI-02 originally required additive scene loading -- now simplified to clean exit
- ANI-03 originally required pre-loading with allowSceneActivation=false -- now removed entirely

## Open Questions

1. **Movie scene existence**
   - What we know: SceneTransitionHandler references a scene name (e.g., "MovieScene")
   - What's unclear: Does this scene exist yet? Is it a placeholder? Will Phase 16 create it?
   - Recommendation: Create a minimal placeholder MovieScene (empty scene with a label "Movie Clip Placeholder") so the scene transition can be tested end-to-end. Phase 16 can populate it with actual content.

2. **NarrativeDirector availability at planning time**
   - What we know: Phase 14 plans define NarrativeDirector with OnAllBeatsComplete event, but Phase 14 has not been executed yet
   - What's unclear: Will NarrativeDirector be fully implemented when Phase 15 executes?
   - Recommendation: Phase 15 depends on Phase 14 (stated in roadmap). Plan should reference NarrativeDirector.OnAllBeatsComplete and fail gracefully if the reference is null (guard with null check). This is also fine for standalone testing -- the scene transition handler can exist without a NarrativeDirector and be wired in Phase 16.

3. **Existing emote/start_movie registrations in AyaSampleController**
   - What we know: AyaSampleController.RegisterFunctions() already registers "emote", "start_movie", and "start_drawing" as hardcoded functions
   - What's unclear: Should Phase 15 replace these, extend them, or leave them and add new ones alongside?
   - Recommendation: Replace the existing hardcoded "emote" registration with the data-driven AnimationConfig approach. The "start_movie" function may be retained or replaced by the NarrativeDirector signal -- the scene transition should be director-driven, not Gemini-driven.

## Sources

### Primary (HIGH confidence)
- `Packages/com.google.ai-embodiment/Runtime/FunctionRegistry.cs` -- Registration API, Freeze(), dual-path output
- `Packages/com.google.ai-embodiment/Runtime/FunctionDeclaration.cs` -- AddEnum, AddString, ToToolJson, ToPromptText
- `Packages/com.google.ai-embodiment/Runtime/FunctionCallContext.cs` -- GetString, GetInt, typed argument access
- `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` -- RegisterFunction, Connect, Disconnect, OnDestroy, event system
- `Assets/AyaLiveStream/AyaSampleController.cs` -- Existing RegisterFunctions pattern (line 41-53), HandleEmote (line 56-61)
- `Assets/AyaLiveStream/ChatBotConfig.cs` -- ScriptableObject pattern to follow
- `Assets/AyaLiveStream/NarrativeBeatConfig.cs` -- ScriptableObject pattern, SceneType.AyaAction placeholder
- `Assets/AyaLiveStream/LivestreamUI.cs` -- UI Toolkit pattern, UIDocument binding, schedule.Execute
- `.planning/phases/14-narrative-director-user-interaction/14-02-PLAN.md` -- OnAllBeatsComplete event design (line 98, 166)
- `.planning/phases/15-scene-transition-animation/15-CONTEXT.md` -- User decisions (clean exit, toast, instant cut)

### Secondary (MEDIUM confidence)
- [Unity SceneManager.LoadScene documentation](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/SceneManagement.SceneManager.LoadScene.html) -- LoadSceneMode.Single destroys current scene
- [Unity SceneManager.LoadSceneAsync documentation](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/SceneManagement.SceneManager.html) -- Async scene loading API

### Tertiary (LOW confidence)
- None -- all findings verified against codebase source code

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all components are existing codebase classes with verified APIs
- Architecture: HIGH -- patterns follow established codebase conventions (ScriptableObject, RegisterFunction, UI Toolkit)
- Pitfalls: HIGH -- all pitfalls identified from actual codebase constraints (Freeze at connect, destroyCancellationToken, Build Settings)
- Code examples: HIGH -- derived directly from existing codebase patterns with minimal adaptation

**Research date:** 2026-02-17
**Valid until:** Indefinite (based on stable local codebase, not external libraries)
