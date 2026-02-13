# Phase 8: PersonaSession Migration and Dependency Removal - Research

**Researched:** 2026-02-13
**Domain:** Unity dependency management, Firebase SDK removal, WebSocket client integration, ScriptableObject settings patterns
**Confidence:** HIGH

## Summary

Phase 8 rewires PersonaSession to use GeminiLiveClient (built in Phase 7) instead of the Firebase LiveSession, removes all Firebase.AI references from the runtime package, and ensures the project compiles cleanly. The existing public API surface (events, methods, properties) stays functionally identical.

The codebase analysis reveals a well-contained Firebase dependency surface. Firebase types are used in exactly 5 files in the runtime package (PersonaSession.cs, SystemInstructionBuilder.cs, FunctionRegistry.cs, ChirpTTSClient.cs, and the runtime asmdef) plus 2 sample scene files (AyaSampleController.cs and its asmdef). The core architectural change is replacing Firebase's pull-based receive loop (`IAsyncEnumerable<LiveSessionResponse>` on a background thread with `MainThreadDispatcher`) with GeminiLiveClient's push-based event queue (`ConcurrentQueue<GeminiEvent>` with `ProcessEvents()` on the main thread).

The Firebase `Assets/Firebase/` directory contains 119 files across source code, DLLs, meta files, and Android m2repository artifacts. The `Assets/ExternalDependencyManager/` directory (30 files) is a Firebase companion and should also be considered for removal.

**Primary recommendation:** Execute in three ordered plans: (1) dependency swap and AIEmbodimentSettings creation, (2) PersonaSession rewire with threading model change, (3) SystemInstructionBuilder, FunctionRegistry, and ChirpTTSClient migration. Each plan must leave the project in a compilable state (or document exactly what breaks and why).

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Newtonsoft.Json | 13.0.1 via com.unity.nuget.newtonsoft-json 3.2.1 | JSON serialization | Already a dependency in package.json; replaces both Firebase and MiniJSON |
| System.Net.WebSockets.ClientWebSocket | .NET Standard 2.1 | WebSocket client | Used by GeminiLiveClient (Phase 7); built into Unity 6 |
| System.Collections.Concurrent.ConcurrentQueue | .NET Standard 2.1 | Thread-safe event dispatch | Used by GeminiLiveClient.ProcessEvents(); replaces MainThreadDispatcher for session events |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| UnityEngine.Resources | Unity 6 built-in | Load AIEmbodimentSettings at runtime | API key discovery via `Resources.Load<AIEmbodimentSettings>()` |
| UnityEditor (editor assembly only) | Unity 6 built-in | Custom inspectors | AIEmbodimentSettings password-masked field, settings asset creation |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AIEmbodimentSettings ScriptableObject via Resources.Load | Unity SettingsProvider (Project Settings panel) | SettingsProvider is editor-only; Resources.Load works at runtime. User decided on Resources.Load approach |
| ConcurrentQueue ProcessEvents polling | MainThreadDispatcher.Enqueue pattern | MainThreadDispatcher still needed for some callbacks but GeminiLiveClient's ProcessEvents is the primary dispatch mechanism now |

## Architecture Patterns

### Recommended Project Structure
```
Packages/com.google.ai-embodiment/
  Runtime/
    AIEmbodimentSettings.cs          # NEW: ScriptableObject singleton for API key
    PersonaSession.cs                # MODIFIED: GeminiLiveClient instead of LiveSession
    SystemInstructionBuilder.cs      # MODIFIED: Returns string, no ModelContent
    FunctionRegistry.cs              # MODIFIED: Stubs for Phase 10 (no FunctionDeclaration/Tool)
    FunctionCallContext.cs           # MODIFIED: Updated docs (MiniJSON -> Newtonsoft)
    ChirpTTSClient.cs               # MODIFIED: MiniJSON -> Newtonsoft.Json
    GeminiLiveClient.cs              # UNCHANGED (from Phase 7)
    GeminiEvent.cs                   # UNCHANGED (from Phase 7)
    GeminiLiveConfig.cs              # UNCHANGED (from Phase 7)
    MainThreadDispatcher.cs          # PRESERVED (still used by non-session callbacks)
    com.google.ai-embodiment.asmdef  # MODIFIED: Remove "Firebase.AI" reference
    ... (other files unchanged)
  Editor/
    AIEmbodimentSettingsEditor.cs    # NEW: Custom inspector with password mask
    PersonaConfigEditor.cs           # UNCHANGED (no Firebase references)
    com.google.ai-embodiment.editor.asmdef  # UNCHANGED (no Firebase references)
  Samples~/AyaLiveStream/
    AyaSampleController.cs           # MODIFIED: Stub Firebase types with TODO comments
    AyaLiveStream.asmdef             # MODIFIED: Remove "Firebase.AI" reference
```

### Pattern 1: AIEmbodimentSettings ScriptableObject Singleton
**What:** A project-wide ScriptableObject discovered via `Resources.Load` that holds the API key and other global settings.
**When to use:** PersonaSession.Connect() loads it automatically. Developers create it once via a menu item.
**Why:** Decided in CONTEXT.md. Follows the same pattern as Unity Addressables settings (project-wide singleton in Resources folder).

```csharp
// Source: Standard Unity ScriptableObject singleton pattern
[CreateAssetMenu(fileName = "AIEmbodimentSettings", menuName = "AI Embodiment/Settings")]
public class AIEmbodimentSettings : ScriptableObject
{
    private const string ResourcePath = "AIEmbodimentSettings";

    [SerializeField] private string _apiKey;

    public string ApiKey => _apiKey;

    private static AIEmbodimentSettings _instance;

    public static AIEmbodimentSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<AIEmbodimentSettings>(ResourcePath);
            }
            return _instance;
        }
    }
}
```

### Pattern 2: Event Queue Bridging (GeminiLiveClient -> PersonaSession)
**What:** PersonaSession subscribes to `GeminiLiveClient.OnEvent`, calls `ProcessEvents()` in Update(), and maps `GeminiEvent` types to public PersonaSession events.
**When to use:** The core integration pattern for this phase.
**Why:** Replaces the Firebase background-thread `ReceiveLoopAsync` + `MainThreadDispatcher.Enqueue` pattern with GeminiLiveClient's built-in `ConcurrentQueue` + `ProcessEvents()` model. Events arrive on the main thread naturally.

```csharp
// PersonaSession rewired pattern
private GeminiLiveClient _client;

private void Update()
{
    _client?.ProcessEvents();
}

private void HandleGeminiEvent(GeminiEvent ev)
{
    switch (ev.Type)
    {
        case GeminiEventType.Audio:
            HandleAudioEvent(ev);
            break;
        case GeminiEventType.OutputTranscription:
            HandleOutputTranscription(ev.Text);
            break;
        case GeminiEventType.InputTranscription:
            OnInputTranscription?.Invoke(ev.Text);
            break;
        case GeminiEventType.TurnComplete:
            HandleTurnComplete();
            break;
        case GeminiEventType.Interrupted:
            HandleInterrupted();
            break;
        case GeminiEventType.Connected:
            SetState(SessionState.Connected);
            break;
        case GeminiEventType.Error:
            HandleError(ev.Text);
            break;
        // ... etc
    }
}
```

### Pattern 3: Float-to-PCM Audio Conversion for SendAudio
**What:** AudioCapture provides `float[]` chunks. GeminiLiveClient.SendAudio expects `byte[]` (PCM16). PersonaSession must convert.
**When to use:** Every audio capture callback.
**Why:** Firebase's `SendAudioAsync(float[])` handled this internally. Now PersonaSession must do the conversion explicitly.

```csharp
// Float[] to PCM16 byte[] conversion
private static byte[] FloatToPcm16(float[] samples)
{
    byte[] pcm = new byte[samples.Length * 2];
    for (int i = 0; i < samples.Length; i++)
    {
        short s = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
        pcm[i * 2] = (byte)(s & 0xFF);
        pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
    }
    return pcm;
}
```

### Anti-Patterns to Avoid
- **Keeping MainThreadDispatcher as the primary dispatch mechanism:** GeminiLiveClient.ProcessEvents() already dispatches on the main thread via Update(). Using MainThreadDispatcher on top would add unnecessary indirection. MainThreadDispatcher is still needed for ChirpTTSClient error callbacks that come from UnityWebRequest internals.
- **Partial Firebase removal:** Leaving any Firebase.AI reference in runtime asmdef/source will cause compile errors when the `Assets/Firebase/` directory is deleted. Must be all-or-nothing per DEP-01.
- **Changing FunctionCallContext's RawArgs type:** FunctionCallContext takes `IReadOnlyDictionary<string, object>` and this interface stays compatible whether the underlying data comes from MiniJSON or Newtonsoft. Keep the same type.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| API key storage | Custom file-based config system | ScriptableObject in Resources folder | Standard Unity pattern, Inspector-friendly, version-control compatible (.gitignore the asset) |
| JSON serialization in ChirpTTS | Custom string concatenation | Newtonsoft.Json JObject or JsonConvert | MiniJSON being removed; Newtonsoft handles escaping, nulls, nested objects correctly |
| Float-to-PCM conversion | New utility class | Inline static method in PersonaSession | Firebase's LiveSession.ConvertTo16BitPCM shows the exact pattern (6 lines); not worth a separate class |
| Thread-safe event dispatch | Custom lock-based queue | GeminiLiveClient.ProcessEvents() (ConcurrentQueue internally) | Already built and tested in Phase 7 |
| Password masking in Inspector | Custom IMGUI char replacement | EditorGUILayout.PasswordField | Built-in Unity API, handles reveal toggle natively |

**Key insight:** The Firebase SDK's LiveSession was doing a LOT of work internally (WebSocket management, JSON parsing, audio encoding, type dispatch). GeminiLiveClient (Phase 7) now handles all of that. PersonaSession's job shrinks to: create config, create client, subscribe to events, map events to public API.

## Common Pitfalls

### Pitfall 1: Threading Model Change (Firebase Background Thread -> GeminiLiveClient Main Thread)
**What goes wrong:** Firebase's `ReceiveLoopAsync` ran on a background thread. ALL event callbacks used `MainThreadDispatcher.Enqueue(() => ...)`. GeminiLiveClient's `ProcessEvents()` runs on the main thread in Update(). If the new event handler still wraps everything in `MainThreadDispatcher.Enqueue`, events get delayed by an extra frame.
**Why it happens:** Copy-pasting the old ProcessResponse pattern without understanding the threading change.
**How to avoid:** In the new `HandleGeminiEvent` method, invoke events DIRECTLY (no MainThreadDispatcher wrapping) because ProcessEvents() already runs on the main thread. Remove all `MainThreadDispatcher.Enqueue` calls from session event routing. MainThreadDispatcher remains for non-session callbacks only (e.g., ChirpTTSClient.OnError).
**Warning signs:** Events arriving one frame late; double-dispatching visible in profiler.

### Pitfall 2: Firebase API Key vs AIEmbodimentSettings API Key
**What goes wrong:** The old code used `Firebase.FirebaseApp.DefaultInstance.Options.ApiKey` to get the API key (e.g., for ChirpTTSClient). After Firebase removal, this call does not exist.
**Why it happens:** ChirpTTSClient constructor takes an API key string. The old code sourced it from FirebaseApp.
**How to avoid:** Source the API key from `AIEmbodimentSettings.Instance.ApiKey` everywhere. Both GeminiLiveClient (via GeminiLiveConfig) and ChirpTTSClient get their API key from the same AIEmbodimentSettings singleton.
**Warning signs:** NullReferenceException when creating ChirpTTSClient during Connect().

### Pitfall 3: GeminiLiveClient.SendAudio Takes byte[], Not float[]
**What goes wrong:** Firebase's `LiveSession.SendAudioAsync(float[])` accepted float arrays directly (it converted internally via `ConvertTo16BitPCM`). GeminiLiveClient.SendAudio takes `byte[]` (PCM16 little-endian). Passing float[] directly causes a compile error; casting to byte[] causes garbled audio.
**Why it happens:** GeminiLiveClient is a lower-level API that works with raw wire format.
**How to avoid:** PersonaSession's `HandleAudioCaptured` must convert float[] to byte[] before calling `_client.SendAudio()`. Use the same conversion pattern Firebase used (clamp, multiply by 32767, cast to short, block-copy to bytes).
**Warning signs:** Compile error or garbled/silent audio output from the AI.

### Pitfall 4: FunctionRegistry.BuildTools() Returns Firebase Tool[] (Compilation Failure)
**What goes wrong:** FunctionRegistry.Register takes `FunctionDeclaration` (Firebase type) and BuildTools() returns `Tool[]` (Firebase type). After removing the Firebase.AI assembly reference, these types do not exist. The code won't compile.
**Why it happens:** Function calling was built on Firebase types in Phase 4.
**How to avoid:** Stub FunctionRegistry to use placeholder types. Since function calling is deferred to Phase 10:
- Remove `FunctionDeclaration` parameter from `Register()` (or replace with a temporary stub)
- Replace `Tool[] BuildTools()` with a method that returns tool JSON (JArray) or null
- AyaSampleController's `RegisterFunction` calls must be commented out with `// TODO: Phase 10` markers
**Warning signs:** Compile errors mentioning FunctionDeclaration, Tool, Schema.

### Pitfall 5: PersonaSession.RegisterFunction Public API Signature Change
**What goes wrong:** `RegisterFunction(string name, FunctionDeclaration declaration, FunctionHandler handler)` uses a Firebase type in its public signature. Removing it is a breaking API change. But keeping it requires keeping Firebase.
**Why it happens:** The function declaration type was Firebase-specific.
**How to avoid:** Phase 8 stubs this method. Two options:
- Option A: Comment out the `FunctionDeclaration` parameter, change to `RegisterFunction(string name, FunctionHandler handler)` temporarily
- Option B: Create a stub `FunctionDeclaration` placeholder in the AIEmbodiment namespace
- **Recommended: Option A** (simpler, Phase 10 will introduce the real replacement type). Mark with `// TODO: Phase 10 -- add function declaration parameter back with WebSocket-native type`

### Pitfall 6: ChirpTTSClient MiniJSON Dependency
**What goes wrong:** ChirpTTSClient uses `Google.MiniJSON.Json.Serialize` and `Json.Deserialize` for building/parsing TTS HTTP requests. MiniJSON comes from `Google.MiniJson.dll` in `Assets/Firebase/Plugins/`. After Firebase directory deletion, MiniJSON is gone.
**Why it happens:** MiniJSON was bundled with Firebase, not a separate dependency.
**How to avoid:** Replace `Json.Serialize` with `JObject.ToString()` (or `JsonConvert.SerializeObject`) and `Json.Deserialize` with `JObject.Parse`. The conversion is straightforward since both produce `Dictionary<string, object>` compatible structures.
**Warning signs:** Compile error on `using Google.MiniJSON;` after Firebase deletion.

### Pitfall 7: Sample Scene Compilation After Firebase Removal
**What goes wrong:** AyaSampleController.cs uses `FunctionDeclaration`, `Schema`, and `Dictionary<string, Schema>` from Firebase.AI. After removing Firebase.AI assembly reference, these types don't exist.
**Why it happens:** The sample scene was built using Firebase types for function registration.
**How to avoid:** Stub the Firebase type usages:
- Comment out RegisterFunctions() body with `// TODO: Phase 10 -- restore function registration with WebSocket-native types`
- Remove `using Firebase.AI;`
- Remove `"Firebase.AI"` from AyaLiveStream.asmdef references
- Both `Assets/AyaLiveStream/` AND `Packages/.../Samples~/AyaLiveStream/` must be updated (they are duplicates).

### Pitfall 8: Forgetting to Delete Assets/ExternalDependencyManager/
**What goes wrong:** The `Assets/ExternalDependencyManager/` directory (30 files) contains Google's dependency manager for Firebase. It may reference Firebase or cause issues after Firebase removal.
**Why it happens:** It is a separate directory from `Assets/Firebase/` and easy to overlook.
**How to avoid:** Include it in the Firebase purge. Search for all Firebase-adjacent directories.
**Warning signs:** Stale editor warnings or errors from dependency resolver looking for Firebase packages.

### Pitfall 9: ProcessEvents() Not Called in Update()
**What goes wrong:** GeminiLiveClient enqueues events into ConcurrentQueue but they never arrive at PersonaSession because nobody calls ProcessEvents().
**Why it happens:** Firebase's ReceiveLoopAsync was self-pumping (background thread). GeminiLiveClient requires explicit polling.
**How to avoid:** PersonaSession.Update() must call `_client?.ProcessEvents()`. This is the fundamental architectural change from push (Firebase) to pull (GeminiLiveClient).
**Warning signs:** Connection succeeds but no events fire. Session stays in Connecting state forever.

### Pitfall 10: Disconnect Lifecycle Mismatch
**What goes wrong:** Firebase LiveSession had `CloseAsync` (async) and `Dispose`. GeminiLiveClient has `Disconnect` (synchronous, blocks up to 2s) and `Dispose` (calls Disconnect). Using the wrong pattern causes deadlocks or resource leaks.
**Why it happens:** Different API contracts.
**How to avoid:** PersonaSession.Disconnect() should call `_client.Disconnect()` (synchronous) then null the reference. PersonaSession.OnDestroy() should call `_client?.Dispose()`. Do NOT await anything -- GeminiLiveClient.Disconnect() is synchronous.

## Code Examples

### Complete Firebase Type Reference Map

Every Firebase type reference in the runtime package and what replaces it:

```
FILE: PersonaSession.cs
  using Firebase.AI;                          -> REMOVE
  FirebaseAI.GetInstance(...)                  -> new GeminiLiveClient(config)
  LiveGenerationConfig(...)                    -> GeminiLiveConfig
  SpeechConfig.UsePrebuiltVoice(...)           -> config.VoiceName
  AudioTranscriptionConfig()                   -> (handled by GeminiLiveClient setup)
  LiveGenerativeModel                          -> (not needed, GeminiLiveClient handles setup)
  LiveSession _liveSession                     -> GeminiLiveClient _client
  _liveSession.ConnectAsync(...)               -> _client.ConnectAsync()
  _liveSession.SendAsync(ModelContent.Text(m)) -> _client.SendText(m)
  _liveSession.SendAudioAsync(float[], ct)     -> _client.SendAudio(FloatToPcm16(chunk))
  _liveSession.CloseAsync(...)                 -> _client.Disconnect()
  _liveSession.Dispose()                       -> _client.Dispose()
  ReceiveLoopAsync(session.ReceiveAsync)       -> _client.OnEvent + Update/ProcessEvents
  ProcessResponse(LiveSessionResponse)         -> HandleGeminiEvent(GeminiEvent)
  LiveSessionContent content                   -> (mapped via GeminiEventType)
  LiveSessionToolCall toolCall                 -> GeminiEventType.FunctionCall
  LiveSessionToolCallCancellation              -> (Phase 10)
  content.TurnComplete                         -> GeminiEventType.TurnComplete
  content.Interrupted                          -> GeminiEventType.Interrupted
  content.InputTranscription.HasValue          -> GeminiEventType.InputTranscription
  content.OutputTranscription.HasValue         -> GeminiEventType.OutputTranscription
  response.AudioAsFloat                        -> GeminiEvent.AudioData (already float[])
  ModelContent.FunctionResponse(...)           -> (Phase 10 -- stub/comment out)
  Firebase.FirebaseApp.DefaultInstance.Options.ApiKey -> AIEmbodimentSettings.Instance.ApiKey

FILE: SystemInstructionBuilder.cs
  using Firebase.AI;                           -> REMOVE
  ModelContent Build(config)                   -> string Build(config)  (return text only)
  ModelContent Build(config, goalManager)      -> string Build(config, goalManager)
  ModelContent.Text(text)                      -> text  (just return the string)

FILE: FunctionRegistry.cs
  using Firebase.AI;                           -> REMOVE
  FunctionDeclaration                          -> REMOVE parameter (Phase 10 adds replacement)
  Tool                                         -> REMOVE return type (Phase 10 adds replacement)
  Tool[] BuildTools()                          -> JArray BuildToolsJson() or null (Phase 10)
  new Tool(declarations)                       -> (Phase 10)

FILE: ChirpTTSClient.cs
  using Google.MiniJSON;                       -> using Newtonsoft.Json; using Newtonsoft.Json.Linq;
  Json.Serialize(requestBody)                  -> JObject(requestBody).ToString()
  Json.Deserialize(responseJson)               -> JObject.Parse(responseJson)

FILE: com.google.ai-embodiment.asmdef
  "Firebase.AI" in references                  -> REMOVE

FILE: AyaSampleController.cs (both locations)
  using Firebase.AI;                           -> REMOVE
  new FunctionDeclaration(...)                 -> // TODO: Phase 10
  new Dictionary<string, Schema>{...}          -> // TODO: Phase 10
  Schema.Enum(...)                             -> // TODO: Phase 10

FILE: AyaLiveStream.asmdef (both locations)
  "Firebase.AI" in references                  -> REMOVE
```

### AIEmbodimentSettings Implementation

```csharp
// Source: Standard Unity ScriptableObject singleton pattern
using UnityEngine;

namespace AIEmbodiment
{
    [CreateAssetMenu(fileName = "AIEmbodimentSettings", menuName = "AI Embodiment/Settings")]
    public class AIEmbodimentSettings : ScriptableObject
    {
        private const string ResourcePath = "AIEmbodimentSettings";

        [SerializeField] private string _apiKey = "";

        /// <summary>Google AI API key for Gemini Live and Cloud TTS.</summary>
        public string ApiKey => _apiKey;

        private static AIEmbodimentSettings _instance;

        /// <summary>
        /// Loads the singleton settings asset from Resources.
        /// Returns null if no asset exists (caller should log a helpful error).
        /// </summary>
        public static AIEmbodimentSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<AIEmbodimentSettings>(ResourcePath);
                }
                return _instance;
            }
        }
    }
}
```

### AIEmbodimentSettings Editor (Password Masking)

```csharp
// Source: Unity EditorGUILayout.PasswordField pattern
using UnityEditor;
using UnityEngine;

namespace AIEmbodiment.Editor
{
    [CustomEditor(typeof(AIEmbodimentSettings))]
    public class AIEmbodimentSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty _apiKey;
        private bool _showApiKey;

        void OnEnable()
        {
            _apiKey = serializedObject.FindProperty("_apiKey");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("API Key", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (_showApiKey)
            {
                EditorGUILayout.PropertyField(_apiKey, new GUIContent("API Key"));
            }
            else
            {
                _apiKey.stringValue = EditorGUILayout.PasswordField(
                    new GUIContent("API Key"), _apiKey.stringValue);
            }
            if (GUILayout.Button(_showApiKey ? "Hide" : "Show", GUILayout.Width(50)))
            {
                _showApiKey = !_showApiKey;
            }
            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrEmpty(_apiKey.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "API key is required. Get one from Google AI Studio: https://aistudio.google.com/apikey",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
```

### PersonaSession.Connect() Rewired

```csharp
// Core Connect() replacement pattern
public async void Connect()
{
    try
    {
        if (State != SessionState.Disconnected) return;
        if (_config == null) { /* log error */ return; }

        var settings = AIEmbodimentSettings.Instance;
        if (settings == null || string.IsNullOrEmpty(settings.ApiKey))
        {
            Debug.LogError(
                "PersonaSession: No API key configured. " +
                "Create an AIEmbodimentSettings asset: " +
                "Assets > Create > AI Embodiment > Settings, " +
                "place it in a Resources folder, and set the API key.");
            return;
        }

        SetState(SessionState.Connecting);
        _sessionCts = new CancellationTokenSource();

        var systemInstruction = SystemInstructionBuilder.Build(_config, _goalManager);

        var liveConfig = new GeminiLiveConfig
        {
            ApiKey = settings.ApiKey,
            Model = _config.modelName,
            SystemInstruction = systemInstruction,
            VoiceName = _config.geminiVoiceName
        };

        _client = new GeminiLiveClient(liveConfig);
        _client.OnEvent += HandleGeminiEvent;

        await _client.ConnectAsync();

        // Initialize audio and PacketAssembler
        _packetAssembler = new PacketAssembler();
        _packetAssembler.SetPacketCallback(HandleSyncPacket);

        _audioPlayback?.Initialize();

        // Initialize Chirp TTS when backend is ChirpTTS
        if (_config.voiceBackend == VoiceBackend.ChirpTTS)
        {
            _chirpClient = new ChirpTTSClient(settings.ApiKey);
            _chirpClient.OnError += HandleChirpError;
        }

        // NOTE: SetState(Connected) happens in HandleGeminiEvent
        // when GeminiEventType.Connected arrives (setupComplete acknowledged)
    }
    catch (Exception ex)
    {
        SetState(SessionState.Error);
        OnError?.Invoke(ex);
    }
}
```

### ChirpTTSClient MiniJSON -> Newtonsoft Migration

```csharp
// Before (MiniJSON):
using Google.MiniJSON;
string json = Json.Serialize(requestBody);              // Dictionary -> JSON string
var response = Json.Deserialize(responseJson) as Dictionary<string, object>;

// After (Newtonsoft):
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
string json = JObject.FromObject(requestBody).ToString(Formatting.None);
// OR build JObject directly:
var obj = new JObject
{
    ["input"] = new JObject { ["ssml"] = $"<speak>{text}</speak>" },
    ["voice"] = new JObject { ["languageCode"] = languageCode, ["name"] = voiceName },
    ["audioConfig"] = new JObject { ["audioEncoding"] = "LINEAR16", ["sampleRateHertz"] = SAMPLE_RATE }
};
string json = obj.ToString(Formatting.None);

// Response parsing:
var response = JObject.Parse(responseJson);
string audioBase64 = response["audioContent"]?.ToString();
```

### FunctionRegistry Stub (Phase 10 Deferred)

```csharp
// FunctionRegistry after Phase 8 (stubbed for Phase 10)
public class FunctionRegistry
{
    private readonly Dictionary<string, FunctionHandler> _handlers = new();
    private readonly HashSet<string> _cancelledIds = new();
    private bool _frozen;

    public bool HasRegistrations => _handlers.Count > 0;

    // TODO: Phase 10 -- add function declaration parameter back
    // with WebSocket-native type (JObject schema)
    public void Register(string name, FunctionHandler handler)
    {
        if (_frozen) throw new InvalidOperationException("...");
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("...");
        _handlers[name] = handler;
    }

    // TODO: Phase 10 -- BuildToolsJson() returns JArray of tool declarations
    public void Freeze() { _frozen = true; }

    public bool TryGetHandler(string name, out FunctionHandler handler)
        => _handlers.TryGetValue(name, out handler);

    public void MarkCancelled(string callId) => _cancelledIds.Add(callId);
    public bool IsCancelled(string callId) => _cancelledIds.Remove(callId);
}
```

## GeminiEvent to PersonaSession Event Mapping

Complete mapping from GeminiLiveClient events to PersonaSession public API:

| GeminiEventType | PersonaSession Event(s) | Additional Logic |
|----------------|------------------------|------------------|
| Connected | OnStateChanged(Connected) | Set SessionState.Connected |
| Audio | OnAISpeakingStarted (first chunk), audio to AudioPlayback.EnqueueAudio, audio to PacketAssembler.AddAudio | Track _aiSpeaking, _turnStarted; only route audio when VoiceBackend.GeminiNative |
| OutputTranscription | OnOutputTranscription(text), OnTextReceived(text) | Route to PacketAssembler.AddTranscription; Chirp text buffer accumulation |
| InputTranscription | OnInputTranscription(text) | Direct passthrough |
| TurnComplete | OnAISpeakingStopped (if speaking), OnTurnComplete | Reset _aiSpeaking, _turnStarted; PacketAssembler.FinishTurn(); Chirp full-response synthesis |
| Interrupted | OnAISpeakingStopped (if speaking), OnInterrupted | AudioPlayback.ClearBuffer(); PacketAssembler.CancelTurn(); clear Chirp buffer |
| FunctionCall | (through PacketAssembler -> OnSyncPacket) | PacketAssembler.AddFunctionCall(name, args, id) -- args need parsing from JSON string |
| Disconnected | OnStateChanged(Disconnected) | Only if state was Connected |
| Error | OnError(new Exception(text)), OnStateChanged(Error) | |

## Files to Delete (Firebase Purge)

| Path | File Count | Reason |
|------|-----------|--------|
| `Assets/Firebase/` (entire directory) | 119 files | Firebase SDK source, DLLs, Android artifacts, meta files |
| `Assets/ExternalDependencyManager/` (entire directory) | 30 files | Firebase companion dependency manager |

**Total files to delete:** ~149 files

**Verification after deletion:** The project must compile. Any stale `.meta` files will cause warnings but not errors. Unity will auto-clean orphaned meta references on reimport.

## GeminiLiveConfig Additions Needed

GeminiLiveConfig (from Phase 7) currently accepts: ApiKey, Model, SystemInstruction, VoiceName, AudioInputSampleRate, AudioOutputSampleRate.

For Phase 8 integration, GeminiLiveConfig may need to accept tools JSON (JArray) for the setup handshake. However, since function calling is deferred to Phase 10, this is not needed now. GeminiLiveClient's `SendSetupMessage()` already has a placeholder comment for tools.

No changes to GeminiLiveConfig are needed for Phase 8.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Firebase LiveSession (background ReceiveAsync) | GeminiLiveClient (ConcurrentQueue + ProcessEvents polling) | Phase 7/8 | All event routing changes from push-to-main-thread to pull-from-queue |
| Firebase.FirebaseApp.DefaultInstance.Options.ApiKey | AIEmbodimentSettings.Instance.ApiKey | Phase 8 | Cleaner, no Firebase dependency, Inspector-visible |
| MiniJSON (Json.Serialize/Deserialize) | Newtonsoft.Json (JObject/JArray) | Phase 7/8 | Better null handling, proper escaping, typed parsing |
| ModelContent.Text(string) for system instructions | Plain string (GeminiLiveConfig.SystemInstruction) | Phase 8 | Simpler, no wrapper type needed |
| FunctionDeclaration + Tool Firebase types | Deferred to Phase 10 (JObject-based) | Phase 8 stubs | Function registration API temporarily simplified |

**Deprecated/outdated:**
- `Firebase.AI` assembly and all types within: Being removed in Phase 8
- `Google.MiniJSON`: Being removed with Firebase (ChirpTTSClient migrates to Newtonsoft)
- `MainThreadDispatcher` for session event routing: Replaced by ProcessEvents() pattern (but MainThreadDispatcher stays for non-session callbacks)

## Open Questions

1. **GeminiEvent.FunctionArgsJson is a string, but PacketAssembler.AddFunctionCall takes IReadOnlyDictionary<string, object>**
   - What we know: GeminiLiveClient stores function args as a JSON string. PacketAssembler and FunctionCallContext expect a dictionary.
   - What's unclear: Where to parse the JSON string to a dictionary.
   - Recommendation: Parse in the event handler: `JObject.Parse(ev.FunctionArgsJson).ToObject<Dictionary<string, object>>()`. This keeps GeminiLiveClient simple (string) and provides the typed dictionary that PacketAssembler expects.

2. **Should MainThreadDispatcher be removed entirely?**
   - What we know: GeminiLiveClient's ProcessEvents() handles the main session event loop. MainThreadDispatcher is still used for ChirpTTSClient.OnError.
   - What's unclear: Whether ChirpTTS errors need MainThreadDispatcher if they already fire from UnityWebRequest (which runs on main thread).
   - Recommendation: Keep MainThreadDispatcher for now. It is lightweight, well-tested, and provides a safety net. Phase 9 (TTS Abstraction) can evaluate whether to remove it.

3. **ExternalDependencyManager -- safe to delete?**
   - What we know: It is 30 files of Google's dependency management for Firebase. It has editor DLLs that may reference Firebase.
   - What's unclear: Whether it provides value to any non-Firebase packages.
   - Recommendation: Delete it. It was installed solely for Firebase. If it causes issues, the deletion is easily reversible via git.

## Sources

### Primary (HIGH confidence)
- Codebase analysis: All source files in `Packages/com.google.ai-embodiment/Runtime/` and `Assets/` read directly
- Phase 7 research: `.planning/phases/07-websocket-transport-and-audio-parsing/07-RESEARCH.md`
- GeminiLiveClient implementation: `Packages/com.google.ai-embodiment/Runtime/GeminiLiveClient.cs` (Phase 7 output)
- Firebase AI SDK source: `Assets/Firebase/FirebaseAI/LiveSession.cs`, `LiveSessionResponse.cs`, `ModelContent.cs`, `FunctionCalling.cs`, `Schema.cs`
- [Unity ScriptableObject Singleton via Resources.Load](https://discussions.unity.com/t/load-scriptableobject-as-singleton-from-resources-folder-tuto-questions/675536) - Community pattern
- [Unity EditorGUILayout.PasswordField API](https://docs.unity3d.com/ScriptReference/EditorGUILayout.PasswordField.html)

### Secondary (MEDIUM confidence)
- [Unity ScriptableSingleton API reference](https://docs.unity3d.com/2020.1/Documentation/ScriptReference/ScriptableSingleton_1.html) - Editor-only, not used but referenced for comparison
- Phase 8 CONTEXT.md decisions (user-locked choices)

### Tertiary (LOW confidence)
- ExternalDependencyManager safety to delete: Based on codebase observation that it was installed with Firebase. Not independently verified.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries already in use (Newtonsoft.Json in package.json, ClientWebSocket in GeminiLiveClient)
- Architecture: HIGH - Complete codebase read of both source (Firebase) and target (GeminiLiveClient) implementations
- Event mapping: HIGH - Every Firebase event type traced through code to corresponding GeminiEventType
- Pitfalls: HIGH - Derived from direct code comparison between old and new patterns
- Firebase purge scope: HIGH - File counts verified via filesystem listing

**Research date:** 2026-02-13
**Valid until:** 2026-03-15 (30 days -- internal migration, stable codebase)
