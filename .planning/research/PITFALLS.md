# Domain Pitfalls

**Domain:** Unity UPM package with real-time voice AI (Firebase AI Logic / Gemini Live)
**Researched:** 2026-02-05
**Confidence:** HIGH for pitfalls verified against actual SDK source code; MEDIUM for general Unity audio/UPM pitfalls based on training data

---

## Critical Pitfalls

Mistakes that cause rewrites, data loss, or architectural collapse.

---

### Pitfall 1: Threading — Firebase Async Callbacks vs Unity Main Thread

**What goes wrong:** The Firebase AI Logic SDK is entirely `async/await` based. `LiveSession.ReceiveAsync()` returns `IAsyncEnumerable<LiveSessionResponse>` which yields responses on whatever thread the WebSocket receive completes on. Developers instinctively call `AudioSource.clip`, `AudioSource.Play()`, `transform.position`, or any `UnityEngine.Object` member from within the `await foreach` loop. Unity crashes or silently corrupts state because Unity API calls are not thread-safe and must occur on the main thread.

**Why it happens:** C# `async/await` with `ClientWebSocket` does not guarantee continuation on the calling thread. Unity's `SynchronizationContext` is set on the main thread, and `await` inside a MonoBehaviour coroutine started with `async void Start()` or `async Task` will typically resume on the main thread **only if the awaited task was started from the main thread and Unity's UnitySynchronizationContext is active**. However, `IAsyncEnumerable` iteration is different: each `MoveNextAsync()` call on the enumerator may resume on the ThreadPool thread that completed the WebSocket read. The SDK's `ReceiveAsync` (line 287-339 of LiveSession.cs) uses `_clientWebSocket.ReceiveAsync(buffer, cancellationToken)` which completes on an IO thread.

**Consequences:**
- `UnityException: ... can only be called from the main thread` at runtime
- Silent state corruption if exception is swallowed
- Intermittent crashes that are hard to reproduce (depends on thread scheduling)
- AudioSource manipulation from wrong thread can cause audio glitches or Unity editor lockup

**Warning signs:**
- `UnityException` mentioning "main thread" in console
- Audio plays sometimes but not always
- NullReferenceException on `AudioSource` that definitely exists
- Tests pass in isolation but fail under load

**Prevention:**
1. Create a dedicated `MainThreadDispatcher` utility that queues `Action` callbacks and executes them in `Update()`. This is the standard pattern for marshaling back to Unity's main thread.
2. The `await foreach` loop over `ReceiveAsync()` should run on a background Task. Each received `LiveSessionResponse` should be enqueued into a `ConcurrentQueue<LiveSessionResponse>`. A MonoBehaviour `Update()` method drains this queue on the main thread.
3. Never touch any `UnityEngine.Object` (AudioSource, Transform, GameObject, etc.) from the receive loop.
4. Consider wrapping the entire Firebase interaction in a plain C# class (not MonoBehaviour) that communicates with the MonoBehaviour layer exclusively through thread-safe queues.

**Detection test:** Add `Debug.Assert(Thread.CurrentThread.ManagedThreadId == 1)` at the top of any method that touches Unity APIs during development.

**Phase relevance:** Must be solved in the very first phase (core session management). Every subsequent feature depends on correct threading.

---

### Pitfall 2: ReceiveAsync Closes on TurnComplete — Single-Turn Trap

**What goes wrong:** The SDK's `ReceiveAsync()` method (LiveSession.cs, line 328-333) explicitly `break`s out of the receive loop when it encounters a `TurnComplete` flag. This means each call to `ReceiveAsync()` only yields responses for a single model turn. Developers who write a single `await foreach (var response in session.ReceiveAsync())` expecting it to be a persistent stream for the lifetime of the session will find that it stops after the first AI response completes.

**Why it happens:** The Gemini Live protocol has a turn-based structure. After the model finishes responding (TurnComplete=true), the client must explicitly call `ReceiveAsync()` again for the next turn. The SDK documentation comment says "Closes upon receiving a TurnComplete from the server" but developers coming from WebSocket experience expect a persistent stream.

**Consequences:**
- Session appears to "die" after the first AI response
- Developer adds reconnection logic when the real fix is re-calling ReceiveAsync
- If not re-called promptly, incoming server messages may be missed or buffered indefinitely in the WebSocket

**Warning signs:**
- "Session disconnected" after first response
- Audio plays once then nothing
- Works for one exchange but not multi-turn

**Prevention:**
1. Wrap `ReceiveAsync()` in an outer `while (!cancelled)` loop that re-calls it after each TurnComplete.
2. Document this explicitly in the PersonaSession component — it is a counter-intuitive API behavior.
3. Build the receive loop as: outer loop (session lifetime) -> inner loop (single turn via ReceiveAsync) -> process each response.

**Detection test:** Integration test that sends two sequential prompts and verifies responses to both.

**Phase relevance:** Core session management phase. Getting this wrong means the fundamental conversation loop is broken.

---

### Pitfall 3: ReceiveAsync Concurrency Warning — Multiple Enumerations

**What goes wrong:** The SDK comments explicitly state: "Having multiple of these ongoing will result in unexpected behavior" (LiveSession.cs, line 282). If a developer calls `ReceiveAsync()` from multiple places (e.g., one coroutine for audio, another for text), both will compete for the same WebSocket receive, causing message loss and corruption.

**Why it happens:** There is a single WebSocket with a single receive buffer. Two concurrent `ReceiveAsync()` calls will interleave reads from the same socket, with each reader getting random fragments of messages.

**Consequences:**
- Partial JSON parsing failures
- Missing audio chunks (some consumed by the wrong reader)
- Intermittent: works sometimes depending on timing
- Extremely difficult to debug because failures are non-deterministic

**Warning signs:**
- JSON parse exceptions in `LiveSessionResponse.FromJson`
- Audio with random gaps or corruption
- "WebSocket is not open" errors when it should be

**Prevention:**
1. Exactly ONE receive loop per LiveSession, ever. This must be a hard architectural constraint.
2. The single receive loop demultiplexes responses by type (audio, text, function call, tool call cancellation) and dispatches to separate handlers/queues.
3. Use the `ILiveSessionMessage` interface to pattern-match: `LiveSessionContent` for audio/text, `LiveSessionToolCall` for function calls, `LiveSessionToolCallCancellation` for cancellations.

**Phase relevance:** Core architecture. This constraint shapes the entire event dispatch system.

---

### Pitfall 4: Audio Sample Rate Mismatch — 16kHz Input vs 24kHz Output vs AudioSource

**What goes wrong:** The Gemini Live protocol expects 16kHz mono 16-bit PCM input (documented in `SendAudioAsync` comment: "Expected format: 16 bit PCM audio at 16kHz little-endian"). The SDK's `ConvertBytesToFloat` in LiveSessionResponse.cs assumes 16-bit encoding for output. However, the output sample rate from Gemini native audio may be 24kHz (Gemini Live's documented output format), while Chirp 3 HD TTS outputs at a potentially different rate. Unity's `AudioSource` plays at whatever sample rate you configure the `AudioClip` with. If you create an `AudioClip` at the wrong sample rate, audio plays back too fast (chipmunk voice) or too slow (demon voice).

**Why it happens:**
- Unity's `Microphone.Start()` default sample rate may differ from 16kHz
- Gemini native audio output is 24kHz PCM, but the SDK doesn't document this explicitly in the response
- Chirp 3 HD TTS returns audio at its own configured sample rate
- Developers assume input rate = output rate

**Consequences:**
- AI voice sounds like a chipmunk (played at 44100Hz when data is 24000Hz)
- AI voice sounds like a demon (played at 16000Hz when data is 24000Hz)
- Microphone capture at wrong rate causes garbled input to Gemini
- Subtle: audio sounds "almost right" but slightly off-pitch, hard to diagnose

**Warning signs:**
- Voice pitch is wrong
- Audio plays but words are unintelligible
- `Microphone.Start()` returns a clip at system default rate, not 16kHz

**Prevention:**
1. **Microphone capture:** Explicitly pass `16000` as the frequency parameter to `Microphone.Start(null, true, bufferLengthSec, 16000)`. Verify the actual sample rate of the returned AudioClip with `clip.frequency` since some platforms may not support 16kHz and will silently use a different rate — in that case, resample.
2. **Gemini native audio playback:** Create AudioClip at 24000Hz sample rate for Gemini audio responses. Verify by checking actual received audio — the SDK returns raw bytes with no explicit sample rate metadata.
3. **Chirp TTS playback:** Parse the sample rate from the TTS API response (Chirp 3 HD returns audio at the rate you request, typically 24000Hz). Create AudioClip at the matching rate.
4. **PacketAssembler:** Track sample rate per-source and set AudioClip frequency accordingly.
5. **Validation:** Play a known test phrase and verify pitch matches expectation in the first integration test.

**Detection test:** Record a known phrase, round-trip it through the system, and compare playback pitch to original.

**Phase relevance:** Audio pipeline phase. Must be validated before building PacketAssembler.

---

### Pitfall 5: Streaming Audio Playback — AudioClip.SetData Race Condition

**What goes wrong:** Unity's `AudioClip` is not designed for streaming append. The common approach is to pre-allocate a large `AudioClip` with `AudioClip.Create(name, totalSamples, channels, frequency, false)` and then call `AudioClip.SetData(samples, offsetInSamples)` to fill it as data arrives. But `AudioSource.Play()` starts playback immediately from sample 0, and if `SetData` writes are slower than playback reads, the AudioSource reaches unfilled (zero) samples, causing audible pops/silence gaps. If you call `SetData` while `AudioSource.isPlaying` is true, Unity does not lock — the read head can read partially written data.

**Why it happens:**
- Network latency means audio chunks arrive irregularly
- Gemini Live sends audio in small chunks within a turn
- There is no built-in streaming AudioClip in Unity (AudioClip.Create with `stream=true` is for `OnAudioRead` callback, not append-style streaming)

**Consequences:**
- Audible clicks/pops between chunks
- Silence gaps where buffer ran out
- Audio plays the first chunk then goes silent
- Garbled audio if write position and read position collide

**Warning signs:**
- Audio plays first word then stops
- Random clicks between sentences
- Audio works with short responses but breaks with long ones

**Prevention:**
1. **Ring buffer approach:** Pre-allocate a large circular AudioClip. Track write position (where new data goes) and read position (where AudioSource is playing). Only call `AudioSource.Play()` after accumulating enough buffered audio (e.g., 200-400ms worth) to absorb network jitter.
2. **OnAudioFilterRead approach (preferred for streaming):** Attach a script with `OnAudioFilterRead(float[] data, int channels)` to the AudioSource's GameObject. This callback is called on the audio thread and lets you fill audio samples directly from a ring buffer. This avoids the AudioClip.SetData race entirely.
3. **Double-buffer approach:** Use two AudioClips. Play clip A while filling clip B. Swap when A finishes and B is ready. This is simpler but has an audible gap at swap boundaries unless crossfaded.
4. **Write-ahead watermark:** Never start playback until at least N milliseconds of audio are buffered. Monitor buffer level and pause playback (or insert silence) if buffer drains too fast.

**Detection test:** Stream a 30-second response and verify no clicks/gaps in playback.

**Phase relevance:** Audio pipeline phase. This is the hardest technical problem in the project.

---

### Pitfall 6: InlineDataPart Base64 Encoding Overhead in Audio Streaming

**What goes wrong:** The SDK's `InlineDataPart.ToJson()` (ModelContent.cs, line 253) calls `Convert.ToBase64String(Data)` on every audio chunk before sending. Base64 encoding expands data by ~33%. For continuous 16kHz 16-bit mono PCM audio (32KB/sec raw), this becomes ~43KB/sec of JSON-encoded WebSocket traffic. The JSON serialization via `MiniJSON` also allocates strings on every send. At high send rates (e.g., sending audio every 100ms), this creates significant GC pressure.

**Why it happens:** The Gemini Live protocol uses JSON over WebSocket with base64-encoded binary data. This is inherent to the protocol, not a bug.

**Consequences:**
- GC spikes causing frame drops (visible as stutters in gameplay)
- Increased network bandwidth usage
- Higher latency due to encoding overhead
- Memory pressure from string allocations

**Warning signs:**
- Unity Profiler shows GC.Alloc spikes correlated with audio send rate
- Frame drops during voice capture
- Increasing memory usage during conversation

**Prevention:**
1. **Send audio in appropriately sized chunks:** Do not send every frame's audio samples individually. Buffer 100-250ms of audio before sending each chunk. This reduces the number of send operations and JSON serializations.
2. **Pool byte arrays:** Reuse byte arrays for PCM conversion instead of allocating new ones each send. The SDK's `ConvertTo16BitPCM` (LiveSession.cs, line 252) allocates new arrays every call.
3. **Profile early:** Use Unity Profiler in the first integration test to measure GC allocations during a 60-second conversation. Set a budget (e.g., <1KB/frame allocation from the AI system).
4. **Consider a send rate limiter:** Gemini Live can handle intermittent audio — you do not need to send continuously. Send chunks at 4-10 per second, not 60.

**Phase relevance:** Audio pipeline phase, but optimization can be deferred to a polish phase. Functional correctness first.

---

### Pitfall 7: WebSocket Lifetime vs Unity Lifecycle Mismatch

**What goes wrong:** `LiveSession` wraps a `ClientWebSocket` and implements `IDisposable`. It has a finalizer (`~LiveSession`) as backup. But Unity has its own lifecycle: `OnDestroy`, `OnApplicationPause`, `OnApplicationQuit`, scene transitions. If a MonoBehaviour holding a LiveSession is destroyed (scene change, GameObject.Destroy), the WebSocket connection is not properly closed because:
- `OnDestroy` is called on the main thread, but the receive loop may be running on a background thread
- The CancellationToken for the receive loop may not be cancelled before the MonoBehaviour is destroyed
- The finalizer runs on the GC thread at an unpredictable time, potentially after Unity has already shut down networking

**Why it happens:** Unity's lifecycle and C# IDisposable/async lifetime management are fundamentally different paradigms. Unity components disappear instantly on Destroy; C# async tasks need cooperative cancellation.

**Consequences:**
- WebSocket connections leak (server-side sessions remain open)
- `ObjectDisposedException` on the AudioSource after scene change
- Background thread tries to access destroyed MonoBehaviour
- Application hangs on quit because background task is still running
- Editor play mode "stop" hangs because WebSocket is still connected

**Warning signs:**
- Editor freezes when stopping Play mode
- "WebSocket is still open" warnings in console after scene change
- Memory grows across scene transitions
- Server-side billing for idle sessions

**Prevention:**
1. **CancellationTokenSource per session:** Create a `CancellationTokenSource` in `OnEnable` (or `Start`), pass its token to all async operations, and call `Cancel()` in `OnDisable` (or `OnDestroy`).
2. **Explicit disposal in OnDestroy:**
   ```
   void OnDestroy() {
       _cts?.Cancel();
       _liveSession?.Dispose();
   }
   ```
3. **OnApplicationPause:** Cancel and dispose the session when the app is paused (mobile backgrounding kills WebSocket connections anyway).
4. **OnApplicationQuit:** Force-close with a synchronous wait or fire-and-forget the close.
5. **Test scene transitions:** Specifically test Start Scene -> Game Scene -> back to Start Scene and verify no leaked connections or errors.

**Detection test:** Enter play mode, start a conversation, stop play mode, verify no console errors and no hung threads.

**Phase relevance:** Core session management phase. Must be designed into PersonaSession from day one.

---

### Pitfall 8: UPM Package with Firebase SDK — Dependency Hell

**What goes wrong:** The Firebase Unity SDK is distributed as `.unitypackage` files or through the External Dependency Manager (EDM4U), not as a proper UPM package. Your UPM package cannot declare Firebase AI Logic as a UPM dependency in `package.json` because it does not exist in the Unity Registry or any scoped registry. This means:
- Your package.json cannot enforce that Firebase AI Logic is installed
- Users can install your package without Firebase, and it will fail to compile
- Version mismatches between Firebase SDK versions are invisible

**Why it happens:** Firebase Unity SDK predates UPM's maturity. Google distributes it through their own mechanisms (EDM4U, direct download, .unitypackage). There is no `com.google.firebase.ai` UPM package in the Unity Registry.

**Consequences:**
- Users install your package, get immediate compile errors, think your package is broken
- Users have wrong Firebase SDK version, get subtle runtime errors
- Your package's asmdef references Firebase assemblies that may or may not exist
- CI/CD pipeline cannot resolve dependencies automatically

**Warning signs:**
- GitHub issues: "does not compile after install"
- Users confused about installation order
- Different Firebase SDK versions cause different runtime behavior

**Prevention:**
1. **Assembly Definition references:** Your package's `.asmdef` file should reference Firebase.AI.dll but with `"autoReferenced": false` and an `#if` directive check. Use `FIREBASE_AI_AVAILABLE` scripting define that users set after installing Firebase.
2. **Runtime dependency check:** On initialization, check if `Firebase.AI.FirebaseAI` type is loadable via reflection. If not, throw a clear error: "Firebase AI Logic SDK not found. Please install it from [URL]."
3. **Documentation-driven dependency:** Since UPM cannot enforce this, make installation instructions crystal clear. Step 1: Install Firebase SDK. Step 2: Install this package. Step 3: Configure Firebase project.
4. **Version compatibility table:** Document which versions of your package work with which Firebase SDK versions.
5. **Consider shipping Firebase AI Logic source within your package:** The Firebase AI Logic C# source is Apache 2.0 licensed (as seen in the file headers). You could include it directly, avoiding the dependency problem entirely. However, this means you own updates. Evaluate this tradeoff carefully.

**Phase relevance:** UPM packaging phase. Must be decided before first public release.

---

## Moderate Pitfalls

Mistakes that cause delays, user frustration, or technical debt.

---

### Pitfall 9: Function Call Response Timing — Blocking the Conversation

**What goes wrong:** When the model issues a `LiveSessionToolCall`, it expects a `FunctionResponsePart` back before it will continue generating. If the developer's function handler is slow (e.g., makes a network call, queries a database, or does a physics raycast that takes a few frames), the model sits waiting. The user hears silence. If the handler takes more than a few seconds, the model may time out the function call and issue a `LiveSessionToolCallCancellation`.

**Why it happens:** The Gemini Live protocol is synchronous for function calls — the model pauses generation until it gets the function response. Developers think of function calls as fire-and-forget events.

**Consequences:**
- Awkward silence during function execution
- Model cancels function calls that take too long
- User thinks the AI is broken during complex function calls

**Warning signs:**
- AI stops talking for several seconds then resumes
- `LiveSessionToolCallCancellation` messages appearing
- Function calls work in testing (fast) but fail in production (slow)

**Prevention:**
1. **Fast handlers only:** Function handlers should return immediately with pre-computed or cached data. If the function needs async work, return a placeholder ("looking that up...") and send a follow-up.
2. **Timeout documentation:** Document the expected response time for function calls. Test with artificial delays to find the model's cancellation threshold.
3. **Cancellation handling:** Implement `LiveSessionToolCallCancellation` handling — when the model cancels a function call, stop the pending work and log it.
4. **Queued function calls:** The model may issue multiple function calls in a single `LiveSessionToolCall`. All must be responded to, and the FunctionResponsePart includes the `Id` field for correlation. Handle batch responses.

**Phase relevance:** Function calling phase.

---

### Pitfall 10: Microphone API Platform Differences

**What goes wrong:** `UnityEngine.Microphone.Start()` behaves differently across platforms:
- On Windows/Mac, it returns an AudioClip immediately and begins recording.
- On Android, it requires `RECORD_AUDIO` permission — first call may prompt the user, and recording does not start until permission is granted.
- On iOS, `NSMicrophoneUsageDescription` must be in Info.plist or the app crashes.
- On WebGL, `Microphone.Start()` is not supported at all.
- Sample rate support varies: some devices do not support 16kHz and silently use a different rate.
- `Microphone.GetPosition()` can return 0 indefinitely if the device's microphone is in use by another app.

**Why it happens:** Unity's Microphone API abstracts platform differences imperfectly.

**Consequences:**
- "Works on my machine" — breaks on target platforms
- Permission dialogs at unexpected times in gameplay
- Silent failure: Microphone.Start succeeds but GetPosition never advances
- Wrong sample rate audio sent to Gemini, causing garbled transcription

**Warning signs:**
- `Microphone.devices` returns empty array
- `Microphone.GetPosition()` stuck at 0
- Audio captures fine in editor but fails on device

**Prevention:**
1. **Check `Microphone.devices` before starting:** If empty, surface a user-visible error.
2. **Verify actual sample rate:** After `Microphone.Start()`, check the returned `AudioClip.frequency`. If it doesn't match 16000, implement resampling (linear interpolation is sufficient for voice).
3. **Android permission flow:** Use Unity's `Permission.RequestUserPermission("android.permission.RECORD_AUDIO")` and wait for grant before initializing the microphone.
4. **GetPosition polling guard:** If `Microphone.GetPosition()` returns 0 for more than 1 second after starting, report an error rather than silently sending empty audio.
5. **Document platform support:** Explicitly list which platforms are supported in v1 (desktop-first per PROJECT.md).

**Phase relevance:** Audio capture phase. Can defer mobile-specific handling per "Out of Scope" but must handle desktop gracefully.

---

### Pitfall 11: PacketAssembler Text/Audio Synchronization Drift

**What goes wrong:** The Gemini Live API sends text and audio in separate response chunks. Text may arrive before, after, or interleaved with audio chunks. The `LiveSessionContent` includes `InputTranscription` and `OutputTranscription` fields (LiveSessionResponse.cs, lines 187-198) which are explicitly documented as "independent to the Content, and doesn't imply any ordering between them." If the PacketAssembler assumes text and audio arrive in lockstep, subtitles will be out of sync with speech.

**Why it happens:** The Gemini Live stream processes text and audio generation in parallel internally. Network packets may be reordered or batched differently.

**Consequences:**
- Subtitles appear before the AI starts speaking
- Subtitles lag behind speech by seconds
- Function calls (which come as text/toolCall) fire at the wrong time relative to audio
- Emote triggers ("wave") animate before the AI says the corresponding line

**Warning signs:**
- Subtitles flash ahead of voice
- Animation triggers fire at wrong times
- Timing works for short responses but drifts on long ones

**Prevention:**
1. **Do not assume ordering between text and audio chunks.** Build the PacketAssembler to buffer and align based on TurnComplete boundaries, not individual chunk arrival order.
2. **Use OutputTranscription for text sync:** If `outputAudioTranscription` is enabled in `LiveGenerationConfig`, use the transcription text for subtitles rather than trying to parse text responses. The transcription is derived from the audio and thus implicitly synchronized.
3. **Timestamp-based alignment:** Track elapsed playback time in the audio buffer. Associate text chunks with their position in the turn, not their arrival time.
4. **TurnComplete as sync point:** At TurnComplete, finalize all text/audio/emote data for that turn as one unit. Do not emit partial results to the user unless explicitly needed for low-latency display.

**Phase relevance:** PacketAssembler phase. This is a design problem, not just an implementation problem.

---

### Pitfall 12: Chirp 3 HD TTS as Separate HTTP Call — Latency Addition

**What goes wrong:** Chirp 3 HD TTS is a separate REST API call (Cloud Text-to-Speech), not integrated into the Gemini Live session. When using Chirp TTS mode, the pipeline is: Gemini Live generates text -> your code receives text -> your code calls Chirp TTS HTTP endpoint -> Chirp returns audio -> you play audio. Each HTTP request adds 200-800ms latency. For a multi-sentence response, this means either waiting for the entire text before calling TTS (high latency) or making multiple TTS calls (complexity + latency per chunk).

**Why it happens:** Gemini Live's built-in voices (Puck, Kore, Aoede, Charon, Fenrir) are synthesized server-side and streamed as audio inline. Chirp 3 HD is a completely separate Google Cloud service.

**Consequences:**
- Gemini native voice: <500ms time-to-first-audio
- Chirp TTS path: 1-3 seconds time-to-first-audio
- User perceives Chirp path as "laggy" or "broken"
- Multiple concurrent TTS requests can overwhelm API quota

**Warning signs:**
- Long silence before AI starts speaking (Chirp path only)
- Works fine with Gemini native voices
- TTS API rate limiting errors under load

**Prevention:**
1. **Sentence-level chunking:** Split text at sentence boundaries and fire TTS requests for each sentence as soon as it arrives. Start playing sentence 1 audio while requesting sentence 2.
2. **Request pipelining:** Send TTS requests in parallel for multiple sentences (up to API quota limits).
3. **Set expectations in docs:** Document that Chirp TTS path has higher latency than Gemini native audio. Recommend Gemini native voices for low-latency use cases.
4. **Pre-buffer text:** Wait until you have at least one complete sentence before requesting TTS, rather than sending fragments.
5. **Cache common phrases:** If persona has greetings or frequently used phrases, pre-generate TTS audio at startup.

**Phase relevance:** Chirp TTS phase. Can be built after Gemini native audio path is working.

---

### Pitfall 13: SetupComplete Response is Silently Swallowed

**What goes wrong:** The SDK's `LiveSessionResponse.FromJson` (line 126-129) returns `null` for `setupComplete` responses, and the `ReceiveAsync` loop (line 323) skips null responses with `if (response != null)`. This means the initial session setup acknowledgment is invisible to the consumer. If your code starts sending audio before receiving setupComplete, the server may reject or ignore the audio.

**Why it happens:** The SDK design choice is to hide the setup handshake from the consumer. But the timing matters: there is a window between `ConnectAsync()` returning and the server being ready to receive media.

**Consequences:**
- First few hundred milliseconds of audio may be lost
- Intermittent: depends on network latency whether setup completes before first send
- Session appears to work but initial words are missing

**Warning signs:**
- First word or two of user speech is not heard by the AI
- Works locally (fast connection) but fails remotely (slow connection)
- Adding a small delay before first send "fixes" it

**Prevention:**
1. **Add a small delay (200-500ms) after ConnectAsync before starting audio capture.** This is a pragmatic workaround.
2. **Or: Modify the receive loop to detect the setupComplete state** — since the SDK swallows it, you could fork or wrap the SDK to expose a "ready" signal. However, forking the SDK has maintenance costs.
3. **Or: Start ReceiveAsync immediately after ConnectAsync** — the first null-yielded iteration corresponds to setupComplete. After the first ReceiveAsync iteration starts successfully, begin audio capture.

**Phase relevance:** Core session management phase. Subtle timing issue that should be tested early.

---

### Pitfall 14: ScriptableObject Serialization of Sensitive Data

**What goes wrong:** PersonaConfig ScriptableObjects are serialized to disk and included in builds. If developers put API keys, secrets, or sensitive configuration in ScriptableObjects, they end up in the built player's data, readable by anyone who decompiles the build.

**Why it happens:** ScriptableObjects are convenient for configuration. Developers instinctively put all config in one place.

**Consequences:**
- API keys exposed in shipped builds
- Firebase project credentials compromised
- Chirp TTS API key leaked

**Warning signs:**
- API key fields in ScriptableObject inspector
- Credentials in version control

**Prevention:**
1. **Never store API keys in ScriptableObjects.** PersonaConfig should contain personality, voice selection, model name, archetype, traits — but NOT credentials.
2. **Firebase credentials come from `google-services.json` / `GoogleService-Info.plist`** — these are handled by the Firebase SDK setup process, not by your package.
3. **Chirp TTS API key:** Use a separate configuration mechanism (e.g., `Resources.Load` of a runtime-only asset, environment variable, or server-side proxy). Document this clearly.
4. **Code review gate:** Add a comment in PersonaConfig: "// DO NOT add API keys or secrets to this ScriptableObject."

**Phase relevance:** Configuration/ScriptableObject design phase.

---

## Minor Pitfalls

Mistakes that cause annoyance but are fixable without major rework.

---

### Pitfall 15: AudioSource 3D Spatial Settings Override

**What goes wrong:** When a developer attaches an AudioSource for AI voice playback and enables 3D spatialization (e.g., for a character in a 3D world), the default 3D settings may cause the AI voice to be inaudible if the AudioListener is far from the character, or to pan unexpectedly as the camera moves.

**Prevention:** Document recommended AudioSource settings for voice AI playback. Suggest `spatialBlend = 0` (2D) for debugging, then tune 3D settings per game. The PersonaSession component should not force any AudioSource settings — leave that to the developer.

**Phase relevance:** Sample scene / documentation phase.

---

### Pitfall 16: Unity Microphone Ring Buffer Overflow

**What goes wrong:** `Microphone.Start(null, true, bufferLengthSec, 16000)` creates a looping AudioClip. If `Microphone.GetPosition()` wraps around the buffer and the consumer has not read the old data, audio is lost. With a small buffer (e.g., 1 second) and infrequent polling, you lose audio data.

**Prevention:** Use a buffer of at least 10 seconds. Poll `Microphone.GetPosition()` every frame in Update(). Track the last read position and copy new samples since last read. Handle the wrap-around case (position < lastPosition means it looped).

**Phase relevance:** Audio capture phase.

---

### Pitfall 17: Assembly Definition File Structure for UPM

**What goes wrong:** UPM packages require assembly definition files (`.asmdef`) to define compilation boundaries. Common mistakes:
- Missing `.asmdef` causes scripts to compile in the default Assembly-CSharp, not the package assembly
- Test assemblies reference runtime assemblies incorrectly
- Editor-only code mixed with runtime code
- Missing `"autoReferenced": true` causes user scripts to not see the package API

**Prevention:**
- Create at least three asmdef files: `Runtime`, `Editor`, `Tests`
- Runtime asmdef: references Firebase.AI, UnityEngine
- Editor asmdef: references Runtime asmdef, UnityEditor
- Tests asmdef: references Runtime asmdef, add test framework references
- Use `"includePlatforms": ["Editor"]` on Editor asmdef

**Phase relevance:** UPM packaging phase.

---

### Pitfall 18: Dispose Pattern — IAsyncDisposable vs IDisposable

**What goes wrong:** `LiveSession` implements `IDisposable` but its cleanup is inherently async (closing a WebSocket). The `Dispose()` method calls `_clientWebSocket.CloseAsync()` but does not await it (LiveSession.cs, line 66). This means the WebSocket close handshake may not complete before the object is garbage collected, potentially leaking the connection.

**Prevention:** In PersonaSession, do not rely solely on `Dispose()`. Instead:
1. `await session.CloseAsync(cancellationToken)` explicitly before disposing
2. Then call `session.Dispose()` for cleanup
3. Handle the case where CloseAsync throws (WebSocket already closed)

**Phase relevance:** Core session management phase.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Core session management | Threading (#1), ReceiveAsync lifecycle (#2, #3), WebSocket lifecycle (#7), SetupComplete timing (#13), Dispose (#18) | Build MainThreadDispatcher first. Single receive loop with ConcurrentQueue. CancellationTokenSource tied to MonoBehaviour lifecycle. |
| Audio capture | Sample rate mismatch (#4), Microphone platform differences (#10), Ring buffer overflow (#16) | Force 16kHz, verify actual rate, 10s buffer, poll every frame |
| Audio playback | Streaming playback race (#5), Sample rate mismatch (#4) | OnAudioFilterRead ring buffer approach. Create AudioClip at correct output sample rate. |
| PacketAssembler | Text/audio sync drift (#11) | Do not assume ordering. Use TurnComplete as sync boundary. |
| Function calling | Response timing (#9), Cancellation handling | Fast handlers, handle batch calls, respect function IDs |
| Chirp TTS | Latency addition (#12) | Sentence-level chunking, pipelining, document latency tradeoff |
| UPM packaging | Firebase dependency (#8), asmdef structure (#17), secrets (#14) | Documentation-driven install, version compat table, separate credentials |
| GC/Performance | Base64 overhead (#6) | Chunk sends at 100-250ms intervals, pool byte arrays, profile early |

---

## Sources

- **Firebase AI Logic SDK source code** (Apache 2.0, directly in project at `Assets/Firebase/FirebaseAI/`): PRIMARY source for pitfalls #1-3, #6-7, #9, #13, #18. Confidence: HIGH.
- **LiveSession.cs** lines 252-268 (ConvertTo16BitPCM), 287-339 (ReceiveAsync), 85-108 (InternalSendBytesAsync): Direct code analysis.
- **LiveSessionResponse.cs** lines 62-76 (Audio property), 89-104 (ConvertBytesToFloat), 126-129 (setupComplete handling), 187-198 (Transcription independence): Direct code analysis.
- **LiveGenerationConfig.cs** lines 86-97 (SpeechConfig, responseModalities): Direct code analysis.
- **Unity Microphone API behavior**: Training data knowledge. Confidence: MEDIUM (well-established API, unlikely to have changed significantly).
- **Unity AudioClip/AudioSource streaming limitations**: Training data knowledge. Confidence: MEDIUM.
- **UPM packaging requirements**: Training data knowledge. Confidence: MEDIUM.
- **Gemini Live protocol details (sample rates, turn semantics)**: Training data + inferred from SDK code. Confidence: MEDIUM.
- **Chirp 3 HD TTS latency characteristics**: Training data. Confidence: LOW (should be validated with actual API calls during implementation).
