# Phase 2: Audio Pipeline - Research

**Researched:** 2026-02-05
**Domain:** Unity real-time audio capture/playback + Firebase AI Logic LiveSession audio streaming
**Confidence:** HIGH

## Summary

This research investigates the exact APIs and patterns needed to build a bidirectional audio pipeline between Unity's microphone, the Gemini Live API (via Firebase AI Logic SDK), and Unity's audio playback system.

The Firebase AI Logic SDK already provides nearly everything needed on the network side. `LiveSession.SendAudioAsync(float[])` accepts Unity-native float[] audio data at 16kHz and handles PCM conversion internally. `LiveSessionResponse.AudioAsFloat` returns decoded float[] arrays ready for Unity playback. The SDK handles base64 encoding/decoding, MIME type headers, and WebSocket framing -- our code never touches raw bytes for the network layer.

The primary engineering challenge is the Unity audio threading model: microphone capture runs on the main thread via polling (Update/coroutine), while audio playback via `OnAudioFilterRead` runs on the audio thread. A lock-free ring buffer bridges these two worlds. Gemini outputs 24kHz audio while Unity's audio system runs at `AudioSettings.outputSampleRate` (typically 44.1kHz or 48kHz), so linear interpolation resampling is needed in the ring buffer read path.

**Primary recommendation:** Use `OnAudioFilterRead` on the AudioPlayback component with a thread-safe ring buffer for zero-copy streaming playback. Use `Microphone.Start` with a looping AudioClip and `GetData`/`GetPosition` polling in a coroutine for capture. Let the Firebase SDK handle all PCM byte conversion -- send float[], receive float[].

## Standard Stack

### Core (Already in Project)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Firebase.AI (LiveSession) | In Assets/Firebase/FirebaseAI/ | Audio send/receive over WebSocket | SDK provides SendAudioAsync(float[]), AudioAsFloat, Transcription -- already handles PCM conversion, base64, MIME types |
| Unity Microphone API | Built-in (UnityEngine) | Audio capture from system mic | Only option for mic access in Unity; works cross-platform |
| Unity AudioSource | Built-in (UnityEngine) | Spatial audio playback | Standard Unity audio; supports mixer routing, 3D spatialization |
| OnAudioFilterRead | Built-in (MonoBehaviour) | Audio thread callback for streaming | Unity's only mechanism for real-time procedural audio injection |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| AudioSettings.outputSampleRate | Built-in | Query system audio sample rate | Needed for resampling 24kHz Gemini audio to system rate |
| Application.RequestUserAuthorization | Built-in | Desktop mic permission | Editor + Desktop permission flow |
| Permission.RequestUserPermission | Built-in (Android) | Android mic permission | Android runtime permission flow |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| OnAudioFilterRead ring buffer | AudioClip.Create(stream:true) with PCMReaderCallback | PCMReaderCallback runs on audio thread too but requires managing AudioClip lifecycle; OnAudioFilterRead is simpler since it works with any existing AudioSource |
| OnAudioFilterRead ring buffer | AudioClip.SetData on looping clip | SetData runs on main thread causing latency; position tracking is fragile; cannot spatialize properly |
| Coroutine mic polling | Update() polling | Coroutine with WaitForEndOfFrame/yield is cleaner; Update() works equally well but clutters the Update loop |

## Architecture Patterns

### Recommended Project Structure (additions to existing package)

```
Packages/com.google.ai-embodiment/Runtime/
  PersonaSession.cs          # MODIFIED: Add audio component refs, push-to-talk API, speaking events
  AudioCapture.cs            # NEW: Microphone recording + streaming to session
  AudioPlayback.cs           # NEW: Ring buffer + OnAudioFilterRead streaming playback
  AudioRingBuffer.cs         # NEW: Lock-free ring buffer (shared utility)
```

### Pattern 1: Microphone Capture with Looping AudioClip

**What:** Start a looping AudioClip at 16kHz, poll `Microphone.GetPosition` in a coroutine, read new samples with `AudioClip.GetData`, send to LiveSession via `SendAudioAsync`.

**When to use:** Always for microphone capture -- this is the only reliable Unity pattern.

**Key details from SDK source (HIGH confidence):**

```csharp
// LiveSession.cs line 275 -- SDK provides this convenience method:
// "Expected format: 16 bit PCM audio at 16kHz little-endian"
public Task SendAudioAsync(float[] audioData, CancellationToken cancellationToken = default)

// Internally it:
// 1. Converts float[] to 16-bit PCM bytes via ConvertTo16BitPCM (line 252)
// 2. Wraps in InlineDataPart with "audio/pcm" MIME type
// 3. Sends via SendAudioRealtimeAsync -> realtimeInput JSON wrapper
```

**Capture pattern:**

```csharp
// AudioCapture.cs
private AudioClip _micClip;
private int _lastSamplePos;
private const int MIC_FREQUENCY = 16000;
private const int MIC_CLIP_LENGTH_SEC = 1; // 1 second looping buffer
private const int CHUNK_SAMPLES = 1600;    // 100ms chunks (16000 * 0.1)

public void StartCapture()
{
    _micClip = Microphone.Start(null, loop: true, MIC_CLIP_LENGTH_SEC, MIC_FREQUENCY);
    _lastSamplePos = 0;
    StartCoroutine(CaptureLoop());
}

private IEnumerator CaptureLoop()
{
    float[] buffer = new float[CHUNK_SAMPLES];
    while (_isCapturing)
    {
        yield return null; // poll every frame

        int currentPos = Microphone.GetPosition(null);
        if (currentPos == _lastSamplePos) continue;

        int samplesToRead;
        if (currentPos > _lastSamplePos)
        {
            samplesToRead = currentPos - _lastSamplePos;
        }
        else // wrapped around
        {
            samplesToRead = (_micClip.samples - _lastSamplePos) + currentPos;
        }

        if (samplesToRead < CHUNK_SAMPLES) continue; // accumulate at least one chunk

        // Read in chunk-sized pieces
        while (samplesToRead >= CHUNK_SAMPLES)
        {
            _micClip.GetData(buffer, _lastSamplePos);
            _lastSamplePos = (_lastSamplePos + CHUNK_SAMPLES) % _micClip.samples;
            samplesToRead -= CHUNK_SAMPLES;

            // Send to LiveSession (fire-and-forget on background thread)
            float[] chunk = new float[CHUNK_SAMPLES];
            System.Array.Copy(buffer, chunk, CHUNK_SAMPLES);
            _ = _session.SendAudioAsync(chunk, _cancellationToken);
        }
    }
}
```

### Pattern 2: Ring Buffer Streaming Playback via OnAudioFilterRead

**What:** Receive audio float[] from LiveSessionResponse.AudioAsFloat, write to a ring buffer, read from ring buffer in OnAudioFilterRead callback with resampling from 24kHz to system sample rate.

**When to use:** Always for AI voice playback -- OnAudioFilterRead is the only low-latency path that works with AudioSource spatialization.

**Key details from SDK source (HIGH confidence):**

```csharp
// LiveSessionResponse.cs line 82 -- SDK provides pre-decoded float arrays:
public IReadOnlyList<float[]> AudioAsFloat
{
    get { return Audio?.Select(ConvertBytesToFloat).ToArray(); }
}
// ConvertBytesToFloat (line 91) handles 16-bit PCM -> float conversion
// Audio data arrives as InlineDataPart with "audio/pcm" MIME type
// Audio is at 24kHz mono (Gemini Live output format)
```

**Ring buffer + playback pattern:**

```csharp
// AudioRingBuffer.cs -- thread-safe, lock-free
public class AudioRingBuffer
{
    private readonly float[] _buffer;
    private volatile int _writePos;
    private volatile int _readPos;
    private readonly int _capacity;

    public AudioRingBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new float[capacity];
    }

    public int Available => (_writePos - _readPos + _capacity) % _capacity;

    public void Write(float[] data, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _buffer[(_writePos + i) % _capacity] = data[offset + i];
        }
        _writePos = (_writePos + count) % _capacity;
    }

    public int Read(float[] data, int offset, int count)
    {
        int available = Available;
        int toRead = System.Math.Min(count, available);
        for (int i = 0; i < toRead; i++)
        {
            data[offset + i] = _buffer[(_readPos + i) % _capacity];
        }
        _readPos = (_readPos + toRead) % _capacity;
        // Zero-fill if underrun
        for (int i = toRead; i < count; i++)
        {
            data[offset + i] = 0f;
        }
        return toRead;
    }
}

// AudioPlayback.cs -- MonoBehaviour on same GameObject as AudioSource
public class AudioPlayback : MonoBehaviour
{
    private AudioRingBuffer _ringBuffer;
    private AudioSource _audioSource;
    private const int GEMINI_SAMPLE_RATE = 24000;
    private const int BUFFER_SECONDS = 2;
    private int _watermarkSamples;
    private double _resampleRatio;

    public void Initialize(AudioSource source)
    {
        _audioSource = source;
        int systemRate = AudioSettings.outputSampleRate;
        _resampleRatio = (double)GEMINI_SAMPLE_RATE / systemRate;

        // Ring buffer sized for 2 seconds of Gemini audio
        _ringBuffer = new AudioRingBuffer(GEMINI_SAMPLE_RATE * BUFFER_SECONDS);

        // Watermark: start playback after 150ms buffered
        _watermarkSamples = (int)(GEMINI_SAMPLE_RATE * 0.15f);

        // Create a silent dummy clip so AudioSource is "playing" and
        // OnAudioFilterRead gets called
        var clip = AudioClip.Create("AIVoice", systemRate, 1, systemRate, false);
        float[] silence = new float[systemRate];
        clip.SetData(silence, 0);
        _audioSource.clip = clip;
        _audioSource.loop = true;
        _audioSource.Play();
    }

    // Called from main thread when audio arrives from Gemini
    public void EnqueueAudio(float[] samples)
    {
        _ringBuffer.Write(samples, 0, samples.Length);
    }

    // Called on AUDIO THREAD -- must be lock-free
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (_ringBuffer == null || _ringBuffer.Available < _watermarkSamples)
        {
            // Underrun or not enough buffered yet -- output silence
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        int outputSamples = data.Length / channels;

        for (int i = 0; i < outputSamples; i++)
        {
            // Linear interpolation resampling from 24kHz to system rate
            double srcIndex = i * _resampleRatio;
            // ... (resample from ring buffer)

            float sample = ReadResampledSample(srcIndex);
            for (int ch = 0; ch < channels; ch++)
            {
                data[i * channels + ch] = sample;
            }
        }
    }
}
```

### Pattern 3: PersonaSession as Audio Orchestrator

**What:** PersonaSession holds Inspector references to optional AudioCapture and AudioPlayback components. It orchestrates the audio lifecycle: StartListening/StopListening, routing audio data between components and LiveSession, and firing speaking/transcript events.

**When to use:** Always -- this is the decided architecture from CONTEXT.md.

```
[PersonaSession]
  |-- [SerializeField] AudioCapture _audioCapture;  // optional
  |-- [SerializeField] AudioPlayback _audioPlayback; // optional
  |
  |-- StartListening()   -> _audioCapture.StartCapture()
  |-- StopListening()    -> _audioCapture.StopCapture()
  |
  |-- ProcessResponse()  -> routes AudioAsFloat to _audioPlayback.EnqueueAudio()
  |                      -> routes InputTranscription to OnUserTranscript event
  |                      -> detects audio start/stop for OnAISpeaking events
  |
  |-- Disconnect()       -> _audioCapture.StopCapture()
  |                      -> _audioPlayback.Stop()
```

### Anti-Patterns to Avoid

- **AudioClip.SetData on main thread for real-time playback:** Causes frame-dependent latency. Use OnAudioFilterRead instead.
- **Allocating arrays in OnAudioFilterRead:** Audio thread callback -- zero allocation allowed. Pre-allocate all buffers.
- **Calling Unity APIs from OnAudioFilterRead:** Most Unity APIs throw on the audio thread. Only primitive operations and array access are safe.
- **Using async/await in OnAudioFilterRead:** Audio thread has no SynchronizationContext. Stick to plain array operations.
- **Reading Microphone.GetPosition on audio thread:** Microphone API is main-thread only.
- **Sending each Microphone frame individually:** Accumulate at least ~100ms (1600 samples at 16kHz) before sending to reduce WebSocket overhead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| PCM float-to-byte conversion for sending | Manual bit manipulation | `LiveSession.SendAudioAsync(float[])` | SDK's ConvertTo16BitPCM (LiveSession.cs:252) handles clamping, endianness, Buffer.BlockCopy |
| PCM byte-to-float for receiving | Manual byte parsing | `LiveSessionResponse.AudioAsFloat` | SDK's ConvertBytesToFloat (LiveSessionResponse.cs:91) handles 16-bit decoding with proper clamping |
| Base64 encoding/decoding | System.Convert | SDK handles internally | InlineDataPart.ToJson does base64 encode; FromJson does base64 decode |
| WebSocket message framing | Manual JSON construction | `LiveSession.SendAudioRealtimeAsync` | Builds correct realtimeInput JSON wrapper with audio key |
| Input transcription parsing | Manual JSON extraction | `LiveSessionContent.InputTranscription` | SDK parses inputTranscription from serverContent JSON |
| Audio thread dispatcher | Custom thread synchronization | `OnAudioFilterRead` with ring buffer | Unity's audio system already provides the callback at the right frequency |
| Microphone permission (Android) | Custom Java plugin | `Permission.RequestUserPermission(Permission.Microphone)` | Unity's Android Permission API handles the system dialog |

**Key insight:** The Firebase AI Logic SDK does 90% of the audio network plumbing. Our code is purely Unity-side: capture mic -> send float[] -> receive float[] -> play through ring buffer. Never touch raw PCM bytes, base64, or JSON directly.

## Common Pitfalls

### Pitfall 1: OnAudioFilterRead Runs at System Sample Rate, Not Gemini's

**What goes wrong:** Audio plays at wrong pitch/speed. Chipmunk effect or slow-motion voice.
**Why it happens:** Gemini outputs 24kHz PCM. Unity's OnAudioFilterRead runs at `AudioSettings.outputSampleRate` (typically 44100 or 48000 Hz). If you read Gemini samples 1:1 into the output buffer, you consume them too fast (higher rate) and the audio plays at ~2x speed.
**How to avoid:** Implement linear interpolation resampling in OnAudioFilterRead. The ratio is `24000.0 / AudioSettings.outputSampleRate`. For each output sample, calculate the corresponding fractional position in the 24kHz source data and interpolate.
**Warning signs:** Voice sounds like chipmunks, or audio plays noticeably faster/slower than expected.

### Pitfall 2: Microphone.GetPosition Wrap-Around

**What goes wrong:** Audio chunks are skipped or duplicated when the circular mic buffer wraps.
**Why it happens:** `Microphone.Start(null, loop:true, ...)` creates a circular AudioClip. `GetPosition` returns a sample index that resets to 0 when it reaches the end. If you don't handle the wrap, you either read stale data or skip a chunk.
**How to avoid:** Track `_lastSamplePos`. When `currentPos < _lastSamplePos`, calculate `samplesToRead = (clipLength - _lastSamplePos) + currentPos`. Read in two parts: end-of-buffer then start-of-buffer.
**Warning signs:** Periodic audio glitches or gaps every N seconds (where N = mic clip length).

### Pitfall 3: Ring Buffer Underrun Causing Pops/Clicks

**What goes wrong:** Audible clicks, pops, or silence gaps in AI voice playback.
**Why it happens:** Network jitter causes audio chunks to arrive unevenly. If OnAudioFilterRead reads faster than data arrives, the buffer empties and outputs zeros (silence gap). When data resumes, the transition from silence to audio causes a click.
**How to avoid:** Implement a write-ahead watermark. Don't start reading until at least 100-200ms of audio is buffered. When underrun occurs, output silence (zeros) until the watermark threshold is met again. Consider a small fade-in (ramp) when resuming from underrun.
**Warning signs:** Sporadic clicks during continuous speech; silence gaps followed by a "pop" when audio resumes.

### Pitfall 4: Memory Allocation in Audio Thread

**What goes wrong:** GC stalls cause audio dropouts on mobile.
**Why it happens:** Allocating arrays, creating objects, or using LINQ in OnAudioFilterRead triggers garbage collection. The audio thread has a hard real-time deadline (~21ms at 48kHz with 1024-sample buffer).
**How to avoid:** Pre-allocate all buffers in Initialize. Never use `new`, LINQ, or string operations in OnAudioFilterRead. The ring buffer read should be pure index arithmetic and array access.
**Warning signs:** Audio glitches that correlate with GC.Collect in the profiler.

### Pitfall 5: ReceiveAsync Loop Breaking After Audio TurnComplete

**What goes wrong:** Session stops receiving after the first AI audio response.
**Why it happens:** Same as Phase 1 Pitfall 1 -- `LiveSession.ReceiveAsync` breaks the IAsyncEnumerable at TurnComplete. The existing outer while loop already handles this, but audio processing adds complexity: must also handle interrupted turns and reset audio state.
**How to avoid:** The outer while loop from Phase 1 already solves this. Audio processing hooks into `ProcessResponse` which runs within the existing loop. When `Interrupted` is true, clear the ring buffer to stop stale audio.
**Warning signs:** AI responds once with audio, then goes silent on subsequent messages.

### Pitfall 6: SendAudioAsync Flooding During Continuous Capture

**What goes wrong:** WebSocket send queue backs up, causing latency buildup or disconnection.
**Why it happens:** Sending every frame's microphone data creates too many small WebSocket messages. LiveSession uses a SemaphoreSlim(1,1) send lock (LiveSession.cs:38), so sends are serialized.
**How to avoid:** Accumulate at least 100ms of audio (1600 samples at 16kHz = 3200 bytes) before each SendAudioAsync call. This balances latency with throughput. The SDK's send lock ensures ordering.
**Warning signs:** Increasing latency over time, or WebSocket disconnection after sustained audio streaming.

### Pitfall 7: Android Microphone Permission Not Requested

**What goes wrong:** `Microphone.Start` returns null on Android because permission was never granted.
**Why it happens:** Android 6+ requires runtime permission request. Unity's `Microphone.Start` silently returns null if permission is denied.
**How to avoid:** Before calling `Microphone.Start`, check `Permission.HasUserAuthorizedPermission(Permission.Microphone)`. If false, call `Permission.RequestUserPermission` with callbacks. On Desktop/Editor, use `Application.RequestUserAuthorization(UserAuthorization.Microphone)`.
**Warning signs:** Microphone works in Editor but fails silently on Android builds.

### Pitfall 8: AudioSource Must Be "Playing" for OnAudioFilterRead

**What goes wrong:** `OnAudioFilterRead` never gets called, no audio plays.
**Why it happens:** Unity only invokes `OnAudioFilterRead` when the AudioSource component is active and playing. Without a clip assigned and `Play()` called, the callback never fires.
**How to avoid:** Create a dummy silent AudioClip, assign it to the AudioSource, set `loop = true`, and call `Play()`. OnAudioFilterRead then fires continuously, and our ring buffer provides the actual audio data.
**Warning signs:** Ring buffer fills up but no audio is heard; OnAudioFilterRead breakpoints never hit.

### Pitfall 9: Firebase SDK Barge-In Behavior

**What goes wrong:** Developer expects client-side interruption handling, but it doesn't exist.
**Why it happens:** Firebase AI Logic documentation states interruption handling is "not yet supported" as a built-in feature. However, the raw Gemini Live API does support server-side barge-in -- when the server detects the user speaking, it may send an `interrupted: true` flag on `LiveSessionContent`.
**How to avoid:** Per CONTEXT.md decision: "Gemini handles barge-in natively on server side -- we always stream audio up and always play audio down, no client-side interruption logic." When `LiveSessionContent.Interrupted` is true (already parsed by SDK), clear the ring buffer and fire OnInterrupted event. No voice activity detection on client side.
**Warning signs:** AI audio continues playing after user starts speaking (if barge-in doesn't fire server-side).

## Code Examples

### Example 1: LiveGenerationConfig for Audio Modality (Already in PersonaSession.cs)

```csharp
// Source: Existing PersonaSession.cs Connect() -- already configured for audio
var liveConfig = new LiveGenerationConfig(
    responseModalities: new[] { ResponseModality.Audio },
    speechConfig: SpeechConfig.UsePrebuiltVoice(_config.geminiVoiceName),
    temperature: _config.temperature,
    inputAudioTranscription: new AudioTranscriptionConfig(),
    outputAudioTranscription: new AudioTranscriptionConfig()
);
```

### Example 2: Processing Audio in Existing Receive Loop

```csharp
// Source: Extension of existing ProcessResponse in PersonaSession.cs
private void ProcessResponse(LiveSessionResponse response)
{
    if (response.Message is LiveSessionContent content)
    {
        // Route audio to playback component
        var audioChunks = response.AudioAsFloat;
        if (audioChunks != null && audioChunks.Count > 0 && _audioPlayback != null)
        {
            foreach (var chunk in audioChunks)
            {
                _audioPlayback.EnqueueAudio(chunk);
            }
            // Track speaking state for events
            if (!_aiSpeaking)
            {
                _aiSpeaking = true;
                MainThreadDispatcher.Enqueue(() => OnAISpeakingStarted?.Invoke());
            }
        }

        // Handle interruption -- clear buffered audio
        if (content.Interrupted && _audioPlayback != null)
        {
            _audioPlayback.ClearBuffer();
            _aiSpeaking = false;
            MainThreadDispatcher.Enqueue(() => OnInterrupted?.Invoke());
        }

        // Input transcription -> user transcript event
        if (content.InputTranscription.HasValue)
        {
            string transcript = content.InputTranscription.Value.Text;
            MainThreadDispatcher.Enqueue(() => OnUserTranscript?.Invoke(transcript));
        }

        // Turn complete -> AI stopped speaking
        if (content.TurnComplete)
        {
            if (_aiSpeaking)
            {
                _aiSpeaking = false;
                // Delay OnAISpeakingStopped until ring buffer drains
                MainThreadDispatcher.Enqueue(() => OnAISpeakingStopped?.Invoke());
            }
            MainThreadDispatcher.Enqueue(() => OnTurnComplete?.Invoke());
        }
    }
}
```

### Example 3: SendAudioAsync Usage (from SDK source)

```csharp
// Source: LiveSession.cs line 275 -- the convenience method we call
// This is the ONLY method AudioCapture needs to call.
// float[] at 16kHz mono -> SDK converts to PCM bytes -> base64 -> JSON -> WebSocket
await _liveSession.SendAudioAsync(audioChunk, cancellationToken);
```

### Example 4: Microphone Permission Request (Cross-Platform)

```csharp
// AudioCapture.cs
private IEnumerator RequestMicrophonePermission()
{
    #if UNITY_ANDROID && !UNITY_EDITOR
    if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
    {
        bool responded = false;
        bool granted = false;
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => { responded = true; granted = true; };
        callbacks.PermissionDenied += _ => { responded = true; granted = false; };
        callbacks.PermissionDeniedAndDontAskAgain += _ => { responded = true; granted = false; };

        Permission.RequestUserPermission(Permission.Microphone, callbacks);
        while (!responded) yield return null;

        if (!granted)
        {
            OnPermissionDenied?.Invoke();
            yield break;
        }
    }
    #else
    yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
    if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
    {
        OnPermissionDenied?.Invoke();
        yield break;
    }
    #endif

    // Permission granted -- safe to call Microphone.Start
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| SendMediaChunksAsync (deprecated) | SendAudioRealtimeAsync / SendAudioAsync(float[]) | Firebase AI Logic SDK current version | Use the new specific methods; old one is marked [Obsolete] |
| Manual PCM byte construction | LiveSession.SendAudioAsync(float[]) | Firebase AI Logic SDK current version | SDK handles float->PCM conversion internally |
| Manual audio response parsing | LiveSessionResponse.AudioAsFloat | Firebase AI Logic SDK current version | SDK decodes base64, converts PCM bytes to float[] |
| Client-side VAD for barge-in | Server-side barge-in (Interrupted flag) | Gemini Live API design | No client-side voice activity detection needed |

**Deprecated/outdated:**
- `SendMediaChunksAsync`: Marked `[Obsolete]` in LiveSession.cs. Use `SendAudioRealtimeAsync` or `SendAudioAsync(float[])` instead.

## Audio Format Reference

| Direction | Sample Rate | Channels | Bit Depth | Format | MIME Type |
|-----------|-------------|----------|-----------|--------|-----------|
| Microphone -> Gemini | 16,000 Hz | 1 (mono) | 16-bit | PCM little-endian | audio/pcm (SDK handles) |
| Gemini -> Playback | 24,000 Hz | 1 (mono) | 16-bit | PCM little-endian | audio/pcm (SDK handles) |
| Unity Audio System | varies (44100/48000) | varies | 32-bit float | IEEE float | N/A (internal) |

**Critical:** The 24kHz -> system rate resampling must happen in OnAudioFilterRead. The Microphone API natively supports requesting 16kHz in `Microphone.Start`.

## Ring Buffer Design

Based on research and the original Persona C++ implementation patterns:

**Sizing:** 2 seconds of 24kHz mono audio = 48,000 float samples. This handles network jitter of up to ~2 seconds.

**Write-ahead watermark:** 150ms = 3,600 samples at 24kHz. Don't start reading until this much is buffered. This absorbs typical network packet jitter.

**Underrun recovery:** When available samples drop below watermark, output silence and set a "buffering" flag. When samples exceed watermark again, resume reading. Optionally ramp up volume over ~5ms (120 samples at 24kHz) to avoid clicks.

**Thread safety:** Main thread writes (from ProcessResponse via MainThreadDispatcher), audio thread reads (OnAudioFilterRead). Single-producer single-consumer pattern. Use volatile int for read/write positions -- no locks needed.

**Resampling:** Linear interpolation in the read path. For each output sample at index `i`, compute source position `srcPos = i * (24000.0 / systemRate)`. Interpolate between `buffer[floor(srcPos)]` and `buffer[ceil(srcPos)]`.

## Session Lifecycle with Audio

The session connection flow from CONTEXT.md with audio:

```
1. PersonaSession.Connect()
   - Creates LiveGenerativeModel with ResponseModality.Audio
   - Connects via ConnectAsync
   - Audio components remain idle

2. PersonaSession.StartListening()  [NEW in Phase 2]
   - If _audioCapture assigned: start mic capture + send loop
   - If not assigned: no-op (text-only mode preserved)
   - Fires OnUserSpeakingStarted (after first audio chunk sent)

3. ReceiveLoop (existing) processes audio responses
   - AudioAsFloat -> _audioPlayback.EnqueueAudio()
   - InputTranscription -> OnUserTranscript
   - Interrupted -> clear ring buffer, fire OnInterrupted
   - TurnComplete -> fire OnAISpeakingStopped when buffer drains

4. PersonaSession.StopListening()  [NEW in Phase 2]
   - Stops mic capture
   - Fires OnUserSpeakingStopped

5. PersonaSession.Disconnect()
   - Stops AudioCapture (if running)
   - Stops AudioPlayback (clears buffer)
   - Existing cleanup (CTS cancel, CloseAsync, Dispose)
```

## Open Questions

1. **Gemini output sample rate verification**
   - What we know: Official docs say 24kHz output. C++ Persona library uses 24000 consistently. Firebase SDK's AudioAsFloat assumes 16-bit encoding (2 bytes per sample) matching the Gemini spec.
   - What's unclear: The SDK doesn't expose output sample rate metadata. We must assume 24kHz based on docs. If wrong, resampling ratio will be off and audio will sound pitched.
   - Recommendation: Hardcode 24kHz. If audio sounds wrong during integration testing, this is the first thing to check.

2. **OnAudioFilterRead with AudioSource spatial blend**
   - What we know: OnAudioFilterRead works with spatialized AudioSources. The developer assigns the AudioSource in Inspector and can configure 3D settings.
   - What's unclear: Whether spatialization affects the callback buffer size or channel count in ways that break our resampling.
   - Recommendation: Always output mono to the callback (fill all channels with same sample). AudioSource's spatial blend and Unity's mixer handle the rest.

3. **Session timeout with audio-only input**
   - What we know: Firebase docs state "Audio-only input sessions are limited to 15 minutes" and sessions terminate after ~10 minutes of connection.
   - What's unclear: Whether push-to-talk (intermittent audio) extends these limits vs continuous streaming.
   - Recommendation: Document the timeout. PersonaSession should handle unexpected disconnection gracefully (already handles this in the receive loop error path).

## Sources

### Primary (HIGH confidence)
- Firebase AI Logic SDK source: `Assets/Firebase/FirebaseAI/LiveSession.cs` -- SendAudioAsync, SendAudioRealtimeAsync, ConvertTo16BitPCM, ReceiveAsync
- Firebase AI Logic SDK source: `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs` -- AudioAsFloat, ConvertBytesToFloat, Transcription, LiveSessionContent.Interrupted
- Firebase AI Logic SDK source: `Assets/Firebase/FirebaseAI/LiveGenerationConfig.cs` -- SpeechConfig, AudioTranscriptionConfig, ResponseModality
- Firebase AI Logic SDK source: `Assets/Firebase/FirebaseAI/ResponseModality.cs` -- Audio enum value
- Existing PersonaSession.cs: `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` -- current connect/receive/process architecture
- Original Persona C++ library: `/home/cachy/workspaces/projects/persona/` -- audio format constants (16kHz input, 24kHz output), packet assembler patterns, streaming architecture

### Secondary (MEDIUM confidence)
- [Firebase AI Logic Live API Limits and Specs](https://firebase.google.com/docs/ai-logic/live-api/limits-and-specs) -- audio format specs, session limits
- [Firebase AI Logic Live API Capabilities](https://firebase.google.com/docs/ai-logic/live-api/capabilities) -- barge-in status, transcription config
- [Gemini Live API Getting Started](https://ai.google.dev/gemini-api/docs/live) -- audio format specs, chunk sizes
- [Unity Microphone API Reference](https://docs.unity3d.com/ScriptReference/Microphone.html) -- Start, GetPosition, devices
- [Unity Microphone.Start](https://docs.unity3d.com/ScriptReference/Microphone.Start.html) -- loop parameter behavior
- [Unity OnAudioFilterRead](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html) -- audio thread, channel interleaving
- [Unity AudioClip.Create](https://docs.unity3d.com/ScriptReference/AudioClip.Create.html) -- stream parameter, PCMReaderCallback
- [Unity AudioClip.GetData](https://docs.unity3d.com/ScriptReference/AudioClip.GetData.html) -- Span/float[] overloads, offset parameter
- [Unity AudioClip.SetData](https://docs.unity3d.com/ScriptReference/AudioClip.SetData.html) -- wrapping behavior
- [Unity Android Permission API](https://docs.unity3d.com/ScriptReference/Android.Permission.RequestUserPermission.html) -- runtime permission flow

### Tertiary (LOW confidence)
- [OnAudioFilterRead sample rate mismatch discussion](https://discussions.unity.com/t/onaudiofilterread-is-not-delivering-the-right-samplerate/884762) -- confirms OnAudioFilterRead runs at system rate
- [Unity audio sample rate handling discussion](https://discussions.unity.com/t/what-ive-learned-about-how-unity-handles-audio-sample-rates/533798) -- platform-specific rate defaults

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- SDK source code is in the project, APIs read directly
- Architecture: HIGH -- patterns verified against SDK source + original C++ reference implementation
- Audio formats: HIGH -- confirmed by official docs, SDK source, and C++ library constants (all agree: 16kHz input, 24kHz output)
- Ring buffer design: MEDIUM -- based on established patterns and original C++ reference, but specific tuning values (watermark, buffer size) need runtime validation
- Pitfalls: HIGH -- most derived from direct SDK source reading and Unity documentation

**Research date:** 2026-02-05
**Valid until:** 2026-04-05 (60 days -- SDK source is local, audio APIs are stable)
