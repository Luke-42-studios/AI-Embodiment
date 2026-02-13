# Phase 7: WebSocket Transport and Audio Parsing - Research

**Researched:** 2026-02-13
**Domain:** Gemini Live API WebSocket protocol, C# ClientWebSocket, audio codec, Unity threading
**Confidence:** HIGH

## Summary

Phase 7 builds a standalone `GeminiLiveClient` that connects to the Gemini Live API over a direct WebSocket, sends/receives audio, and exposes all server events via a thread-safe event queue. This is a greenfield component with no Unity MonoBehaviour dependency -- pure C# with `System.Net.WebSockets.ClientWebSocket`.

A complete, working reference implementation exists in the Persona library at `/home/cachy/workspaces/projects/persona/unity/Persona/Runtime/GeminiLiveClient.cs` (839 lines). This implementation has been verified against the live API and covers: WebSocket connect, setup handshake, receive loop, audio decoding, transcription extraction, function call handling, text emote mode, and clean disconnect. The AI-Embodiment GeminiLiveClient should be adapted from this reference, simplified to match the AI-Embodiment package's needs (no emote/animation logic, which belongs in Phase 10), and placed in the `AIEmbodiment` namespace.

The Gemini Live protocol uses JSON-over-WebSocket with base64-encoded audio. Audio input is 16kHz 16-bit PCM mono; output is 24kHz 16-bit PCM mono. The `mediaChunks` field in `realtimeInput` is deprecated; use `audio` instead. Transcription (both input and output) lives inside `serverContent`, not as top-level server message fields.

**Primary recommendation:** Adapt the reference Persona GeminiLiveClient to the AI-Embodiment codebase. Strip emote/animation logic (Phase 10 concern). Use Newtonsoft.Json (JObject/JArray) for all JSON. Use `ConcurrentQueue<GeminiEvent>` for thread-safe event dispatch. Use `CancellationTokenSource` for lifecycle management.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Net.WebSockets.ClientWebSocket | .NET Standard 2.1 | WebSocket client | Built into Unity 6 (.NET Standard 2.1 profile), no external dependency needed. Used by both the Firebase AI SDK and the Persona reference implementation. |
| Newtonsoft.Json (JObject/JArray) | 13.0.1 via com.unity.nuget.newtonsoft-json 3.x | JSON serialization/deserialization | Decided in v0.8 requirements. Unity 6 ships it. Replaces MiniJSON. |
| System.Collections.Concurrent.ConcurrentQueue | .NET Standard 2.1 | Thread-safe event queue | Lock-free, zero-allocation dequeue. Already used in MainThreadDispatcher. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Threading.CancellationTokenSource | .NET Standard 2.1 | Cooperative cancellation | Pass to ConnectAsync, ReceiveAsync, SendAsync for clean shutdown |
| System.Convert.ToBase64String / FromBase64String | .NET Standard 2.1 | Audio data encoding/decoding | Encode outgoing PCM to base64 for realtimeInput; decode incoming base64 inlineData to PCM bytes |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ClientWebSocket | NativeWebSocket Unity package | Extra dependency, ClientWebSocket works fine in Unity 6 desktop |
| Newtonsoft.Json JObject | System.Text.Json | Not available in Unity 6 .NET Standard 2.1 profile |
| ConcurrentQueue | Channel\<T\> | Channel is .NET 6+ only, not available in Unity 6 |

**Installation:**
```bash
# Newtonsoft.Json -- add to package.json dependencies
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

## Architecture Patterns

### Recommended Project Structure
```
Packages/com.google.ai-embodiment/Runtime/
  GeminiLiveClient.cs      # WebSocket client (pure C#, no UnityEngine)
  GeminiEvent.cs            # Event struct + GeminiEventType enum
  GeminiLiveConfig.cs       # Config POCO (model, API key, voice, sample rates)
  ... (existing files)
```

### Pattern 1: Event Queue (Producer-Consumer)
**What:** Background receive loop enqueues `GeminiEvent` structs into a `ConcurrentQueue`. Main thread calls `ProcessEvents()` in Update() to drain the queue and fire `OnEvent`.
**When to use:** Always -- this is the core threading pattern for this component.
**Why:** Unity's main thread constraint means we cannot fire events directly from the WebSocket receive thread. ConcurrentQueue is lock-free for single-producer scenarios and has zero allocation on dequeue.

```csharp
// Source: Reference implementation GeminiLiveClient.cs
private readonly ConcurrentQueue<GeminiEvent> _eventQueue = new();

public event Action<GeminiEvent> OnEvent;

// Called from background receive loop
private void Enqueue(GeminiEvent ev)
{
    _eventQueue.Enqueue(ev);
}

// Called from Unity Update() on main thread
public void ProcessEvents()
{
    while (_eventQueue.TryDequeue(out var ev))
    {
        OnEvent?.Invoke(ev);
    }
}
```

### Pattern 2: Setup-then-Receive Lifecycle
**What:** ConnectAsync opens WebSocket, sends setup message, starts background receive loop. Receive loop blocks on WebSocket reads. setupComplete acknowledgment transitions to ready state.
**When to use:** Every connection.
**Why:** The Gemini Live protocol requires the setup message as the first message. The server responds with `{"setupComplete":{}}` before accepting any other messages.

```csharp
// Source: Reference implementation GeminiLiveClient.cs
public async Task ConnectAsync()
{
    _cts = new CancellationTokenSource();
    _ws = new ClientWebSocket();

    var url = $"{BaseUrl}?key={_config.ApiKey}";
    await _ws.ConnectAsync(new Uri(url), _cts.Token);
    _connected = true;

    await SendSetupMessage();

    // Fire-and-forget receive loop
    _ = ReceiveLoop(_cts.Token);
}
```

### Pattern 3: Full-Message Accumulation in Receive Loop
**What:** WebSocket messages may span multiple frames. The receive loop accumulates frames into a MemoryStream until `EndOfMessage` is true, then processes the complete message.
**When to use:** Every receive operation.
**Why:** ClientWebSocket.ReceiveAsync returns partial frames. Processing partial JSON or audio would corrupt data.

```csharp
// Source: Reference implementation GeminiLiveClient.cs
var buffer = new byte[64 * 1024];
var ms = new System.IO.MemoryStream();
WebSocketReceiveResult result;
do
{
    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
    if (result.MessageType == WebSocketMessageType.Close) { /* handle */ }
    ms.Write(buffer, 0, result.Count);
} while (!result.EndOfMessage);
var bytes = ms.ToArray();
```

### Pattern 4: JSON Type Dispatch
**What:** Parse each complete message as JObject, check for known top-level keys (`setupComplete`, `serverContent`, `toolCall`, `toolCallCancellation`), dispatch to specific handlers.
**When to use:** Every received JSON message.
**Why:** The Gemini Live protocol uses a union-type message pattern. Each message has exactly one of these top-level keys.

```csharp
if (msg["setupComplete"] != null)     { /* handle setup ack */ }
var serverContent = msg["serverContent"] as JObject;
if (serverContent != null)             { /* handle content */ }
var toolCall = msg["toolCall"] as JObject;
if (toolCall != null)                  { /* handle tool calls */ }
```

### Anti-Patterns to Avoid
- **Processing partial WebSocket frames as complete messages:** Always accumulate until EndOfMessage.
- **Using `async void` for the receive loop:** Use `async Task` and fire-and-forget with `_ =`. The `async void` swallows exceptions silently.
- **Sending before setupComplete:** The server will reject messages sent before acknowledging setup. Track `_setupComplete` flag.
- **Multiple concurrent receive loops:** Only ONE receive loop per WebSocket. The reference implementation enforces this by starting exactly one `_ = ReceiveLoop()` in ConnectAsync.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON parsing | Custom parser or regex extraction | Newtonsoft.Json JObject.Parse | Handles edge cases (nested objects, escaping, null values) that naive parsing misses |
| Base64 codec | Custom base64 encoder/decoder | System.Convert.ToBase64String / FromBase64String | Standard, optimized, handles padding correctly |
| Thread-safe queue | Lock-based queue or custom ring buffer | ConcurrentQueue\<T\> | Lock-free, tested, handles all edge cases |
| WebSocket client | Raw TCP/TLS socket management | System.Net.WebSockets.ClientWebSocket | Handles framing, masking, close handshake, TLS, ping/pong |
| PCM float-to-int16 conversion | Naive cast | Clamp + scale pattern: `(short)(Math.Clamp(sample * 32767f, -32768f, 32767f))` | Must clamp to prevent overflow wrapping |
| Cancellation propagation | Boolean flags | CancellationTokenSource / CancellationToken | Cooperative cancellation with proper exception handling, integrates with async/await |

**Key insight:** The reference implementation at `/home/cachy/workspaces/projects/persona/unity/Persona/Runtime/GeminiLiveClient.cs` already solves all of these correctly. Adapt, do not reinvent.

## Common Pitfalls

### Pitfall 1: mediaChunks is Deprecated
**What goes wrong:** The reference Persona implementation uses `realtimeInput.mediaChunks` to send audio. The official API reference marks `mediaChunks` as DEPRECATED and instructs to use `realtimeInput.audio` instead.
**Why it happens:** The reference was written against an older API version. The field still works but could be removed in a future API version.
**How to avoid:** Use the new `realtimeInput.audio` field format:
```json
{
  "realtimeInput": {
    "audio": {
      "mimeType": "audio/pcm;rate=16000",
      "data": "<base64>"
    }
  }
}
```
Instead of the deprecated:
```json
{
  "realtimeInput": {
    "mediaChunks": [{"mimeType": "audio/pcm;rate=16000", "data": "<base64>"}]
  }
}
```
**Warning signs:** API returns error 1008 or deprecation warnings in response.

### Pitfall 2: Audio is 16-bit PCM Bytes, Not Float Arrays
**What goes wrong:** The GeminiLiveClient receives audio as base64-encoded 16-bit signed PCM bytes (little-endian). Developers pass these raw bytes directly to AudioPlayback which expects float arrays in [-1.0, 1.0] range.
**Why it happens:** Confusion between the wire format (16-bit PCM bytes) and Unity's audio format (float arrays).
**How to avoid:** Convert in the GeminiEvent: decode base64 to byte[], then convert 16-bit PCM to float[]:
```csharp
// 16-bit LE PCM bytes -> float[]
int sampleCount = pcmBytes.Length / 2;
float[] floats = new float[sampleCount];
for (int i = 0; i < sampleCount; i++)
{
    short sample = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
    floats[i] = sample / 32768f;
}
```
**Decision point:** Convert to float[] inside GeminiLiveClient before enqueuing the event (cleaner API for consumers), OR return raw bytes and let the consumer convert (more flexible). The reference implementation returns raw bytes. For AI-Embodiment, converting to float[] in the client is better because AudioPlayback.EnqueueAudio already expects float[].

### Pitfall 3: Gemini Sends JSON as Binary WebSocket Frames
**What goes wrong:** Developers expect JSON on Text frames and binary on Binary frames. Gemini Live sends ALL messages (including JSON) as Binary WebSocket frames.
**Why it happens:** The Firebase SDK confirms this: `LiveSession.ReceiveAsync()` (line 312) only handles `WebSocketMessageType.Binary`, and throws on `WebSocketMessageType.Text`.
**How to avoid:** After accumulating a complete message, check if the first byte looks like JSON (`{` or `[`). If so, decode as UTF-8 string and parse as JSON. Otherwise treat as raw binary audio data. The reference implementation does exactly this:
```csharp
bool isJson = bytes.Length > 0 && (bytes[0] == '{' || bytes[0] == '[');
```
**Note:** In practice, Gemini sends all responses as JSON with audio inside base64 `inlineData` fields. Raw binary frames may not actually occur. But the check is a safe defensive pattern.

### Pitfall 4: Missing Model Prefix in Setup Message
**What goes wrong:** The setup message requires `"model": "models/gemini-2.5-flash-native-audio-preview-12-2025"` with the `models/` prefix. Omitting it or using just the model name causes an InternalServerError and immediate WebSocket close.
**Why it happens:** Other Google APIs accept bare model names. The Live API requires the full resource path.
**How to avoid:** Always prefix: `setupInner["model"] = "models/" + _config.Model;`
**Warning signs:** WebSocket closes with InternalServerError immediately after setup message.

### Pitfall 5: Disconnect Race Condition
**What goes wrong:** Calling Disconnect while the receive loop is mid-read causes WebSocketException or ObjectDisposedException.
**Why it happens:** CancellationToken cancels the receive, but the WebSocket close handshake and disposal happen on a different thread.
**How to avoid:**
1. Cancel the CTS first (receive loop catches OperationCanceledException and exits)
2. Then attempt close handshake with `CancellationToken.None` and a timeout
3. Then dispose the WebSocket
4. Swallow exceptions during close (connection may already be dead)
The reference implementation handles this correctly in its `Disconnect()` method.

### Pitfall 6: setupComplete Must Be Awaited
**What goes wrong:** Sending audio or text before the server sends `{"setupComplete":{}}` causes the server to reject or ignore the messages.
**Why it happens:** The receive loop runs in the background. The setupComplete event arrives asynchronously. If the caller starts sending immediately after ConnectAsync returns, the setup may not be complete yet.
**How to avoid:** Track `_setupComplete` as a volatile bool. Set it when the receive loop sees `setupComplete`. Guard all send methods with `if (!IsConnected) return;` where `IsConnected => _connected && _setupComplete`. Callers can subscribe to the `GeminiEventType.Connected` event to know when the client is ready.

### Pitfall 7: MemoryStream Allocation Per Message
**What goes wrong:** Creating a new `MemoryStream` for every WebSocket message creates GC pressure during high-frequency audio streaming.
**Why it happens:** The reference implementation allocates a MemoryStream per message for simplicity.
**How to avoid:** This is acceptable for Phase 7. The receive loop processes ~10-30 messages per second (audio chunks + transcriptions + turn events). MemoryStream allocation is small relative to the audio data itself. Optimize only if profiling shows GC pressure. A future optimization would be to use a recyclable MemoryStream or ArrayPool-backed buffer.

## Code Examples

Verified patterns from the reference implementation and official API docs:

### Setup Message Construction
```csharp
// Source: Reference GeminiLiveClient.cs lines 186-273, adapted for AI-Embodiment
private async Task SendSetupMessage()
{
    var setup = new JObject();
    var setupInner = new JObject();

    setupInner["model"] = "models/" + _config.Model;

    var genConfig = new JObject
    {
        ["responseModalities"] = new JArray("AUDIO"),
        ["speechConfig"] = new JObject
        {
            ["voiceConfig"] = new JObject
            {
                ["prebuiltVoiceConfig"] = new JObject
                {
                    ["voiceName"] = _config.VoiceName ?? "Puck"
                }
            }
        }
    };
    setupInner["generationConfig"] = genConfig;

    // Enable transcription (AUD-03, AUD-04)
    setupInner["outputAudioTranscription"] = new JObject();
    setupInner["inputAudioTranscription"] = new JObject();

    // System instruction (optional)
    if (!string.IsNullOrEmpty(_config.SystemInstruction))
    {
        setupInner["systemInstruction"] = new JObject
        {
            ["parts"] = new JArray { new JObject { ["text"] = _config.SystemInstruction } }
        };
    }

    // Tools (optional, will be populated in Phase 10)
    // if (tools != null) setupInner["tools"] = tools;

    setup["setup"] = setupInner;
    await SendJsonAsync(setup);
}
```

### Audio Send (Updated - Non-Deprecated)
```csharp
// Use realtimeInput.audio (NOT mediaChunks which is deprecated)
public void SendAudio(byte[] pcm16Data)
{
    if (!IsConnected || pcm16Data == null || pcm16Data.Length == 0) return;

    var encoded = Convert.ToBase64String(pcm16Data);
    var payload = new JObject
    {
        ["realtimeInput"] = new JObject
        {
            ["audio"] = new JObject
            {
                ["mimeType"] = "audio/pcm;rate=16000",
                ["data"] = encoded
            }
        }
    };
    _ = SendJsonAsync(payload);
}
```

### Audio Decoding from serverContent
```csharp
// Source: Reference GeminiLiveClient.cs lines 569-584, enhanced for AI-Embodiment
var inlineData = part["inlineData"] as JObject;
if (inlineData != null)
{
    var b64 = inlineData["data"]?.ToString();
    if (!string.IsNullOrEmpty(b64))
    {
        var audioBytes = Convert.FromBase64String(b64);

        // Convert 16-bit PCM to float[] for AudioPlayback compatibility
        int sampleCount = audioBytes.Length / 2;
        float[] floats = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(audioBytes[i * 2] | (audioBytes[i * 2 + 1] << 8));
            floats[i] = sample / 32768f;
        }

        Enqueue(new GeminiEvent
        {
            Type = GeminiEventType.Audio,
            AudioData = floats,
            AudioSampleRate = 24000
        });
    }
}
```

### Transcription Extraction
```csharp
// Source: Reference GeminiLiveClient.cs lines 617-641
// outputTranscription (AI speech text) -- inside serverContent
var outputTranscription = serverContent["outputTranscription"] as JObject;
if (outputTranscription != null)
{
    var t = outputTranscription["text"]?.ToString();
    if (!string.IsNullOrEmpty(t))
    {
        Enqueue(new GeminiEvent { Type = GeminiEventType.OutputTranscription, Text = t });
    }
}

// inputTranscription (user STT) -- inside serverContent
var inputTranscription = serverContent["inputTranscription"] as JObject;
if (inputTranscription != null)
{
    var t = inputTranscription["text"]?.ToString();
    if (!string.IsNullOrEmpty(t))
    {
        Enqueue(new GeminiEvent { Type = GeminiEventType.InputTranscription, Text = t });
    }
}
```

### Turn Lifecycle Events
```csharp
// Source: Reference GeminiLiveClient.cs lines 610-614
// turnComplete -- inside serverContent
var turnComplete = serverContent["turnComplete"];
if (turnComplete != null && turnComplete.Value<bool>())
{
    Enqueue(new GeminiEvent { Type = GeminiEventType.TurnComplete });
}

// interrupted -- inside serverContent (not in reference, but in Firebase SDK)
var interrupted = serverContent["interrupted"];
if (interrupted != null && interrupted.Value<bool>())
{
    Enqueue(new GeminiEvent { Type = GeminiEventType.Interrupted });
}
```

### GeminiEvent Struct (Adapted for AI-Embodiment)
```csharp
public enum GeminiEventType
{
    Audio,               // AI audio response (float[] PCM 24kHz)
    OutputTranscription, // AI speech text (AUD-04)
    InputTranscription,  // User speech text (AUD-03)
    TurnComplete,        // AI finished responding (AUD-05)
    Interrupted,         // User barged in (AUD-05)
    FunctionCall,        // AI triggered a function (Phase 10)
    Connected,           // Setup handshake complete
    Disconnected,        // WebSocket closed
    Error                // Something went wrong
}

public struct GeminiEvent
{
    public GeminiEventType Type;
    public string Text;              // For transcription, error, disconnected events
    public float[] AudioData;        // For Audio events (24kHz mono float[])
    public int AudioSampleRate;      // Sample rate of AudioData (always 24000)
    public string FunctionName;      // For FunctionCall events (Phase 10)
    public string FunctionArgsJson;  // For FunctionCall events (Phase 10)
}
```

### GeminiLiveConfig (Adapted for AI-Embodiment)
```csharp
public class GeminiLiveConfig
{
    public string ApiKey;
    public string Model = "gemini-2.5-flash-native-audio-preview-12-2025";
    public string SystemInstruction;
    public string VoiceName = "Puck";
    public int AudioInputSampleRate = 16000;
    public int AudioOutputSampleRate = 24000;
    // Tools and animations added in Phase 10
}
```

### Clean Disconnect
```csharp
// Source: Reference GeminiLiveClient.cs lines 82-109
public void Disconnect()
{
    if (!_connected && _ws == null) return;

    _connected = false;
    _setupComplete = false;
    _cts?.Cancel();

    try
    {
        if (_ws?.State == WebSocketState.Open || _ws?.State == WebSocketState.CloseReceived)
        {
            _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect",
                CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
        }
    }
    catch { /* Swallow -- tearing down */ }

    _ws?.Dispose();
    _ws = null;
    _cts?.Dispose();
    _cts = null;

    Enqueue(new GeminiEvent { Type = GeminiEventType.Disconnected, Text = "Disconnected" });
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `realtimeInput.mediaChunks` (array of blobs) | `realtimeInput.audio` (single blob) | 2025 (deprecated) | Must update send format; old format still works but deprecated |
| Firebase AI Logic SDK (`LiveSession`) | Direct `ClientWebSocket` to Gemini Live | v0.8 decision | Full protocol control, zero dependency |
| MiniJSON for serialization | Newtonsoft.Json `JObject`/`JArray` | v0.8 decision | Better API, handles nested objects, proper null handling |
| `gemini-2.0-flash-live-001` model | `gemini-2.5-flash-native-audio-preview-12-2025` | 2025 | Native audio support, dual transcription streams |
| Text + Audio response modalities | AUDIO-only with outputTranscription | 2025 with native audio models | Simpler flow, transcription is separate field not inline text |

**Deprecated/outdated:**
- `realtimeInput.mediaChunks`: Deprecated per official API reference. Use `realtimeInput.audio` instead.
- Firebase AI Logic SDK: Being removed in v0.8. Direct WebSocket replaces it.
- MiniJSON: Being removed in v0.8. Newtonsoft.Json replaces it.

## Verified Protocol Details

### Audio Formats (HIGH confidence - official docs + reference implementation)
- **Input:** 16kHz, 16-bit signed PCM, mono, little-endian. MIME: `audio/pcm;rate=16000`
- **Output:** 24kHz, 16-bit signed PCM, mono, little-endian. MIME: `audio/pcm;rate=24000`
- **Wire encoding:** Base64 in JSON `inlineData.data` field

### WebSocket Endpoint (HIGH confidence - reference implementation verified working)
```
wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={API_KEY}
```

### Message Protocol (HIGH confidence - official API reference + reference implementation)
- All messages are JSON, sent/received as Binary WebSocket frames
- Client messages have exactly one top-level key: `setup`, `clientContent`, `realtimeInput`, or `toolResponse`
- Server messages have exactly one top-level key: `setupComplete`, `serverContent`, `toolCall`, `toolCallCancellation`, `goAway`, or `sessionResumptionUpdate`
- `inputTranscription` and `outputTranscription` are INSIDE `serverContent`, not top-level

### Setup Handshake (HIGH confidence - verified working in reference)
1. Client connects to WSS endpoint with API key in query string
2. Client sends `{"setup": {...}}` with model, generationConfig, systemInstruction, tools, transcription config
3. Server responds with `{"setupComplete": {}}`
4. Client may now send `realtimeInput`, `clientContent`, or `toolResponse`

## Key Differences from Reference Implementation

The AI-Embodiment GeminiLiveClient differs from the Persona reference in these ways:

| Aspect | Persona Reference | AI-Embodiment Target |
|--------|-------------------|----------------------|
| Namespace | `Persona` | `AIEmbodiment` |
| Audio event data | `byte[] AudioData` (raw PCM) | `float[] AudioData` (converted for AudioPlayback) |
| Emote/animation logic | Built-in text emote mode, animation filtering | Deferred to Phase 10 |
| Custom functions in setup | Built-in emote function + custom functions | Deferred to Phase 10 (tools added later) |
| realtimeInput field | `mediaChunks` (deprecated) | `audio` (current) |
| `interrupted` handling | Not implemented | Implemented (matches Firebase SDK) |
| Event types | `Transcription` + `InputTranscription` | `OutputTranscription` + `InputTranscription` (clearer naming) |
| SendAudio input | `byte[] pcm16Data` | `byte[] pcm16Data` (same -- AudioCapture provides float[] which PersonaSession converts) |
| toolCall handling | Full function dispatch + auto-response | Enqueue event only (dispatch in Phase 10) |

## Open Questions

Things that could not be fully resolved:

1. **goAway server message handling**
   - What we know: The API can send `goAway` to signal impending disconnect. Not handled in reference implementation.
   - What's unclear: When/why this is sent, how much warning time is given.
   - Recommendation: Log it as a warning but do not add complex reconnection logic in Phase 7. Phase 8 (PersonaSession) can add reconnection if needed.

2. **Session resumption**
   - What we know: The API supports `sessionResumption` in setup and `sessionResumptionUpdate` from server.
   - What's unclear: Exact format and when to use it.
   - Recommendation: Ignore for v0.8. Document as future enhancement.

3. **Audio output sample rate confirmation**
   - What we know: Official docs state 24kHz. Reference implementation uses 24kHz. The config defaults to 24kHz.
   - What's unclear: Whether native audio models could change this. The `inlineData.mimeType` field includes `rate=24000` which could be parsed for verification.
   - Recommendation: Use 24kHz as default. Optionally parse mimeType from first audio response to verify. This addresses the STATE.md blocker: "Gemini output audio sample rate assumed 24kHz -- verify with actual API response".

## Sources

### Primary (HIGH confidence)
- Reference implementation: `/home/cachy/workspaces/projects/persona/unity/Persona/Runtime/GeminiLiveClient.cs` (839 lines, verified working)
- Reference events: `/home/cachy/workspaces/projects/persona/unity/Persona/Runtime/GeminiLiveEvents.cs`
- Reference config: `/home/cachy/workspaces/projects/persona/unity/Persona/Runtime/GeminiLiveConfig.cs`
- Firebase AI SDK: `/home/cachy/workspaces/projects/games/AI-Embodiment/Assets/Firebase/FirebaseAI/LiveSession.cs` (current integration being replaced)
- Firebase AI SDK: `/home/cachy/workspaces/projects/games/AI-Embodiment/Assets/Firebase/FirebaseAI/LiveSessionResponse.cs` (response parsing being replaced)
- [Live API - WebSockets API reference](https://ai.google.dev/api/live) - Official protocol specification
- [Live API capabilities guide](https://ai.google.dev/gemini-api/docs/live-guide) - Audio format specs, transcription config

### Secondary (MEDIUM confidence)
- [Get started with Live API](https://ai.google.dev/gemini-api/docs/live) - Setup examples
- [Gemini 2.0 Realtime WebSocket API Notes](https://gist.github.com/quartzjer/9636066e96b4f904162df706210770e4) - Practical protocol examples
- [Unity & Gemini Live API forum thread](https://discuss.ai.google.dev/t/unity-gemini-live-api-websocket-closed-with-internalservererror-after-sending-setup-messag/95520) - Common setup errors

### Tertiary (LOW confidence)
- Unity 6 .NET Standard 2.1 System.Net.WebSockets support inferred from .NET Standard spec (not explicitly confirmed in Unity docs for all platforms)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - ClientWebSocket verified in reference implementation and Firebase SDK; Newtonsoft.Json is a locked decision
- Architecture: HIGH - Reference implementation is a complete, working example of the exact pattern needed
- Protocol details: HIGH - Cross-verified between official API docs, reference implementation, and Firebase SDK source
- Pitfalls: HIGH - Identified from direct code reading and API documentation analysis
- Audio format: HIGH - Official docs confirm 24kHz 16-bit PCM output, 16kHz 16-bit PCM input

**Research date:** 2026-02-13
**Valid until:** 2026-04-13 (60 days -- Gemini Live API is stable, protocol unlikely to change significantly)
