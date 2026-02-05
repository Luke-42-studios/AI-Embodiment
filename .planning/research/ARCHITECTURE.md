# Architecture Patterns

**Domain:** Unity UPM package for real-time AI conversation with game characters
**Researched:** 2026-02-05
**Overall confidence:** HIGH (based on direct source code reading of Firebase AI Logic SDK 13.7.0 and Unity 6 conventions)

## Recommended Architecture

The system is a pipeline with five stages: **Capture**, **Transport**, **Processing**, **Assembly**, and **Presentation**. Each stage has a single owner component with well-defined inputs and outputs.

```
                              MAIN THREAD                          BACKGROUND THREADS
                    +---------------------------+          +---------------------------+
                    |                           |          |                           |
                    |  [AudioCapture]            |   PCM    |                           |
 Microphone ------->  Unity Microphone API     ----------->  LiveSession               |
                    |  16kHz mono float[]       |  (send)  |  (WebSocket to Gemini)    |
                    |                           |          |                           |
                    +---------------------------+          |  [VoiceBackend]            |
                                                           |  Gemini Audio path:       |
                    +---------------------------+   recv   |    audio bytes from WS    |
                    |                           | <--------|  Chirp TTS path:          |
                    |  [MainThreadDispatcher]    |          |    text -> HTTP POST ->   |
                    |  ConcurrentQueue<Action>  |          |    PCM bytes back         |
                    |  drained every Update()   |          +---------------------------+
                    |                           |
                    +------------+--------------+
                                 |
                                 v
                    +---------------------------+
                    |  [PacketAssembler]         |
                    |  Correlates:               |
                    |   - text chunks            |
                    |   - audio PCM data         |
                    |   - function calls/emotes  |
                    |  Emits: PersonaPacket      |
                    +------------+--------------+
                                 |
                                 v
                    +---------------------------+
                    |  [AudioPlayback]           |
                    |  AudioClip from PCM        |
                    |  AudioSource.PlayOneShot   |
                    |                           |
                    |  [FunctionCallHandler]     |
                    |  C# delegates invoked     |
                    |  on main thread            |
                    +---------------------------+
```

### Component Boundaries

| Component | Responsibility | Inputs | Outputs | Thread |
|-----------|---------------|--------|---------|--------|
| **PersonaConfig** | ScriptableObject holding persona definition (personality, voice, model, tools) | Developer edits in Inspector | Read by PersonaSession at connect time | N/A (data) |
| **PersonaSession** | MonoBehaviour lifecycle owner; connects/disconnects LiveSession; owns the receive loop | PersonaConfig, GameObject with AudioSource | Events to subscribers (OnTextReceived, OnAudioReceived, OnFunctionCall, OnTurnComplete, OnError) | Main thread (MonoBehaviour) |
| **AudioCapture** | Reads Unity Microphone into float[] chunks, converts to PCM, sends to LiveSession | Microphone device name, sample rate | float[] chunks pushed to LiveSession.SendAudioAsync | Main thread (reads Microphone in Update/Coroutine) |
| **AudioPlayback** | Receives PCM audio, creates AudioClip, schedules playback on AudioSource | byte[] PCM data (16-bit, 24kHz), AudioSource reference | Audio heard by player; playback timing events | Main thread |
| **ChirpTTSClient** | HTTP POST to Cloud TTS v1 API, returns PCM audio from text | Text string, voice config, API key | byte[] PCM audio | Background thread (HttpClient async) |
| **PacketAssembler** | Correlates text, audio, and function call data from a single model turn into ordered packets | Streamed LiveSessionResponse chunks | PersonaPacket structs emitted in order | Main thread (after dispatch) |
| **FunctionCallHandler** | Registry of C# delegates keyed by function name; dispatches LiveSessionToolCall to registered handlers | LiveSessionToolCall from Firebase SDK | FunctionResponsePart returned to LiveSession; side effects via delegates | Main thread |
| **MainThreadDispatcher** | Marshals callbacks from background WebSocket thread to Unity main thread | Actions enqueued from any thread | Actions dequeued and invoked in Update() | Main thread (consumer), any thread (producer) |
| **SystemInstructionBuilder** | Generates system instruction ModelContent from PersonaConfig fields | PersonaConfig (archetype, traits, backstory, speech patterns, available functions) | ModelContent with role "system" | N/A (pure function) |

### Key Data Types

```
PersonaConfig (ScriptableObject)
  - displayName: string
  - archetype: string (e.g., "merchant", "guide", "companion")
  - personalityTraits: string[]
  - backstory: string
  - speechPatterns: string
  - voiceBackend: enum { GeminiNative, ChirpTTS }
  - geminiVoice: string (e.g., "Aoede", "Puck", "Kore", "Charon", "Fenrir")
  - chirpVoice: string (e.g., "en-US-Chirp3-HD-Achernar")
  - chirpApiKey: string
  - modelName: string (default: "gemini-2.0-flash-live-001")
  - temperature: float
  - functionDeclarations: FunctionDeclarationConfig[]
  - safetySettings: SafetySettingConfig[]

PersonaPacket (readonly struct)
  - text: string (accumulated text so far)
  - audioClip: AudioClip (nullable, populated when audio data arrives)
  - functionCalls: FunctionCallPart[] (nullable, populated when tool calls arrive)
  - isTurnComplete: bool
  - isInterrupted: bool
  - inputTranscription: string (nullable)
  - outputTranscription: string (nullable)

SessionState (enum)
  - Disconnected
  - Connecting
  - Connected
  - Reconnecting
  - Error
```

## Data Flow

### Path 1: Gemini Native Audio (Preferred)

This path uses Gemini's built-in voice synthesis. Audio comes directly from the WebSocket alongside text.

```
1. AudioCapture.Update()
   - Microphone.GetData() -> float[] samples
   - Downsample if needed (Microphone may not support 16kHz directly)
   - session.SendAudioAsync(float[]) [enqueues to WebSocket, async]

2. PersonaSession receive loop (background thread via ReceiveAsync)
   - await foreach (var response in session.ReceiveAsync(ct))
   - For each LiveSessionResponse:
     a. response.Message is LiveSessionContent:
        - response.Text -> enqueue text to main thread
        - response.AudioAsFloat -> enqueue audio float[] to main thread
        - content.InputTranscription -> enqueue to main thread
        - content.OutputTranscription -> enqueue to main thread
        - content.TurnComplete -> enqueue turn-complete signal
        - content.Interrupted -> enqueue interruption signal
     b. response.Message is LiveSessionToolCall:
        - enqueue function calls to main thread
     c. response.Message is LiveSessionToolCallCancellation:
        - enqueue cancellation to main thread

3. MainThreadDispatcher.Update() drains queue
   - Calls into PacketAssembler with each chunk

4. PacketAssembler accumulates chunks into PersonaPacket
   - Fires events: OnPacketReady(PersonaPacket)

5. AudioPlayback receives audio data
   - Creates AudioClip from float[] (24kHz sample rate for Gemini native)
   - Schedules on AudioSource via PlayOneShot or streaming clip

6. FunctionCallHandler receives function calls
   - Looks up registered delegate by name
   - Invokes delegate with args dictionary
   - Returns FunctionResponsePart to LiveSession (marshaled back to background)
```

### Path 2: Chirp 3 HD TTS (Custom Voices)

This path requests only text from Gemini, then synthesizes speech via a separate HTTP call to Cloud TTS.

```
1. AudioCapture -> same as Path 1

2. PersonaSession receive loop (same background thread)
   - LiveGenerationConfig uses ResponseModality.Text only (no Audio)
   - response.Text -> accumulate text chunks
   - On TurnComplete or sentence boundary:
     a. Enqueue text to main thread for display
     b. Send accumulated text to ChirpTTSClient

3. ChirpTTSClient.SynthesizeAsync(text, voiceConfig)
   - Background thread: HTTP POST to https://texttospeech.googleapis.com/v1/text:synthesize
   - Request body: { input: { text }, voice: { name, languageCode }, audioConfig: { audioEncoding: LINEAR16, sampleRateHertz: 24000 } }
   - Response: { audioContent: base64-encoded PCM }
   - Decode base64 -> byte[]
   - Enqueue audio to main thread

4. MainThreadDispatcher.Update() -> PacketAssembler -> AudioPlayback
   (same as Path 1 from step 4 onward)
```

### Function Call Round-Trip

```
1. Gemini sends LiveSessionToolCall with FunctionCallPart[]
   - Each FunctionCallPart has: Name, Args (Dictionary<string, object>), Id

2. MainThreadDispatcher enqueues to main thread

3. FunctionCallHandler.Dispatch(FunctionCallPart call)
   - Looks up _handlers[call.Name]
   - If found: invokes delegate, gets Dictionary<string, object> result
   - If not found: returns error response

4. Build FunctionResponsePart(call.Name, result, call.Id)

5. Marshal back to background thread:
   - session.SendAsync(ModelContent.FunctionResponse(name, result, id))
```

## UPM Package Structure

Based on Unity's custom package layout conventions (verified against Unity 6 documentation and existing UPM packages).

```
com.luke42studios.ai-embodiment/
  package.json                          # Package manifest (name, version, dependencies)
  README.md                             # Package documentation shown in Package Manager
  LICENSE.md                            # License file
  CHANGELOG.md                          # Version history
  Runtime/
    com.luke42studios.ai-embodiment.asmdef  # Runtime assembly definition
    PersonaConfig.cs                    # ScriptableObject (persona definition)
    PersonaSession.cs                   # MonoBehaviour (session lifecycle)
    AudioCapture.cs                     # Microphone capture component
    AudioPlayback.cs                    # AudioSource playback component
    ChirpTTSClient.cs                   # Cloud TTS HTTP client
    PacketAssembler.cs                  # Correlates streamed chunks
    FunctionCallHandler.cs              # Delegate registry for function calls
    MainThreadDispatcher.cs             # Thread marshaling utility
    SystemInstructionBuilder.cs         # Builds system prompt from config
    PersonaPacket.cs                    # Data struct for assembled output
    SessionState.cs                     # Enum for connection state
    Internal/
      AudioConverter.cs                 # PCM <-> float[] utilities with buffer pooling
      SentenceBoundaryDetector.cs       # Text chunking for TTS path
  Editor/
    com.luke42studios.ai-embodiment.editor.asmdef  # Editor assembly definition
    PersonaConfigEditor.cs              # Custom Inspector for PersonaConfig
    PersonaSessionEditor.cs             # Custom Inspector with connect/test buttons
  Tests/
    Runtime/
      com.luke42studios.ai-embodiment.tests.asmdef
      PersonaConfigTests.cs
      PacketAssemblerTests.cs
      AudioConverterTests.cs
      SystemInstructionBuilderTests.cs
    Editor/
      com.luke42studios.ai-embodiment.editor.tests.asmdef
  Samples~/
    BasicConversation/
      BasicConversation.unity           # Scene with one persona talking
      SamplePersona.asset               # Example PersonaConfig
      SampleFunctionHandler.cs          # Example function call handler
    AnimatedCharacter/
      AnimatedCharacter.unity           # Scene with emote-driven animations
      EmoteHandler.cs                   # Emote function -> Animator triggers
      SampleAnimatedPersona.asset
  Documentation~/
    index.md                            # Package documentation
    getting-started.md
    voice-configuration.md
    function-calling.md
```

### Assembly Definition Requirements

**Runtime asmdef** (`com.luke42studios.ai-embodiment.asmdef`):
```json
{
  "name": "com.luke42studios.ai-embodiment",
  "rootNamespace": "AIEmbodiment",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": []
}
```

**Critical note on Firebase dependency:** The Firebase AI Logic SDK ships as raw `.cs` files under `Assets/Firebase/FirebaseAI/` without an assembly definition. This means it compiles into `Assembly-CSharp` by default. The UPM package's runtime asmdef cannot directly reference `Assembly-CSharp`. Two solutions:

1. **Recommended:** Add an asmdef to the Firebase AI source files (`Assets/Firebase/FirebaseAI/Firebase.AI.asmdef`) and reference it from the package's asmdef. This keeps Firebase as a separately compiled assembly.
2. **Alternative:** Use `Assembly-CSharp` references from the package asmdef (less clean, but works if Firebase SDK cannot be modified).

The `package.json` should list Firebase AI Logic as a dependency that devs install separately:
```json
{
  "name": "com.luke42studios.ai-embodiment",
  "version": "0.1.0",
  "displayName": "AI Embodiment",
  "description": "AI-powered game characters with real-time conversation",
  "unity": "6000.0",
  "dependencies": {}
}
```

Firebase is NOT listed in `dependencies` because it is not a UPM package -- it is installed via the Firebase Unity SDK `.unitypackage` or tarball. The package should document this prerequisite and fail gracefully with a clear error if Firebase.AI types are not found.

### Samples~ Directory Convention

The `~` suffix in `Samples~` is mandatory. Unity ignores directories ending with `~` during import, which means:
- Sample assets are not compiled or imported automatically
- Users import samples via Package Manager UI ("Import" button)
- Each sample subfolder becomes a separate importable sample
- Samples are copied into `Assets/Samples/AI Embodiment/{version}/{SampleName}/`

## Patterns to Follow

### Pattern 1: Thread Marshaling via ConcurrentQueue

The Firebase `LiveSession.ReceiveAsync()` runs on a background thread (the WebSocket receive loop uses `ClientWebSocket` which is not Unity main-thread-bound). All Unity API calls (AudioSource, AudioClip, MonoBehaviour, Transform) must happen on the main thread.

**What:** A singleton-style dispatcher that drains a ConcurrentQueue in Update().

**When:** Every time data arrives from the LiveSession receive loop or ChirpTTSClient HTTP response.

**Implementation approach:**
```csharp
public class MainThreadDispatcher : MonoBehaviour
{
  private static readonly ConcurrentQueue<Action> _queue = new();

  public static void Enqueue(Action action)
  {
    _queue.Enqueue(action);
  }

  private void Update()
  {
    while (_queue.TryDequeue(out Action action))
    {
      action.Invoke();
    }
  }
}
```

**Why not SynchronizationContext:** Unity's `UnitySynchronizationContext` exists but has known issues with heavy load (posts to a queue that is drained once per frame, same as above, but with more overhead). A simple ConcurrentQueue is more predictable, has lower allocation overhead, and gives explicit control over drain timing.

**Why not Coroutines:** The receive loop is `IAsyncEnumerable`, not a Unity coroutine. Bridging async/await to coroutines adds complexity without benefit.

### Pattern 2: ScriptableObject Configuration

**What:** All persona configuration lives in a ScriptableObject asset.

**When:** Developers define character personalities, voice settings, model parameters.

**Why:**
- Inspector-editable without custom editor code (basic fields just work)
- Serializable to disk as `.asset` files
- Referenceable from any MonoBehaviour via serialized field
- Supports multiple personas as separate assets
- Can be swapped at runtime by assigning a different config to PersonaSession

**Key decision:** PersonaConfig is read-only at runtime. Voice backend and model selection are set at connect time (when `LiveGenerativeModel.ConnectAsync()` is called with the config). Changing persona mid-session requires disconnecting and reconnecting.

### Pattern 3: Event-Driven Output via C# Delegates

**What:** PersonaSession exposes events as `Action<T>` delegates, not UnityEvents.

**When:** Consumers need to react to AI responses (text, audio, function calls, state changes).

**Implementation approach:**
```csharp
public class PersonaSession : MonoBehaviour
{
  public event Action<string> OnTextReceived;
  public event Action<AudioClip> OnAudioReady;
  public event Action<PersonaPacket> OnPacketReady;
  public event Action<FunctionCallPart> OnFunctionCall;
  public event Action OnTurnComplete;
  public event Action<bool> OnInterrupted;
  public event Action<SessionState> OnStateChanged;
  public event Action<Exception> OnError;
}
```

**Why delegates over UnityEvents:**
- Type-safe (compiler catches mismatches)
- No Inspector serialization overhead for high-frequency audio events
- Composable (multiple subscribers, lambda-friendly)
- Better for library code (UnityEvents are designed for scene-level wiring)

**Why not interfaces:** Delegates are more flexible for this use case. A single consumer may want to subscribe to some events but not others. Interface implementations require all methods.

### Pattern 4: Buffer Pooling for Audio

**What:** Reuse byte[] and float[] arrays for audio data instead of allocating new ones each frame.

**When:** Every audio chunk received from LiveSession (potentially 30-60 times per second).

**Why:** The Firebase SDK's `ConvertBytesToFloat` and `ConvertTo16BitPCM` allocate new arrays on every call (verified in `LiveSession.cs:252-268` and `LiveSessionResponse.cs:91-103`). This creates GC pressure that causes frame hitches in real-time audio playback.

**Implementation approach:**
```csharp
internal static class AudioConverter
{
  private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;
  private static readonly ArrayPool<float> FloatPool = ArrayPool<float>.Shared;

  public static float[] BytesToFloat(byte[] pcmBytes, out int sampleCount)
  {
    sampleCount = pcmBytes.Length / 2;
    float[] buffer = FloatPool.Rent(sampleCount);
    for (int i = 0; i < sampleCount; i++)
    {
      buffer[i] = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8)) / 32768f;
    }
    return buffer; // Caller must return to pool
  }
}
```

### Pattern 5: Receive Loop with CancellationToken

**What:** The PersonaSession runs a continuous receive loop as a background Task, controlled by a CancellationTokenSource tied to the MonoBehaviour lifecycle.

**When:** For the entire duration of a connected session.

**Why:** `LiveSession.ReceiveAsync()` yields `IAsyncEnumerable<LiveSessionResponse>` that terminates on `TurnComplete`. But for a conversation, you need to call `ReceiveAsync()` again after each turn. The outer loop handles this.

**Implementation approach:**
```csharp
private async Task ReceiveLoopAsync(LiveSession session, CancellationToken ct)
{
  while (!ct.IsCancellationRequested)
  {
    try
    {
      await foreach (var response in session.ReceiveAsync(ct))
      {
        ProcessResponse(response); // Enqueues to main thread
      }
      // ReceiveAsync completed (TurnComplete received)
      // Loop back to receive the next turn
    }
    catch (OperationCanceledException) { break; }
    catch (WebSocketException ex)
    {
      MainThreadDispatcher.Enqueue(() => OnError?.Invoke(ex));
      break;
    }
  }
}
```

**Lifecycle binding:**
```csharp
private CancellationTokenSource _sessionCts;

private void OnDestroy()
{
  _sessionCts?.Cancel();
  _sessionCts?.Dispose();
  _liveSession?.Dispose();
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Calling Unity API from Background Thread

**What:** Accessing any MonoBehaviour, Transform, AudioSource, AudioClip, or other Unity object from the WebSocket receive thread.

**Why bad:** Unity is single-threaded. Accessing Unity objects from non-main threads causes crashes, undefined behavior, or silent data corruption. The crash may not happen immediately -- it may corrupt memory and crash later in an unrelated location.

**Instead:** Always marshal to main thread via MainThreadDispatcher before touching any Unity object. The `LiveSession.ReceiveAsync()` loop runs on a thread pool thread (verified: `ClientWebSocket.ReceiveAsync` uses the .NET thread pool). Every piece of data from this loop must be enqueued to the main thread.

### Anti-Pattern 2: Creating AudioClip Per Frame

**What:** Calling `AudioClip.Create()` for every received audio chunk.

**Why bad:** AudioClip creation is expensive (allocates unmanaged memory, registers with audio system). At 30+ chunks per second, this will cause frame drops and memory fragmentation.

**Instead:** Use a ring-buffer approach:
1. Pre-allocate a single AudioClip with sufficient duration (e.g., 10 seconds)
2. Write incoming PCM data into the clip via `AudioClip.SetData()` at a write cursor
3. Play with `AudioSource.Play()` and let the read cursor chase the write cursor
4. Or use `OnAudioFilterRead` for direct PCM injection (most control, but requires careful timing)

### Anti-Pattern 3: Sending Full Audio Buffer Every Frame

**What:** Reading all available Microphone data every Update() and sending the entire buffer.

**Why bad:** Unity's `Microphone.GetData()` returns accumulated samples since last read. If Update() runs at 60fps and microphone is at 16kHz, each chunk is ~267 samples (~0.5KB). But if a frame spike occurs, the next read catches up and may send a large burst. Meanwhile, the WebSocket has a send lock (`SemaphoreSlim` in `LiveSession.cs:38`), so rapid small sends queue up.

**Instead:** Read from Microphone at a fixed interval (~100ms chunks = 1600 samples at 16kHz). Use a ring buffer to track the read position. Send chunks of consistent size. This matches Gemini's expected input pattern and reduces WebSocket send contention.

### Anti-Pattern 4: Blocking Async in MonoBehaviour Methods

**What:** Using `.Result` or `.Wait()` on Tasks in Start(), Update(), or other MonoBehaviour methods.

**Why bad:** Blocks the main thread. Unity becomes unresponsive. Can also deadlock if the Task tries to marshal back to the main thread (which is blocked).

**Instead:** Use `async void` only for Unity lifecycle entry points (Start, event handlers). Use `async Task` for everything else. Fire-and-forget with error handling:
```csharp
private async void Start()
{
  try
  {
    await ConnectAsync();
  }
  catch (Exception ex)
  {
    Debug.LogError($"Connection failed: {ex.Message}");
    OnError?.Invoke(ex);
  }
}
```

### Anti-Pattern 5: Tight Coupling to Firebase SDK Types

**What:** Passing `LiveSessionResponse`, `LiveSessionContent`, `FunctionCallPart` directly to game code.

**Why bad:** The Firebase AI Logic SDK is in **Public Preview** (explicitly stated in `LiveGenerativeModel.cs:37-40`). It can change in backwards-incompatible ways without notice. If game code depends directly on SDK types, every SDK update potentially breaks all consumers.

**Instead:** Define package-owned data types (`PersonaPacket`, `PersonaFunctionCall`) and convert from Firebase types at the boundary. Only `PersonaSession` and `FunctionCallHandler` touch Firebase types directly. Everything downstream works with package-owned types.

## Threading Model

### Thread Map

| Thread | What Runs | Can Touch Unity API? |
|--------|-----------|---------------------|
| **Main Thread** | MonoBehaviour lifecycle (Start, Update, OnDestroy), AudioCapture mic reading, AudioPlayback clip manipulation, PacketAssembler, FunctionCallHandler dispatch, MainThreadDispatcher drain | YES |
| **WebSocket Receive Thread** (thread pool) | `LiveSession.ReceiveAsync()` loop, JSON parsing of responses, `ConvertBytesToFloat` | NO |
| **WebSocket Send Thread** (thread pool) | `LiveSession.SendAudioAsync()`, `SendAsync()` -- these are awaited from whichever thread calls them, but the actual send is serialized by `SemaphoreSlim` | NO |
| **HTTP Thread** (thread pool) | `ChirpTTSClient` HTTP POST to Cloud TTS API | NO |

### Marshaling Strategy

```
Background Thread                    ConcurrentQueue<Action>              Main Thread

ReceiveAsync yields response  --->  Enqueue(() => {                  -->  Update() drains queue
                                      assembler.ProcessChunk(data);        assembler processes
                                    })                                     fires events

ChirpTTS gets audio back     --->  Enqueue(() => {                  -->  Update() drains queue
                                      playback.QueueAudio(pcm);           playback creates clip
                                    })                                     plays audio

FunctionCallHandler result          (result returned to delegate)    -->  Needs to send response
  needs to go back           --->  Task.Run(() => {                       back on background
                                      session.SendAsync(response);        thread
                                    })
```

### Critical Constraint: Function Call Response Path

Function calls arrive on a background thread, get marshaled to main thread for handler execution, then the response must be sent back via `LiveSession.SendAsync()` which is an async operation on the WebSocket. This creates a main-thread -> background-thread -> main-thread -> background-thread round trip:

1. Background: ReceiveAsync yields LiveSessionToolCall
2. Main: Enqueue -> FunctionCallHandler invokes delegate -> gets result
3. Background: Task.Run -> session.SendAsync(FunctionResponsePart)

The handler delegate itself MUST be synchronous (it runs on the main thread during Update). If a handler needs async work (e.g., database lookup), it should return immediately with a "pending" marker and send the function response asynchronously when ready.

## Scalability Considerations

| Concern | Single Persona | 2-4 Personas | 10+ Personas |
|---------|---------------|--------------|--------------|
| WebSocket connections | 1 connection, fine | 2-4 connections, fine | May hit Firebase rate limits; consider connection pooling |
| Audio capture | 1 Microphone, shared | Same mic, send to multiple sessions | Same mic, fan-out to sessions |
| Audio playback | 1 AudioSource | Multiple AudioSources, Unity mixer handles | Need spatial audio management, voice priority system |
| Memory (audio buffers) | ~100KB ring buffers | ~400KB total | Pool management becomes critical, consider streaming-only |
| Thread pool pressure | 1 receive task | 2-4 receive tasks, fine | May want dedicated threads instead of thread pool |
| GC pressure | Manageable with pooling | Moderate, still manageable | Must pool aggressively, avoid LINQ in hot paths |

## Build Order (Dependency Graph)

Components should be built in this order based on what depends on what:

```
Phase 1: Foundation (no Firebase dependency)
  MainThreadDispatcher     -- standalone, no dependencies
  PersonaConfig            -- standalone ScriptableObject
  SessionState             -- standalone enum
  PersonaPacket            -- standalone struct
  AudioConverter (Internal) -- standalone utility

Phase 2: Audio Pipeline (Unity API only)
  AudioCapture             -- depends on: Unity Microphone API
  AudioPlayback            -- depends on: Unity AudioSource, AudioConverter

Phase 3: Firebase Integration
  SystemInstructionBuilder -- depends on: PersonaConfig, Firebase.AI.ModelContent
  PersonaSession (connect) -- depends on: PersonaConfig, Firebase.AI.LiveGenerativeModel
  PersonaSession (receive) -- depends on: MainThreadDispatcher, Firebase.AI.LiveSession

Phase 4: Processing
  PacketAssembler          -- depends on: PersonaPacket, LiveSessionResponse types
  FunctionCallHandler      -- depends on: Firebase.AI.FunctionCallPart/FunctionResponsePart

Phase 5: Voice Backend
  ChirpTTSClient           -- depends on: UnityEngine.Networking or System.Net.Http
  VoiceBackendRouter       -- depends on: PersonaConfig.voiceBackend, ChirpTTSClient

Phase 6: Integration & Polish
  Full PersonaSession      -- wires all components together
  Editor Inspectors        -- depends on: all Runtime types
  Sample Scenes            -- depends on: everything
```

**Rationale:** Phase 1-2 can be built and tested without any Firebase connection. Phase 3 is the first time a real API call happens. Phase 4 processes what comes back. Phase 5 adds the alternative voice path. Phase 6 ties everything together. This ordering means each phase is independently testable before moving to the next.

## Sources

- Firebase AI Logic SDK 13.7.0 source code: `Assets/Firebase/FirebaseAI/` (direct reading of LiveSession.cs, LiveGenerativeModel.cs, LiveSessionResponse.cs, FunctionCalling.cs, ModelContent.cs, LiveGenerationConfig.cs, ResponseModality.cs)
- Codebase analysis: `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONCERNS.md`, `.planning/codebase/INTEGRATIONS.md`
- Project specification: `.planning/PROJECT.md`
- Unity UPM package layout conventions (based on established Unity documentation patterns for custom packages -- HIGH confidence from direct knowledge of Unity 6 conventions)
- Threading model verified from Firebase SDK source: `ClientWebSocket` async operations run on .NET thread pool, `SemaphoreSlim` for send serialization (`LiveSession.cs:38`), `IAsyncEnumerable` for receive (`LiveSession.cs:287`)

---

*Architecture research: 2026-02-05*
