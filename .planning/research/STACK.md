# Technology Stack

**Project:** AI Embodiment (Unity UPM Package)
**Researched:** 2026-02-05
**Overall Confidence:** HIGH (primary source: actual SDK source code in project)

## Research Method

This stack analysis is derived primarily from **reading the actual Firebase AI Logic SDK source code** present at `Assets/Firebase/FirebaseAI/` (v13.7.0), the Unity project manifest, and the project configuration files. Web search and web fetch tools were unavailable during this research session, so third-party documentation could not be verified externally. Confidence levels reflect what could be confirmed from source code inspection versus what relies on training data.

---

## Recommended Stack

### Core Runtime

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Unity 6 | 6000.3.7f1 | Game engine, audio pipeline, component model | Already in use. Unity 6 is the current LTS-equivalent. `AudioSource`, `Microphone`, `AudioClip` APIs are stable. | HIGH |
| C# | 9.0 | Primary language | Confirmed via `Assembly-CSharp.csproj`. Supports `IAsyncEnumerable`, `record`, pattern matching -- all needed for streaming patterns. | HIGH |
| .NET Standard | 2.1 | Target framework | Confirmed in `.csproj`. Provides `System.Net.WebSockets`, `System.Threading.Tasks`, `System.Buffers` for audio buffer pooling. | HIGH |

### AI / Conversation Backend

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Firebase AI Logic SDK | 13.7.0 (source) | Gemini Live bidirectional streaming | Already imported. Source-level SDK gives us full visibility into the WebSocket protocol, audio format, and function calling wire format. No black-box dependencies. | HIGH |
| Firebase App SDK | 13.7.0 (native) | Firebase initialization, API key, auth token management | Already imported. Required by Firebase AI Logic. Provides `FirebaseApp.DefaultInstance`, API key from `google-services.json`. | HIGH |
| Gemini 2.0 Flash | (API-side) | AI model for real-time conversation | The `LiveGenerativeModel` is designed for Gemini 2.0 Flash's live streaming mode. Supports audio input/output, function calling, transcription. Model name passed as string to `GetLiveModel()`. | HIGH |

### Text-to-Speech (Chirp 3 HD)

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Google Cloud TTS REST API | v1 | Chirp 3 HD voice synthesis | Firebase AI Logic SDK does NOT include TTS. Must call Cloud TTS API directly via HTTP. The `SpeechConfig.UsePrebuiltVoice()` in the SDK only configures Gemini's native voices -- it does not provide Chirp 3 HD access. | HIGH |
| UnityWebRequest | Built-in | HTTP client for TTS requests | Unity-native HTTP. Runs on main thread by default, supports async via coroutines or `SendWebRequest().completed`. Preferred over `System.Net.Http.HttpClient` because it respects Unity's lifecycle and works on all platforms including WebGL/mobile. | HIGH |

**Chirp 3 HD API Details (MEDIUM confidence -- from training data, not verified against live docs):**

- **Endpoint:** `https://texttospeech.googleapis.com/v1/text:synthesize`
- **Auth:** API key via `?key=` query param or `Authorization: Bearer` with service account OAuth token
- **Request format:**
  ```json
  {
    "input": { "text": "Hello world" },
    "voice": {
      "languageCode": "en-US",
      "name": "en-US-Chirp3-HD-Achernar"
    },
    "audioConfig": {
      "audioEncoding": "LINEAR16",
      "sampleRateHertz": 24000
    }
  }
  ```
- **Response:** Base64-encoded audio in `audioContent` field
- **Audio format:** LINEAR16 (16-bit PCM), 24kHz sample rate recommended for HD quality
- **Available Chirp 3 HD voices:** Names follow the pattern `{lang}-{region}-Chirp3-HD-{VoiceName}` (e.g., Achernar, Algieba, Auva, Charon, Fenrir, Kore, Leda, Orus, Puck, Sulafat, Zephyr, and others)
- **IMPORTANT:** Verify voice name format and available voices against official docs before implementation. Chirp 3 HD voices may have been updated since training cutoff.

### Unity Audio Pipeline

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| `UnityEngine.Microphone` | Built-in | Audio capture from user microphone | Unity's cross-platform mic API. `Microphone.Start()` creates an `AudioClip` that fills a circular buffer. Read samples with `AudioClip.GetData()`. | HIGH |
| `UnityEngine.AudioSource` | Built-in | AI voice playback | Standard Unity spatial audio. Developers can attach effects, spatialize in 3D, and mix via AudioMixer. The key challenge is streaming PCM data to it in real-time. | HIGH |
| `UnityEngine.AudioClip` | Built-in | Audio buffer container | `AudioClip.Create()` with streaming flag for dynamic audio. Use `AudioClip.SetData()` to push PCM samples. | HIGH |

### Package Distribution

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Unity Package Manager (UPM) | Built-in | Package format and distribution | Standard Unity package distribution. Installable via git URL. Supports assembly definitions, samples, and dependency declaration. | HIGH |

### Supporting Libraries

| Library | Version | Purpose | When to Use | Confidence |
|---------|---------|---------|-------------|------------|
| `System.Buffers` (ArrayPool) | .NET Standard 2.1 | Buffer pooling for audio data | Always for audio PCM conversion. Avoids GC pressure from constant `float[]`/`byte[]` allocation in the hot audio path. | HIGH |
| `System.Threading.Channels` | .NET Standard 2.1 | Producer-consumer queues | Thread-safe hand-off between WebSocket receive thread and Unity main thread for audio/text data. | HIGH |
| `Google.MiniJSON` | (bundled) | JSON serialization | Already bundled with Firebase SDK. Use for any JSON that interfaces with Firebase. For project-specific serialization, prefer `UnityEngine.JsonUtility`. | HIGH |
| `UnityEngine.JsonUtility` | Built-in | Serialization for ScriptableObject configs | Use for project-authored data structures (persona config, etc). Fast, no allocation, GC-friendly. | HIGH |
| Newtonsoft.Json (Unity) | `com.unity.nuget.newtonsoft-json` | Complex JSON serialization | Only if `JsonUtility` is insufficient (e.g., polymorphic types, dictionary serialization). TTS API response parsing may benefit from this. | MEDIUM |

---

## What NOT to Use

| Technology | Why Not |
|------------|---------|
| `System.Net.Http.HttpClient` for TTS | Does not respect Unity's lifecycle, can cause issues on IL2CPP/mobile. `UnityWebRequest` is the correct choice for HTTP in Unity. |
| Newtonsoft.Json for everything | Adds a dependency. `JsonUtility` handles most cases faster with zero allocation. Only pull in Newtonsoft if you hit a `JsonUtility` limitation (no dictionary support, no polymorphism). |
| WebSocket# / NativeWebSocket / third-party WS libs | The Firebase SDK already manages its own `ClientWebSocket`. Adding another WebSocket library creates confusion. All Gemini Live communication goes through `LiveSession`. |
| FMOD / Wwise / third-party audio | Overkill for v1. Unity's built-in AudioSource + AudioClip + Microphone is sufficient. Adding middleware creates a dependency users must also install. |
| Firebase Auth SDK (for v1) | Adds complexity. The API key approach works for desktop-first development. Firebase Auth can be added later as an optional dependency. The SDK already discovers it via reflection if present. |
| `async void` patterns | Dangerous in Unity -- unhandled exceptions crash silently. All async code should return `Task` and be caught with try-catch or `UniTask`. |
| UniTask | Tempting for Unity async, but adds a dependency to the UPM package. Stick with standard `Task`/`async-await` from .NET Standard 2.1. Users who want UniTask can wrap the API themselves. |

---

## Firebase AI Logic SDK -- Detailed API Reference

This section documents the actual API surface from reading the source code. This is the ground truth for building the integration layer.

### Initialization

```csharp
// Get Firebase AI instance (GoogleAI backend -- simpler, no location needed)
var ai = FirebaseAI.GetInstance(FirebaseAI.Backend.GoogleAI());

// Or Vertex AI backend (requires location)
var ai = FirebaseAI.GetInstance(FirebaseAI.Backend.VertexAI("us-central1"));
```

**IMPORTANT BUG (from source inspection):** `LiveGenerativeModel.ConnectAsync()` at line 154 hardcodes the VertexAI-style model path in the setup message (`projects/{id}/locations/{location}/publishers/google/models/{name}`) regardless of backend. The `GetURL()` method correctly switches WebSocket endpoints. Recommendation: **Use VertexAI backend for Live sessions** until this is confirmed fixed upstream.

### Creating a Live Model

```csharp
var liveModel = ai.GetLiveModel(
    modelName: "gemini-2.0-flash-live-001",  // or "gemini-2.0-flash"
    liveGenerationConfig: new LiveGenerationConfig(
        responseModalities: new[] { ResponseModality.Audio },
        speechConfig: SpeechConfig.UsePrebuiltVoice("Puck"),
        inputAudioTranscription: new AudioTranscriptionConfig(),
        outputAudioTranscription: new AudioTranscriptionConfig(),
        temperature: 0.7f
    ),
    tools: new Tool[] { new Tool(functionDeclarations) },
    systemInstruction: ModelContent.Text("You are a friendly NPC...")
);
```

**Key facts from source:**
- `LiveGenerationConfig` supports: `speechConfig`, `responseModalities`, `temperature`, `topP`, `topK`, `maxOutputTokens`, `presencePenalty`, `frequencyPenalty`, `inputAudioTranscription`, `outputAudioTranscription`
- `ResponseModality` enum: `Text`, `Image`, `Audio` -- SDK comment says "currently only supports one type"
- `SpeechConfig.UsePrebuiltVoice(string voice)` -- these are **Gemini's native voices** (Puck, Kore, Aoede, Charon, Fenrir), NOT Chirp 3 HD voices
- `AudioTranscriptionConfig` is an empty struct -- its presence enables transcription, its absence disables it
- System instruction is a `ModelContent` -- use `ModelContent.Text("instruction")` for text-only

### Connecting and Streaming

```csharp
// Connect -- opens WebSocket, sends setup, returns LiveSession
LiveSession session = await liveModel.ConnectAsync(cancellationToken);

// Send audio (float[] from Unity Microphone)
await session.SendAudioAsync(audioSamples, cancellationToken);
// Expected format: 16-bit PCM at 16kHz, little-endian
// The SDK converts float[] to byte[] internally via ConvertTo16BitPCM()

// Send text
await session.SendTextRealtimeAsync("Hello", cancellationToken);

// Send structured content with turn-complete signal
await session.SendAsync(
    content: ModelContent.Text("What's the weather?"),
    turnComplete: true,
    cancellationToken: ct
);

// Receive responses (IAsyncEnumerable)
await foreach (var response in session.ReceiveAsync(cancellationToken))
{
    // Text content
    string text = response.Text;

    // Audio content (byte arrays of 16-bit PCM)
    IReadOnlyList<byte[]> audioBytes = response.Audio;

    // Audio as float[] (for Unity AudioClip.SetData)
    IReadOnlyList<float[]> audioFloats = response.AudioAsFloat;

    // Message type dispatch
    if (response.Message is LiveSessionContent content)
    {
        // content.TurnComplete -- model finished responding
        // content.Interrupted -- client interrupted the model
        // content.InputTranscription?.Text -- what the user said
        // content.OutputTranscription?.Text -- what the model said
    }
    else if (response.Message is LiveSessionToolCall toolCall)
    {
        foreach (var fc in toolCall.FunctionCalls)
        {
            string name = fc.Name;
            IReadOnlyDictionary<string, object> args = fc.Args;
            string id = fc.Id;
            // Execute function, then send response back
        }
    }
    else if (response.Message is LiveSessionToolCallCancellation cancel)
    {
        IReadOnlyList<string> cancelledIds = cancel.FunctionIds;
    }
}

// Close session
await session.CloseAsync(cancellationToken);
// Or session.Dispose()
```

### Function Calling

```csharp
// Declare functions
var emoteFunc = new FunctionDeclaration(
    name: "emote",
    description: "Trigger a character animation/emote",
    parameters: new Dictionary<string, Schema>
    {
        { "emote_name", Schema.String("The name of the emote to play") },
        { "intensity", Schema.Float("How intense the emote should be", minimum: 0f, maximum: 1f) }
    },
    optionalParameters: new[] { "intensity" }
);

var tool = new Tool(emoteFunc);

// Handle function response
var functionResponse = ModelContent.FunctionResponse(
    name: "emote",
    response: new Dictionary<string, object> { { "status", "playing" } },
    id: functionCallPart.Id  // Pass back the ID from the FunctionCallPart
);
await session.SendAsync(content: functionResponse);
```

### Audio Format Details (from source code)

**Input (microphone to Gemini):**
- Format: 16-bit PCM, 16kHz, mono, little-endian
- MIME type: `"audio/pcm"` (set in `SendAudioAsync`)
- Conversion: `LiveSession.ConvertTo16BitPCM(float[])` -- clamps to [-32768, 32767], copies via `Buffer.BlockCopy`
- Wire format: base64-encoded in JSON `{ "realtimeInput": { "audio": { "mimeType": "audio/pcm", "data": "base64..." } } }`

**Output (Gemini response audio):**
- Format: 16-bit PCM (sample rate determined by model/SpeechConfig -- typically 24kHz for Gemini native audio)
- Access: `response.Audio` returns `List<byte[]>`, `response.AudioAsFloat` converts to `float[]`
- Conversion: `LiveSessionResponse.ConvertBytesToFloat(byte[])` -- little-endian 16-bit to [-1f, 1f] float

**CRITICAL NOTE:** The SDK sends and receives audio as `"audio/pcm"` mime type. The sample rate for **input** is documented as 16kHz. The sample rate for **output** from Gemini's native voices is **24kHz** (this is from training data -- MEDIUM confidence). This mismatch means you need separate AudioClip configurations for capture vs playback.

---

## Unity Microphone API Specifics

**Confidence:** HIGH (Unity built-in API, well-documented, stable across versions)

### Capture Setup

```csharp
// Start recording
string deviceName = null; // null = default microphone
int sampleRate = 16000;   // 16kHz for Gemini input
int lengthSec = 1;        // Circular buffer length
bool loop = true;          // Circular buffer mode

AudioClip micClip = Microphone.Start(deviceName, loop, lengthSec, sampleRate);

// Wait for microphone to actually start
while (Microphone.GetPosition(deviceName) <= 0) { }
```

### Reading Samples

```csharp
int micPosition = Microphone.GetPosition(deviceName);
int samplesToRead = micPosition - lastReadPosition;
if (samplesToRead < 0) samplesToRead += micClip.samples; // Handle circular wrap

float[] buffer = new float[samplesToRead];
micClip.GetData(buffer, lastReadPosition);
lastReadPosition = micPosition;
```

### Key Parameters

| Parameter | Recommended Value | Rationale |
|-----------|-------------------|-----------|
| Sample rate | 16000 Hz | Gemini Live expects 16kHz PCM input (confirmed in SDK source: `ConvertTo16BitPCM` doc comment says "16 bit PCM audio at 16kHz") |
| Channels | 1 (mono) | Gemini expects mono audio |
| Buffer length | 1 second | Short enough for low latency, long enough to avoid overwrites at typical send rates (every 100-200ms) |
| Loop | true | Circular buffer -- read position chases write position |
| Send interval | 100ms (~1600 samples) | Good balance of latency vs overhead. Each send is ~3.2KB of PCM data. |

### Platform Caveats

| Platform | Issue | Mitigation |
|----------|-------|------------|
| Android | `Microphone.Start()` requires `RECORD_AUDIO` permission | Must declare in AndroidManifest.xml. Unity can auto-request in Player Settings. |
| iOS | Requires `NSMicrophoneUsageDescription` in Info.plist | Set in Player Settings > iOS > Microphone Usage Description. |
| WebGL | `Microphone` API not supported | Out of scope for v1. |
| macOS | Privacy prompt for mic access | Handled by OS, no code needed. |
| Linux | ALSA/PulseAudio required | Standard on modern Linux desktops. |

---

## AudioSource Streaming Patterns for Real-Time PCM Playback

**Confidence:** HIGH (established Unity pattern)

### Pattern 1: Streaming AudioClip with Ring Buffer (Recommended)

This is the recommended approach for real-time AI voice playback. Create a long AudioClip and continuously write incoming PCM data to it.

```csharp
// Create streaming AudioClip
int outputSampleRate = 24000; // Gemini native audio or Chirp 3 HD
int bufferLengthSec = 10;     // 10 second ring buffer
AudioClip streamClip = AudioClip.Create(
    "AIVoice",
    outputSampleRate * bufferLengthSec,
    1,                          // mono
    outputSampleRate,
    false                       // NOT streaming callback -- we push data manually
);

AudioSource audioSource = GetComponent<AudioSource>();
audioSource.clip = streamClip;
audioSource.loop = true;
audioSource.Play();

// Write incoming audio
int writePosition = 0;
void OnAudioReceived(float[] pcmData)
{
    streamClip.SetData(pcmData, writePosition);
    writePosition = (writePosition + pcmData.Length) % streamClip.samples;
}
```

**Why this pattern:**
- `AudioSource.Play()` with a looping clip continuously reads from the buffer
- We write ahead of the playback position
- Developers can apply Unity audio effects, spatialize in 3D, route through AudioMixer
- No native plugin required

**Key challenge:** Tracking write position vs. play position to avoid underrun (silence gaps) or overrun (audio glitches). The PacketAssembler component needs to manage this.

### Pattern 2: OnAudioFilterRead Callback (Alternative)

For more precise control, use `OnAudioFilterRead` on the AudioSource's GameObject.

```csharp
// MonoBehaviour on same GameObject as AudioSource
private ConcurrentQueue<float> _audioQueue = new();

void OnAudioFilterRead(float[] data, int channels)
{
    for (int i = 0; i < data.Length; i++)
    {
        if (_audioQueue.TryDequeue(out float sample))
        {
            data[i] = sample;
        }
        else
        {
            data[i] = 0f; // Silence on underrun
        }
    }
}
```

**Why NOT this as primary pattern:**
- Runs on audio thread (not main thread) -- cannot access most Unity APIs
- Harder for end-users to work with
- Still useful as an internal implementation detail within the package

### Playback Configuration

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Sample rate (Gemini native) | 24000 Hz | Gemini Live output audio sample rate (MEDIUM confidence) |
| Sample rate (Chirp 3 HD) | 24000 Hz | Chirp 3 HD recommended output rate (MEDIUM confidence) |
| Channels | 1 (mono) | AI voice is mono. Users can spatialize in Unity. |
| Buffer length | 10 seconds | Enough headroom for network jitter. PacketAssembler manages actual write position. |
| AudioSource.priority | 0 (highest) | AI voice should never be culled by Unity's audio channel limit. |

---

## UPM Package Structure

**Confidence:** HIGH (standard Unity convention, well-established)

### Directory Layout

```
com.yourcompany.ai-embodiment/
  package.json                    # Package manifest (REQUIRED)
  README.md                       # Package documentation
  CHANGELOG.md                    # Version history
  LICENSE.md                      # License file
  Runtime/
    AIEmbodiment.asmdef           # Runtime assembly definition (REQUIRED)
    PersonaConfig.cs              # ScriptableObject for persona configuration
    PersonaSession.cs             # MonoBehaviour -- main entry point
    LiveSessionManager.cs         # Wraps Firebase LiveSession lifecycle
    MicrophoneCapture.cs          # Microphone input to PCM conversion
    AudioPlayback.cs              # PCM streaming to AudioSource
    PacketAssembler.cs            # Synchronizes text + audio + emotes
    ChirpTTSClient.cs             # HTTP client for Cloud TTS API
    FunctionCallDispatcher.cs     # C# delegate-based function call routing
    SystemInstructionBuilder.cs   # Generates system prompt from PersonaConfig
  Editor/
    AIEmbodiment.Editor.asmdef    # Editor assembly definition
    PersonaConfigEditor.cs        # Custom inspector (optional)
  Tests/
    Runtime/
      AIEmbodiment.Tests.asmdef   # Test assembly definition
    Editor/
      AIEmbodiment.Editor.Tests.asmdef
  Samples~/                       # Samples (tilde = not auto-imported)
    BasicConversation/
      .sample.json
      SampleScene.unity
      SamplePersona.asset
  Documentation~/                 # Package documentation
    index.md
```

### package.json

```json
{
  "name": "com.nevatars.ai-embodiment",
  "version": "0.1.0",
  "displayName": "AI Embodiment",
  "description": "AI-powered game characters with real-time conversation, synchronized voice, and animation events.",
  "unity": "6000.0",
  "unityRelease": "0f1",
  "documentationUrl": "https://github.com/org/ai-embodiment#readme",
  "changelogUrl": "https://github.com/org/ai-embodiment/blob/main/CHANGELOG.md",
  "licensesUrl": "https://github.com/org/ai-embodiment/blob/main/LICENSE.md",
  "dependencies": {
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0"
  },
  "keywords": [
    "ai",
    "npc",
    "character",
    "conversation",
    "gemini",
    "voice",
    "tts"
  ],
  "author": {
    "name": "Nevatars",
    "url": "https://github.com/org"
  },
  "type": "library"
}
```

**Key decisions:**
- `"unity": "6000.0"` -- minimum Unity 6. Set floor at 6000.0 for broadest Unity 6 compatibility.
- Firebase AI Logic is NOT listed as a UPM dependency because it is distributed as a `.unitypackage`, not a UPM package. The README must instruct users to install Firebase SDK separately.
- `com.unity.modules.audio` and `com.unity.modules.unitywebrequest` are the only hard UPM dependencies.

### Assembly Definitions

The package MUST use Assembly Definitions (`.asmdef`) to:
1. Isolate compilation -- package code compiles independently from user code
2. Control references -- explicitly declare which assemblies the package depends on
3. Enable testability -- test assemblies can reference the runtime assembly

**Runtime asmdef references:**
- `Firebase.AI` (via GUID or assembly name reference)
- `Firebase.App` (via GUID or assembly name reference)
- `UnityEngine.AudioModule`
- `UnityEngine.UnityWebRequestModule`

**IMPORTANT:** Firebase AI Logic SDK files in `Assets/Firebase/FirebaseAI/` do NOT have their own `.asmdef` file. They compile into the default `Assembly-CSharp`. The UPM package's `.asmdef` will need to reference `Assembly-CSharp` or -- better -- the Firebase SDK files should be given their own `.asmdef` before the package is extracted. Alternatively, reference the Firebase DLLs directly if they are pre-compiled.

---

## Threading Model

**Confidence:** HIGH (Unity threading constraints are well-established)

### The Core Problem

Unity's main thread restriction means:
- `AudioSource`, `AudioClip`, `Microphone`, all `MonoBehaviour` methods -- **main thread only**
- Firebase `LiveSession.ReceiveAsync()` blocks waiting for WebSocket data -- **should NOT block main thread**
- Firebase `LiveSession.SendAudioAsync()` is async but fast -- **can call from main thread**

### Recommended Pattern

```
[Main Thread]                     [Background Thread/Task]

MonoBehaviour.Update()            LiveSession.ReceiveAsync() loop
  |                                 |
  +-- Read Microphone.GetData()     +-- Parse LiveSessionResponse
  +-- Send audio via SendAudioAsync +-- Extract audio/text/toolcalls
  |                                 +-- Write to Channel<T>
  +-- Read from Channel<T>
  +-- Push audio to AudioClip
  +-- Dispatch function calls
  +-- Update UI/animations
```

**Use `System.Threading.Channels.Channel<T>`** as the thread-safe handoff mechanism:
- Background task writes `LiveSessionResponse` items to channel
- Main thread reads from channel in `Update()` (non-blocking `TryRead`)
- No `lock` contention, no `ConcurrentQueue` overhead, built into .NET Standard 2.1

### SynchronizationContext

Unity provides a `UnitySynchronizationContext` on the main thread. Firebase SDK's `await` calls will resume on the captured context if started from the main thread. However, the `ReceiveAsync()` loop should be started with `Task.Run()` or `ConfigureAwait(false)` to avoid blocking the main thread.

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| AI Backend | Firebase AI Logic (Gemini Live) | Direct Gemini API WebSocket | Firebase SDK handles auth, token refresh, WebSocket protocol, type safety. No reason to reimplement. |
| AI Backend | Firebase AI Logic | OpenAI Realtime API | Google ecosystem (Firebase + Gemini + Chirp) is more integrated. OpenAI would require a separate auth system, different audio formats, different function calling protocol. |
| TTS | Chirp 3 HD via REST | ElevenLabs, Azure TTS | Chirp 3 HD is Google ecosystem (same project, same billing). Lower latency when co-located. Good voice quality for games. |
| TTS | Chirp 3 HD via REST | Gemini native audio only | Gemini native voices (Puck, Kore, etc.) are limited to ~5 voices. Chirp 3 HD offers many more voices with HD quality. Having both paths gives developers choice. |
| Audio capture | Unity Microphone API | Native plugins (e.g., NatCorder) | Adds platform-specific complexity. Unity Microphone API works on desktop and mobile. Sufficient for v1. |
| Audio playback | AudioSource + AudioClip.SetData | OnAudioFilterRead only | AudioSource gives users spatial audio, effects, mixing for free. OnAudioFilterRead is lower-level and harder to use. |
| JSON | JsonUtility + MiniJSON | Newtonsoft.Json everywhere | Fewer dependencies. JsonUtility is fast and GC-free. MiniJSON is already bundled. Only add Newtonsoft if hit a wall. |
| Async model | Task/async-await (.NET) | UniTask | Adding UniTask to a UPM package forces the dependency on all users. Standard Task works fine with .NET Standard 2.1. |
| Package format | UPM via git URL | Asset Store package | UPM supports proper versioning, dependency management, and CI. Asset Store requires review process and has different update semantics. |
| Config system | ScriptableObject | JSON/YAML files | ScriptableObject is Unity-native, Inspector-editable, serializable, and familiar to Unity developers. JSON config requires custom editor windows. |

---

## Version Pinning

| Component | Pinned Version | Update Strategy |
|-----------|----------------|-----------------|
| Firebase SDK | 13.7.0 | Pin until Live API exits Public Preview. Test thoroughly before upgrading. |
| Unity | 6000.3.x | Follow Unity 6 patch releases. No reason to stay on exact .7f1. |
| Gemini model | `gemini-2.0-flash-live-001` | Model version is a string -- can be changed at runtime or in persona config. |
| Chirp 3 HD | API v1 | Google TTS API is stable (v1). Monitor for v2 announcements. |

---

## Dependency Graph

```
[Developer's Unity Project]
    |
    +-- [AI Embodiment UPM Package]
    |       |
    |       +-- com.unity.modules.audio (built-in)
    |       +-- com.unity.modules.unitywebrequest (built-in)
    |       +-- Firebase AI Logic SDK (external, user must install)
    |              |
    |              +-- Firebase.App 13.7.0 (native plugins)
    |              +-- Google.MiniJSON (bundled)
    |              +-- ExternalDependencyManager 1.2.187
    |
    +-- [User's game code]
            |
            +-- References AI Embodiment package
            +-- Creates PersonaConfig ScriptableObjects
            +-- Adds PersonaSession to GameObjects
            +-- Registers function call handlers
```

---

## Installation Flow for End Users

```bash
# Step 1: Install Firebase AI Logic SDK (via .unitypackage or UPM tarball)
# Download from: https://firebase.google.com/docs/unity/setup
# Import FirebaseAI.unitypackage into Unity project

# Step 2: Configure Firebase
# Place google-services.json in Assets/
# Configure API key in Google Cloud Console

# Step 3: Install AI Embodiment package
# In Unity: Window > Package Manager > + > Add package from git URL
# URL: https://github.com/org/ai-embodiment.git

# Step 4: (Optional) Install Chirp 3 HD support
# Enable Cloud Text-to-Speech API in Google Cloud Console
# Create API key or service account for TTS
```

---

## Sources

- **Firebase AI Logic SDK source code** (v13.7.0): `Assets/Firebase/FirebaseAI/` -- READ DIRECTLY. All API details, wire formats, and WebSocket protocol confirmed from source.
- **Firebase version manifest**: `Assets/Firebase/Editor/FirebaseAI_version-13.7.0_manifest.txt`
- **Unity project manifest**: `Packages/manifest.json` -- confirmed all package versions
- **Assembly-CSharp.csproj**: confirmed C# 9.0, .NET Standard 2.1
- **Project settings**: `ProjectSettings/ProjectSettings.asset` -- confirmed Unity 6000.3.7f1, build targets
- **Google Cloud TTS API format**: Training data (MEDIUM confidence -- verify against live docs)
- **Gemini output audio sample rate (24kHz)**: Training data (MEDIUM confidence -- verify during implementation)
- **UPM package structure**: Training data (HIGH confidence -- stable convention since Unity 2019)
- **Unity Microphone/AudioSource API**: Training data (HIGH confidence -- stable APIs, unchanged in Unity 6)

---

## Items Requiring Phase-Specific Verification

These items could not be verified via web fetch and should be verified when implementation begins:

1. **Chirp 3 HD voice name format** -- Verify exact `name` field format against `https://cloud.google.com/text-to-speech/docs/chirp3-hd`
2. **Gemini Live output audio sample rate** -- Confirm 24kHz by inspecting actual response audio during first integration test
3. **Gemini native voice names** -- Confirm current set (Puck, Kore, Aoede, Charon, Fenrir) against Firebase docs
4. **Firebase AI Logic SDK update cadence** -- Check if a newer version than 13.7.0 is available
5. **`SpeechConfig` voice names for Gemini Live** -- The SDK references Chirp 3 HD voices in the `SpeechConfig` doc comment but the actual voices available via this path need verification
6. **`ResponseModality.Audio` output format** -- Whether the audio response includes a sample rate header or if it must be assumed

---

*Stack research: 2026-02-05*
