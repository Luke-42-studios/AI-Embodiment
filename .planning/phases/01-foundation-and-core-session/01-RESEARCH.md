# Phase 1: Foundation and Core Session - Research

**Researched:** 2026-02-05
**Domain:** Unity UPM package structure, Firebase AI Logic LiveSession lifecycle, threading, ScriptableObject configuration
**Confidence:** HIGH

## Summary

Phase 1 establishes the UPM package skeleton, thread marshaling infrastructure, persona configuration ScriptableObject, and a working Gemini Live session that sustains multi-turn text conversation. The phase requires building three tightly coupled subsystems: (1) the UPM package structure with assembly definitions that correctly reference the Firebase AI SDK, (2) a ConcurrentQueue-based MainThreadDispatcher that safely bridges background WebSocket threads to Unity's main thread, and (3) a PersonaSession MonoBehaviour that wraps LiveSession with proper lifecycle management (CancellationToken tied to OnDestroy, outer receive loop that re-calls ReceiveAsync after each TurnComplete).

The Firebase AI Logic SDK 13.7.0 source code is already in the project and provides full visibility into the WebSocket protocol. The SDK is confirmed as the latest available version. Key findings from this research: (a) Gemini 2.0 Flash models are being retired March 31, 2026 -- the default model must be set to `gemini-2.5-flash-native-audio-preview-12-2025` (GoogleAI) or `gemini-live-2.5-flash-native-audio` (VertexAI); (b) the official Firebase docs now show `GoogleAI()` backend in all Live API examples, suggesting the ConnectAsync model path bug flagged in project research may not be a real issue in practice; (c) the Live API supports text-only sessions with `ResponseModality.Text`, but native audio models require audio input -- for Phase 1 text-only testing, use `SendAsync` with `turnComplete: true` rather than `SendTextRealtimeAsync`; (d) the output audio sample rate is confirmed as 24kHz (from official Firebase limits/specs documentation).

**Primary recommendation:** Use `ConcurrentQueue<Action>` for the MainThreadDispatcher, `GoogleAI()` backend, and `gemini-2.5-flash-native-audio-preview-12-2025` as the default model. Structure the UPM package at the project root (not under Assets/) and add an asmdef to the Firebase AI SDK source files to enable cross-assembly referencing.

## Standard Stack

The stack is fully determined by what is already in the project. No new dependencies should be added for Phase 1.

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Unity 6 | 6000.3.7f1 | Game engine, component model, AudioSource | Already in use, LTS-equivalent |
| C# 9.0 / .NET Standard 2.1 | Per csproj | Language runtime with async/await, IAsyncEnumerable | Confirmed in project, provides all needed async primitives |
| Firebase AI Logic SDK | 13.7.0 (source) | Gemini Live bidirectional streaming | Already imported, full source visibility at `Assets/Firebase/FirebaseAI/` |
| Firebase App SDK | 13.7.0 (native) | Firebase initialization, API key management | Required by Firebase AI Logic |
| System.Collections.Concurrent | .NET Standard 2.1 | ConcurrentQueue for thread-safe dispatch | Built-in, no NuGet package needed, proven pattern |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Google.MiniJSON | Bundled with Firebase | JSON serialization for Firebase wire protocol | Internal to SDK only; not for project code |
| UnityEngine.JsonUtility | Built-in | Serialization for project data structures | If needed for any custom JSON serialization |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ConcurrentQueue<Action> | System.Threading.Channels | Channels is not built into Unity's .NET Standard 2.1 -- requires NuGet package, has known Unity issues; ConcurrentQueue is simpler and sufficient |
| ConcurrentQueue<Action> | UnitySynchronizationContext.Post() | Higher allocation overhead per callback, less explicit control over drain timing |
| GoogleAI() backend | VertexAI() backend | VertexAI requires a location parameter and has slightly different model names; GoogleAI is simpler and matches official docs |
| Standard Task/async-await | UniTask | UniTask forces a dependency on package consumers; standard Task works fine for this use case |

**Installation:**
No new packages to install. Firebase AI SDK is already present. The UPM package structure is created from existing project code.

## Architecture Patterns

### Recommended Project Structure (UPM Package)

```
com.luke42studios.ai-embodiment/    (project root, IS the UPM package)
  package.json
  README.md
  LICENSE.md
  CHANGELOG.md
  Runtime/
    com.luke42studios.ai-embodiment.asmdef
    PersonaConfig.cs                    # ScriptableObject
    PersonaSession.cs                   # MonoBehaviour - session lifecycle
    MainThreadDispatcher.cs             # ConcurrentQueue dispatcher
    SessionState.cs                     # Enum
    SystemInstructionBuilder.cs         # Pure function
    Internal/                           # Not part of public API
      (reserved for future internal utilities)
  Editor/
    com.luke42studios.ai-embodiment.editor.asmdef
    (empty for Phase 1 -- custom editors in later phases)
  Tests/
    Runtime/
      com.luke42studios.ai-embodiment.tests.asmdef
      SystemInstructionBuilderTests.cs
      PersonaConfigTests.cs
    Editor/
      com.luke42studios.ai-embodiment.editor.tests.asmdef
  Samples~/
    (empty for Phase 1 -- samples in Phase 6)
  Documentation~/
    (empty for Phase 1)
```

**Critical: Firebase asmdef requirement.** The Firebase AI SDK source files at `Assets/Firebase/FirebaseAI/` have no assembly definition, so they compile into `Assembly-CSharp`. The UPM package's asmdef CANNOT reference `Assembly-CSharp`. Solution: Add a `Firebase.AI.asmdef` file to `Assets/Firebase/FirebaseAI/` so it compiles as a separate assembly that the package can reference. This is a prerequisite before any Phase 1 code compiles.

### Pattern 1: MainThreadDispatcher (ConcurrentQueue)

**What:** A MonoBehaviour that drains a ConcurrentQueue<Action> every Update frame, executing enqueued callbacks on the main thread.

**When to use:** Every time data arrives from the Firebase LiveSession receive loop (which runs on a .NET thread pool thread).

**Example:**
```csharp
// Source: Established Unity pattern, verified against multiple implementations
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace AIEmbodiment
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static MainThreadDispatcher _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance == null)
            {
                var go = new GameObject("[MainThreadDispatcher]");
                _instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _queue.Enqueue(action);
        }

        private void Update()
        {
            while (_queue.TryDequeue(out Action action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
```

**Key design decisions:**
- Static `Enqueue` method so background threads can call it without a reference to the MonoBehaviour
- `[RuntimeInitializeOnLoadMethod]` ensures it exists before any session starts
- `DontDestroyOnLoad` survives scene transitions
- Try-catch per action prevents one bad callback from killing the drain loop
- No frame budget limit needed for Phase 1 (text-only produces minimal callbacks)

### Pattern 2: Receive Loop with Outer While Loop

**What:** The PersonaSession runs a continuous receive loop as a background Task, wrapping ReceiveAsync in an outer while loop to handle the TurnComplete break behavior.

**When to use:** For the entire duration of a connected session.

**Example:**
```csharp
// Source: Firebase AI Logic SDK LiveSession.cs lines 287-339 (ReceiveAsync breaks on TurnComplete)
private async Task ReceiveLoopAsync(LiveSession session, CancellationToken ct)
{
    try
    {
        while (!ct.IsCancellationRequested)
        {
            await foreach (var response in session.ReceiveAsync(ct))
            {
                ProcessResponse(response); // Enqueues to main thread via dispatcher
            }
            // ReceiveAsync completed because TurnComplete was received.
            // Loop back to receive the next turn.
        }
    }
    catch (OperationCanceledException)
    {
        // Expected on cancellation -- session is shutting down
    }
    catch (WebSocketException ex)
    {
        MainThreadDispatcher.Enqueue(() => HandleError(ex));
    }
    catch (Exception ex)
    {
        MainThreadDispatcher.Enqueue(() => HandleError(ex));
    }
}
```

### Pattern 3: CancellationTokenSource Lifecycle Binding

**What:** A CancellationTokenSource created per session, cancelled in OnDestroy (and OnDisable for safety).

**When to use:** Every PersonaSession instance.

**Example:**
```csharp
// Source: Standard .NET CancellationToken pattern for Unity lifecycle
private CancellationTokenSource _sessionCts;
private LiveSession _liveSession;

public async Task Connect()
{
    _sessionCts = new CancellationTokenSource();
    // ... create model, connect ...
    _liveSession = await liveModel.ConnectAsync(_sessionCts.Token);
    _ = ReceiveLoopAsync(_liveSession, _sessionCts.Token); // Fire and forget with error handling inside
}

public async Task Disconnect()
{
    _sessionCts?.Cancel();
    if (_liveSession != null)
    {
        try { await _liveSession.CloseAsync(CancellationToken.None); }
        catch (Exception) { /* WebSocket may already be closed */ }
        _liveSession.Dispose();
        _liveSession = null;
    }
    _sessionCts?.Dispose();
    _sessionCts = null;
}

private void OnDestroy()
{
    _sessionCts?.Cancel();
    _liveSession?.Dispose();
    _sessionCts?.Dispose();
}
```

### Pattern 4: ScriptableObject Configuration

**What:** PersonaConfig as a ScriptableObject with [CreateAssetMenu] for Inspector creation.

**When to use:** Developers define persona personalities, model selection, and voice settings.

**Example:**
```csharp
// Source: Standard Unity ScriptableObject pattern
using UnityEngine;

namespace AIEmbodiment
{
    [CreateAssetMenu(fileName = "NewPersonaConfig", menuName = "AI Embodiment/Persona Config")]
    public class PersonaConfig : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "New Persona";
        public string archetype = "companion";

        [TextArea(3, 10)]
        public string backstory;

        [Header("Personality")]
        public string[] personalityTraits;

        [TextArea(2, 5)]
        public string speechPatterns;

        [Header("Model")]
        public string modelName = "gemini-2.5-flash-native-audio-preview-12-2025";

        [Range(0f, 2f)]
        public float temperature = 0.7f;

        [Header("Voice")]
        public VoiceBackend voiceBackend = VoiceBackend.GeminiNative;
        public string geminiVoiceName = "Puck";
        public string chirpVoiceName = "en-US-Chirp3-HD-Achernar";
    }

    public enum VoiceBackend
    {
        GeminiNative,
        ChirpTTS
    }
}
```

### Pattern 5: SystemInstructionBuilder (Pure Function)

**What:** A static utility that composes PersonaConfig fields into a `ModelContent` system instruction for the Gemini Live API.

**When to use:** At session connect time, before calling `GetLiveModel()`.

**Example:**
```csharp
// Source: Firebase AI SDK ModelContent.cs -- system instruction uses ModelContent with role "system"
using Firebase.AI;

namespace AIEmbodiment
{
    public static class SystemInstructionBuilder
    {
        public static ModelContent Build(PersonaConfig config)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"You are {config.displayName}, a {config.archetype}.");

            if (!string.IsNullOrEmpty(config.backstory))
            {
                sb.AppendLine();
                sb.AppendLine("BACKSTORY:");
                sb.AppendLine(config.backstory);
            }

            if (config.personalityTraits != null && config.personalityTraits.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("PERSONALITY TRAITS:");
                foreach (var trait in config.personalityTraits)
                {
                    if (!string.IsNullOrEmpty(trait))
                        sb.AppendLine($"- {trait}");
                }
            }

            if (!string.IsNullOrEmpty(config.speechPatterns))
            {
                sb.AppendLine();
                sb.AppendLine("SPEECH PATTERNS:");
                sb.AppendLine(config.speechPatterns);
            }

            return ModelContent.Text(sb.ToString());
        }
    }
}
```

### Pattern 6: State Machine for Session Lifecycle

**What:** A SessionState enum with event-driven transitions.

**When to use:** PersonaSession exposes its connection state to consumer code.

**Example:**
```csharp
namespace AIEmbodiment
{
    public enum SessionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        Error
    }
}
```

Events are exposed as C# delegates (not UnityEvents) for type safety and library code:
```csharp
public event Action<SessionState> OnStateChanged;
public event Action<string> OnTextReceived;
public event Action<Exception> OnError;
```

### Anti-Patterns to Avoid

- **Calling Unity API from background thread:** The ReceiveAsync loop runs on a thread pool thread. Never touch AudioSource, Transform, GameObject, or any UnityEngine.Object from within the receive loop. Always enqueue to MainThreadDispatcher.
- **Multiple concurrent ReceiveAsync calls:** The SDK explicitly warns: "Having multiple of these ongoing will result in unexpected behavior." Exactly ONE receive loop per LiveSession, ever.
- **Blocking async in MonoBehaviour methods:** Never use `.Result` or `.Wait()` on Tasks in Start/Update. Use `async void` only for MonoBehaviour entry points (Start, event handlers) with try-catch.
- **Tight coupling to Firebase SDK types:** The SDK is in Public Preview. Define package-owned types (SessionState, PersonaConfig) and convert from Firebase types at the boundary. Only PersonaSession and SystemInstructionBuilder touch Firebase types directly.
- **Using `async void` without try-catch:** Unhandled exceptions in `async void` methods crash Unity silently. Always wrap in try-catch.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Thread-safe queue | Custom lock-based queue | ConcurrentQueue<Action> | Lock-free, built into .NET, battle-tested |
| JSON serialization for Firebase | Custom JSON builder | Google.MiniJSON (bundled) or the SDK's ToJson/FromJson methods | SDK already handles all wire format details |
| WebSocket management | Custom ClientWebSocket wrapper | LiveSession from Firebase SDK | Handles framing, protocol, reconnection signaling |
| Async cancellation | Boolean flags | CancellationTokenSource | Standard .NET pattern, integrates with all async APIs |
| System instruction formatting | Raw string concatenation | SystemInstructionBuilder + ModelContent.Text() | ModelContent is the SDK's expected type, handles JSON serialization |

**Key insight:** Phase 1's value is in the GLUE between established patterns, not in novel algorithms. Every individual component (queue, config, async loop) has a well-known solution. The challenge is wiring them together correctly with proper lifecycle management.

## Common Pitfalls

### Pitfall 1: ReceiveAsync Single-Turn Trap

**What goes wrong:** ReceiveAsync() breaks on TurnComplete (LiveSession.cs line 328-333). A single `await foreach` over ReceiveAsync covers only one model turn. The session appears to "die" after the first response.
**Why it happens:** The Gemini Live protocol is turn-based. The SDK breaks the enumerable at each TurnComplete.
**How to avoid:** Wrap ReceiveAsync in an outer `while (!ct.IsCancellationRequested)` loop that re-calls it after each turn completes.
**Warning signs:** Session works for one exchange but second prompt gets no response.

### Pitfall 2: Threading -- Firebase Async vs Unity Main Thread

**What goes wrong:** The Firebase SDK's `ReceiveAsync` and `ClientWebSocket.ReceiveAsync` complete on .NET thread pool threads. Touching any Unity API (AudioSource, Transform, Debug.Log is fine) from these threads causes crashes or silent corruption.
**Why it happens:** Unity is single-threaded. `IAsyncEnumerable` iteration resumes on whatever thread the WebSocket read completes on.
**How to avoid:** The single receive loop on the background thread enqueues ALL data to MainThreadDispatcher. No Unity API calls in the receive loop except `Debug.Log`.
**Warning signs:** `UnityException: ... can only be called from the main thread`, intermittent NullReferenceExceptions.

### Pitfall 3: WebSocket Lifetime vs Unity Lifecycle Mismatch

**What goes wrong:** Scene transitions, OnDestroy, or application quit can leak WebSocket connections. The background receive Task continues running after the MonoBehaviour is destroyed.
**Why it happens:** Unity lifecycle (OnDestroy is synchronous) and C# async lifetime (cooperative cancellation) are fundamentally different paradigms.
**How to avoid:** CancellationTokenSource created per session, cancelled in OnDestroy. Explicit CloseAsync + Dispose in Disconnect(). OnApplicationQuit as safety net.
**Warning signs:** Editor freezes when stopping Play mode. Console errors after scene change. Memory grows across transitions.

### Pitfall 4: SetupComplete Response is Silently Swallowed

**What goes wrong:** The SDK's `LiveSessionResponse.FromJson` (line 126-129) returns null for setupComplete, and ReceiveAsync skips null responses (line 323). The session setup acknowledgment is invisible. Sending data before setup completes may cause the server to reject it.
**Why it happens:** SDK design choice to hide the handshake. But timing matters.
**How to avoid:** Start ReceiveAsync immediately after ConnectAsync. The first successful iteration (even if it yields nothing visible) means setup is likely complete. Alternatively, add a small delay (200ms) after ConnectAsync before sending the first message.
**Warning signs:** First message sometimes gets no response. Works locally but fails remotely.

### Pitfall 5: ConnectAsync Model Path Uses VertexAI Format for All Backends

**What goes wrong:** The setup message at LiveGenerativeModel.cs line 154 hardcodes the VertexAI-style model path: `projects/{id}/locations/{_backend.Location}/publishers/google/models/{name}`. For GoogleAI backend, `_backend.Location` is null, producing `locations//publishers`.
**Why it happens:** SDK bug -- the GetModelName() method correctly branches by backend, but ConnectAsync does not call GetModelName().
**How to avoid:** Official Firebase docs show GoogleAI() backend working for Live sessions, so the server may accept this format. Use GoogleAI() backend as shown in official docs and test early. If it fails, switch to VertexAI("us-central1") backend. This should be verified in the very first integration test.
**Warning signs:** ConnectAsync throws or the session immediately errors after setup.

### Pitfall 6: Gemini 2.0 Flash Retirement

**What goes wrong:** Using model name `gemini-2.0-flash-live-001` (the old default) will stop working on March 31, 2026.
**Why it happens:** Google is deprecating Gemini 2.0 Flash models.
**How to avoid:** Use `gemini-2.5-flash-native-audio-preview-12-2025` (GoogleAI) as the default model name. Make model name configurable in PersonaConfig so it can be updated without code changes.
**Warning signs:** API errors starting late March 2026 if old model names are used.

### Pitfall 7: LiveSession.Dispose() Does Not Await Close

**What goes wrong:** `Dispose()` at LiveSession.cs line 66 calls `CloseAsync` without awaiting it. The WebSocket close handshake may not complete before GC collects the object, leaking the connection.
**Why it happens:** `IDisposable.Dispose()` is synchronous by convention; the SDK cannot await in it.
**How to avoid:** In PersonaSession.Disconnect(), explicitly call `await session.CloseAsync(ct)` BEFORE calling `session.Dispose()`. Handle the case where CloseAsync throws (WebSocket may already be closed or in a transitional state).
**Warning signs:** Server-side billing for idle sessions. WebSocket connections not properly closed.

## Code Examples

### Complete PersonaSession Connect Flow

```csharp
// Source: Firebase AI Logic SDK API + official Firebase docs
using Firebase.AI;

public async Task Connect()
{
    if (State != SessionState.Disconnected) return;
    if (_config == null)
    {
        Debug.LogError("PersonaSession: No PersonaConfig assigned.");
        return;
    }

    SetState(SessionState.Connecting);

    try
    {
        _sessionCts = new CancellationTokenSource();

        var ai = FirebaseAI.GetInstance(FirebaseAI.Backend.GoogleAI());

        var liveConfig = new LiveGenerationConfig(
            responseModalities: new[] { ResponseModality.Audio },
            speechConfig: SpeechConfig.UsePrebuiltVoice(_config.geminiVoiceName),
            temperature: _config.temperature,
            inputAudioTranscription: new AudioTranscriptionConfig(),
            outputAudioTranscription: new AudioTranscriptionConfig()
        );

        var systemInstruction = SystemInstructionBuilder.Build(_config);

        var liveModel = ai.GetLiveModel(
            modelName: _config.modelName,
            liveGenerationConfig: liveConfig,
            systemInstruction: systemInstruction
        );

        _liveSession = await liveModel.ConnectAsync(_sessionCts.Token);

        SetState(SessionState.Connected);

        // Start receive loop on background thread
        _ = ReceiveLoopAsync(_liveSession, _sessionCts.Token);
    }
    catch (Exception ex)
    {
        SetState(SessionState.Error);
        OnError?.Invoke(ex);
        Debug.LogError($"PersonaSession: Connection failed: {ex.Message}");
    }
}
```

### Response Processing (Dispatched to Main Thread)

```csharp
// Source: LiveSessionResponse.cs -- ILiveSessionMessage pattern matching
private void ProcessResponse(LiveSessionResponse response)
{
    // This runs on background thread -- enqueue everything to main thread
    string text = response.Text;

    if (response.Message is LiveSessionContent content)
    {
        if (!string.IsNullOrEmpty(text))
        {
            MainThreadDispatcher.Enqueue(() => OnTextReceived?.Invoke(text));
        }

        if (content.TurnComplete)
        {
            MainThreadDispatcher.Enqueue(() => OnTurnComplete?.Invoke());
        }

        if (content.Interrupted)
        {
            MainThreadDispatcher.Enqueue(() => OnInterrupted?.Invoke());
        }

        if (content.InputTranscription.HasValue)
        {
            string transcript = content.InputTranscription.Value.Text;
            MainThreadDispatcher.Enqueue(() => OnInputTranscription?.Invoke(transcript));
        }

        if (content.OutputTranscription.HasValue)
        {
            string transcript = content.OutputTranscription.Value.Text;
            MainThreadDispatcher.Enqueue(() => OnOutputTranscription?.Invoke(transcript));
        }
    }
    else if (response.Message is LiveSessionToolCall toolCall)
    {
        // Phase 4 -- function calling (not implemented in Phase 1)
    }
}
```

### Sending Text via SendAsync (Phase 1 Text Testing)

```csharp
// Source: LiveSession.cs -- SendAsync with turnComplete flag
public async Task SendText(string message)
{
    if (_liveSession == null || State != SessionState.Connected) return;

    try
    {
        await _liveSession.SendAsync(
            content: ModelContent.Text(message),
            turnComplete: true,
            cancellationToken: _sessionCts.Token
        );
    }
    catch (Exception ex)
    {
        MainThreadDispatcher.Enqueue(() =>
        {
            OnError?.Invoke(ex);
            Debug.LogError($"PersonaSession: Send failed: {ex.Message}");
        });
    }
}
```

### Assembly Definition for Runtime

```json
{
    "name": "com.luke42studios.ai-embodiment",
    "rootNamespace": "AIEmbodiment",
    "references": [
        "Firebase.AI"
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

### Firebase AI Assembly Definition (to add to vendor SDK)

```json
{
    "name": "Firebase.AI",
    "rootNamespace": "Firebase.AI",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "Google.MiniJson.dll",
        "Firebase.App.dll",
        "Firebase.Platform.dll",
        "Firebase.TaskExtension.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Note:** The Firebase AI SDK source references `Firebase.App` types (FirebaseApp), `Google.MiniJSON` (Json), `Firebase.AI.Internal` types, and `Firebase.Platform`/`Firebase.TaskExtension` types. The asmdef must use `overrideReferences: true` with `precompiledReferences` listing the Firebase DLLs. The exact DLL names should be verified by checking `Assets/Firebase/Plugins/`.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `gemini-2.0-flash-live-001` model | `gemini-2.5-flash-native-audio-preview-12-2025` | Dec 2025 | 2.0 Flash retiring March 31, 2026. Must use 2.5 Flash. |
| `SendMediaChunksAsync` for audio | `SendAudioRealtimeAsync` / `SendAudioAsync` | SDK 13.4.0 (Oct 2025) | Old method deprecated, new method is type-specific |
| VertexAI backend recommended | GoogleAI backend shown in official docs | Late 2025 | Official Firebase docs now use GoogleAI() for Live API examples |
| 5 Gemini native voices | 30 Chirp 3-based voices via SpeechConfig | Dec 2025 | Puck, Kore, Aoede, Charon, Fenrir plus 25 additional voices (Zephyr, Autonoe, Orus, Umbriel, Callirrhoe, Erinome, Iapetus, Laomedeia, Schedar, Achird, Sadachbia, Enceladus, Algieba, Algenib, Achernar, Gacrux, Zubenelgenubi, Sadaltager, Despina, Rasalgethi, Alnilam, Pulcherrima, Vindemiatrix, Sulafat, Leda) |

**Deprecated/outdated:**
- `gemini-2.0-flash-live-001` / `gemini-2.0-flash-live-preview-04-09`: Retired March 31, 2026
- `SendMediaChunksAsync`: Deprecated in SDK 13.4.0, replaced by type-specific send methods
- `gemini-live-2.5-flash-preview`: Superseded by native-audio variants

## Open Questions

Things that could not be fully resolved:

1. **ConnectAsync model path format with GoogleAI backend**
   - What we know: Line 154 hardcodes VertexAI-style path with `_backend.Location` (null for GoogleAI). Official docs show GoogleAI working.
   - What's unclear: Whether the server accepts `locations//publishers` (null location) or if there is a separate fix.
   - Recommendation: Use GoogleAI() as shown in docs. If ConnectAsync fails, switch to VertexAI("us-central1"). Test in the first integration test of Plan 01-03.

2. **Text-only Live API session for Phase 1 testing**
   - What we know: The Live API supports `ResponseModality.Text` for text-to-text. But native audio models "require audio input." The model `gemini-live-2.5-flash-preview` appears to support text-only mode.
   - What's unclear: Whether `gemini-2.5-flash-native-audio-preview-12-2025` accepts text input via `SendAsync` with `turnComplete: true` (as opposed to requiring audio).
   - Recommendation: For Phase 1 text testing, use `SendAsync` with `ModelContent.Text(message)` and `turnComplete: true`. The SendAsync path sends structured content (not realtime input), which should work with text regardless of response modality. If the native audio model rejects text-only input, fall back to requesting `ResponseModality.Text` for Phase 1 only.

3. **Firebase AI SDK asmdef compatibility**
   - What we know: Firebase AI source files at `Assets/Firebase/FirebaseAI/` need an asmdef. They reference `Firebase.App`, `Google.MiniJSON`, `Firebase.Platform`, `Firebase.TaskExtension` DLLs and `Firebase.AI.Internal` types plus `UnityEngine.Application`.
   - What's unclear: Exact DLL names for precompiledReferences. Whether the Internal/ subfolder needs its own asmdef or is included in the parent.
   - Recommendation: List all DLLs in `Assets/Firebase/Plugins/` before creating the asmdef. The Internal/ folder contains only internal helper classes in the `Firebase.AI.Internal` namespace and should be in the same assembly.

4. **Session duration limit**
   - What we know: Firebase docs state ~10 minute connection limit for audio sessions, 15 minutes for audio-only.
   - What's unclear: Whether text-only sessions have the same limit. Whether the SDK fires an event or just closes.
   - Recommendation: Handle unexpected WebSocket close in the receive loop. Log it and fire OnStateChanged(Disconnected). Consider auto-reconnect in a future phase.

## Sources

### Primary (HIGH confidence)
- Firebase AI Logic SDK 13.7.0 source code: `Assets/Firebase/FirebaseAI/LiveSession.cs`, `LiveGenerativeModel.cs`, `LiveSessionResponse.cs`, `LiveGenerationConfig.cs`, `FirebaseAI.cs`, `ModelContent.cs`, `FunctionCalling.cs`, `ResponseModality.cs`
- [Firebase AI Logic Live API documentation](https://firebase.google.com/docs/ai-logic/live-api) -- ConnectAsync examples, model names, GoogleAI backend usage
- [Firebase AI Logic supported models](https://firebase.google.com/docs/ai-logic/models) -- model deprecation dates, recommended replacements
- [Firebase AI Logic Live API configuration](https://firebase.google.com/docs/ai-logic/live-api/configuration) -- voice names, SpeechConfig, transcription config
- [Firebase AI Logic Live API limits and specs](https://firebase.google.com/docs/ai-logic/live-api/limits-and-specs) -- confirmed 16kHz input, 24kHz output, session duration, rate limits
- [Firebase Unity SDK releases](https://github.com/firebase/firebase-unity-sdk/releases) -- confirmed 13.7.0 is latest

### Secondary (MEDIUM confidence)
- [Gemini Live API capabilities guide](https://ai.google.dev/gemini-api/docs/live-guide) -- text-only mode support, multi-turn patterns
- [Gemini deprecations](https://ai.google.dev/gemini-api/docs/deprecations) -- Gemini 2.0 Flash retirement March 31, 2026
- Project research documents: `.planning/research/SUMMARY.md`, `.planning/research/ARCHITECTURE.md`, `.planning/research/PITFALLS.md`, `.planning/research/STACK.md`
- ConcurrentQueue MainThreadDispatcher pattern: Multiple Unity community implementations ([PimDeWitte/UnityMainThreadDispatcher](https://github.com/PimDeWitte/UnityMainThreadDispatcher), [gustavopsantos/UnityMainThreadDispatcher](https://github.com/gustavopsantos/UnityMainThreadDispatcher))

### Tertiary (LOW confidence)
- Text-only mode with native audio models: Not explicitly documented for `gemini-2.5-flash-native-audio-preview-12-2025`. Must test.
- Firebase AI asmdef DLL reference names: Must verify by listing `Assets/Firebase/Plugins/` contents.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all from SDK source code and official docs
- Architecture patterns: HIGH -- established Unity patterns with SDK source verification
- Pitfalls: HIGH -- verified against SDK source code line numbers and official documentation
- Model names/deprecation: HIGH -- from official Firebase docs (fetched 2026-02-05)
- Text-only Live API mode: MEDIUM -- docs confirm text support but unclear with native audio models
- asmdef configuration: MEDIUM -- standard pattern but Firebase DLL names need filesystem verification

**Research date:** 2026-02-05
**Valid until:** 2026-03-05 (30 days -- stable domain, but model deprecation date of March 31 makes model name verification time-sensitive)
