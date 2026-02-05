# Phase 3: Synchronization - Research

**Researched:** 2026-02-05
**Domain:** Real-time text/audio/event correlation from Gemini Live API into unified SyncPackets
**Confidence:** HIGH

## Summary

Phase 3 builds the PacketAssembler layer between the existing Gemini Live receive loop (PersonaSession.ProcessResponse) and the developer-facing API. Currently, ProcessResponse routes raw data directly to AudioPlayback and fires individual events (OnTextReceived, OnOutputTranscription, OnAISpeakingStarted, etc). PacketAssembler replaces this scatter-shot dispatch with a single unified OnSyncPacket event that delivers correlated text, audio, and function call data.

The critical technical insight from this research is that **Gemini Live's serverContent messages contain audio and transcription as independent, unordered fields within the same message type**. The official API reference states: "The transcription is sent independently of the other server messages and there is no guaranteed ordering." A single `LiveSessionContent` from the SDK can contain any combination of: `modelTurn` (with audio InlineDataParts and/or TextParts), `outputTranscription`, `inputTranscription`, `turnComplete`, and `interrupted`. These fields arrive in separate WebSocket messages, not combined into one. The PacketAssembler must buffer incoming chunks and assemble them into coherent packets at sentence boundaries rather than assuming any ordering relationship.

The C++ reference implementation at `/home/cachy/workspaces/projects/persona/src/voice/packet_assembler.cpp` provides a proven pattern: buffer text, detect sentence/clause boundaries, and emit SyncPackets keyed on audio timing. For the Gemini native audio path (Phase 3 scope), audio and transcription arrive together from the same Gemini response stream. The ISyncDriver interface is designed for future extensibility (Chirp TTS in Phase 5, Face Animation later) without architectural changes.

**Primary recommendation:** Build PacketAssembler as a plain C# class (not MonoBehaviour) that receives raw events from ProcessResponse on the main thread, buffers them per-turn with sentence boundary detection, and emits SyncPackets via a callback. Use `OutputTranscription` text for subtitle content (it is derived from the audio and thus implicitly synchronized) and audio data from `AudioAsFloat` for the PCM payload. The ISyncDriver interface controls release timing -- for Phase 3 (Gemini native only), packets release immediately on assembly.

## Standard Stack

### Core (Already in Project)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Firebase.AI (LiveSessionResponse) | 13.7.0 | Source of text, audio, transcription, tool call events | SDK provides AudioAsFloat, OutputTranscription, LiveSessionToolCall -- all data the assembler needs |
| System.Collections.Generic | .NET Standard 2.1 | Queue, List for internal buffering | Built-in, zero-allocation-path friendly |
| System.Text | .NET Standard 2.1 | StringBuilder for sentence boundary buffering | Built-in, avoids string concatenation allocations |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| MainThreadDispatcher | Phase 1 (existing) | Ensure SyncPacket events fire on main thread | All packet callbacks dispatched through this |
| AudioPlayback | Phase 2 (existing) | Developer routes audio from SyncPacket to playback | SyncPacket carries PCM data; developer calls EnqueueAudio |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Plain C# class for PacketAssembler | MonoBehaviour | PacketAssembler has no Unity lifecycle needs -- it is pure data processing. MonoBehaviour adds overhead and forces GameObject attachment |
| StringBuilder for text buffering | string concatenation | StringBuilder avoids O(n^2) allocation from repeated += on strings during a turn |
| Queue<SyncPacket> for pending packets | List<SyncPacket> | Queue has O(1) dequeue; packets are always consumed in order |

**Installation:**
No new packages to install. All infrastructure is already in the project from Phases 1 and 2.

## Architecture Patterns

### Recommended Project Structure (additions to existing package)

```
Packages/com.google.ai-embodiment/Runtime/
  PersonaSession.cs          # MODIFIED: Route through PacketAssembler instead of direct dispatch
  PacketAssembler.cs         # NEW: Buffers and correlates text/audio/events into SyncPackets
  SyncPacket.cs              # NEW: Unified packet data type + SyncPacketType enum
  ISyncDriver.cs             # NEW: Interface for sync timing control
  AudioPlayback.cs           # UNMODIFIED (developer routes audio from SyncPacket)
  AudioCapture.cs            # UNMODIFIED
  AudioRingBuffer.cs         # UNMODIFIED
```

### Pattern 1: SyncPacket as Readonly Struct

**What:** SyncPacket is an immutable value type containing all data a developer needs for one "moment" of AI output: subtitle text, PCM audio, function call events, and a turn ID.

**When to use:** Every time PacketAssembler emits a correlated chunk to the developer.

**Design (Claude's Discretion per CONTEXT.md):**

```csharp
// Source: Derived from C++ reference (sync_packet.h) + CONTEXT.md decisions
namespace AIEmbodiment
{
    /// <summary>
    /// Discriminates the type of content in a SyncPacket.
    /// </summary>
    public enum SyncPacketType
    {
        /// <summary>Contains subtitle text and/or PCM audio data.</summary>
        TextAudio,

        /// <summary>Contains a function call event from the AI.</summary>
        FunctionCall
    }

    /// <summary>
    /// Unified packet correlating text, audio, and events from an AI response turn.
    /// Subscribe to PersonaSession.OnSyncPacket to receive these.
    /// </summary>
    public readonly struct SyncPacket
    {
        /// <summary>Packet type discriminator.</summary>
        public SyncPacketType Type { get; }

        /// <summary>Turn identifier for grouping packets within one AI response.</summary>
        public int TurnId { get; }

        /// <summary>Sequence number within the turn (0-based, ascending).</summary>
        public int Sequence { get; }

        /// <summary>
        /// Subtitle text for this packet segment. Empty for FunctionCall packets.
        /// Derived from OutputTranscription (synchronized with audio).
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// PCM audio data at 24kHz mono (float[]). Null for FunctionCall packets.
        /// Developer routes this to AudioPlayback.EnqueueAudio.
        /// </summary>
        public float[] Audio { get; }

        /// <summary>
        /// Function call name. Empty for TextAudio packets.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Function call arguments. Null for TextAudio packets.
        /// </summary>
        public IReadOnlyDictionary<string, object> FunctionArgs { get; }

        /// <summary>
        /// Function call ID for response correlation. Null for TextAudio packets.
        /// </summary>
        public string FunctionId { get; }

        /// <summary>
        /// True if this is the last packet in the turn.
        /// </summary>
        public bool IsTurnEnd { get; }
    }
}
```

**Key design decisions:**
- `readonly struct` follows Firebase SDK convention (ModelContent, LiveSessionContent are all readonly struct)
- Type discriminator enum avoids polymorphism overhead; CONTEXT.md decision: "packet has a type field"
- Function calls get dedicated packets per CONTEXT.md: "not mixed into text/audio packets"
- Turn ID per CONTEXT.md: "developers can group content per AI response turn"
- Audio as `float[]` -- same format AudioPlayback.EnqueueAudio already accepts (24kHz mono)
- Text from OutputTranscription, not from response.Text -- transcription is derived from audio and better synchronized

### Pattern 2: PacketAssembler as Event-Driven Processor

**What:** PacketAssembler receives raw events from ProcessResponse, buffers text at sentence boundaries, and emits SyncPackets. It is a plain C# class (not MonoBehaviour) owned by PersonaSession.

**When to use:** Always -- it sits between the receive loop and the developer API.

**Data flow:**

```
Background Thread:                  Main Thread:
ReceiveAsync                        MainThreadDispatcher.Update()
  |                                   |
  v                                   v
ProcessResponse()              PacketAssembler.ProcessXxx()
  |                                   |
  +-- Enqueue to dispatcher           +-- Buffer text/audio
                                      +-- Detect sentence boundary
                                      +-- Emit SyncPacket via callback
                                      |
                                      v
                                 PersonaSession.OnSyncPacket
                                      |
                                      v
                                 Developer code
```

**Critical: PacketAssembler runs entirely on the main thread.** ProcessResponse (background thread) enqueues raw data to MainThreadDispatcher. PacketAssembler methods are called from the dispatcher callbacks. This means no thread safety concerns inside PacketAssembler -- all calls are serialized on the main thread.

### Pattern 3: Sentence Boundary Buffering

**What:** Buffer incoming transcription text until a sentence-like boundary is detected, then emit a SyncPacket with the complete sentence and its corresponding accumulated audio.

**When to use:** For all TextAudio packets in the Gemini native audio path.

**Boundary detection algorithm (Claude's Discretion per CONTEXT.md):**

```csharp
// Sentence boundaries: . ? ! followed by whitespace or end of string
// Clause boundaries:   , ; : followed by whitespace (lower priority)
// Time-based fallback: if no boundary found within 500ms and 20+ chars buffered,
//                      flush at last word boundary
private int FindSentenceBoundary(string text, int startIndex)
{
    int lastSentence = -1;
    for (int i = startIndex; i < text.Length; i++)
    {
        char c = text[i];
        if (c == '.' || c == '?' || c == '!')
        {
            // Valid boundary if followed by space or end of text
            if (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]))
            {
                lastSentence = i + 1;
            }
        }
    }
    return lastSentence; // -1 if no boundary found
}
```

**Rationale from C++ reference:** The C++ PacketAssembler uses `findLastSafeFlushPoint` with the same punctuation set (`. ? ! , ; :`) and a 500ms / 20-char time-based fallback. This produces natural subtitle display and gives predictable points for animation event alignment.

### Pattern 4: ISyncDriver Interface

**What:** An interface that controls WHEN assembled SyncPackets are released to the developer. Any component can register as a sync driver.

**When to use:** Gemini native audio path has no external driver (immediate release). Chirp TTS (Phase 5) and future Face Animation will register as drivers to gate packet release.

```csharp
// Source: CONTEXT.md decision -- ISyncDriver interface
namespace AIEmbodiment
{
    /// <summary>
    /// Controls when assembled SyncPackets are released to the developer.
    /// The highest-latency driver wins: packets are held until the slowest
    /// registered driver signals readiness.
    /// </summary>
    public interface ISyncDriver
    {
        /// <summary>
        /// Called when a SyncPacket is ready for release.
        /// The driver may hold it and release later (e.g., waiting for TTS audio).
        /// </summary>
        void OnPacketReady(SyncPacket packet);

        /// <summary>
        /// Register a callback that the driver calls when it's ready to release packets.
        /// </summary>
        void SetReleaseCallback(Action<SyncPacket> releaseCallback);

        /// <summary>
        /// The estimated pipeline latency of this driver in milliseconds.
        /// Used to determine which driver is the highest-latency (and thus the pacer).
        /// </summary>
        int EstimatedLatencyMs { get; }
    }
}
```

**Phase 3 behavior:** No external driver registered. PacketAssembler emits packets immediately through the release callback. The driver registration path exists but is not exercised until Phase 5.

### Pattern 5: Turn ID Generation

**What:** Simple incrementing integer counter that resets when PersonaSession connects. Each AI response turn gets a unique ID.

**When to use:** Developers use turn ID to group SyncPackets that belong to the same AI response.

```csharp
// Claude's Discretion per CONTEXT.md: "Turn ID generation strategy"
private int _nextTurnId = 0;

public void StartTurn()
{
    _currentTurnId = _nextTurnId++;
    _sequence = 0;
    // ... reset buffers
}
```

**Rationale:** Simple integer is sufficient. UUID would be overkill -- turns are sequential within a session and the ID only needs to be unique within that session lifetime.

### Anti-Patterns to Avoid

- **Assuming text and audio arrive in the same message:** The Gemini Live API sends outputTranscription independently from audio content. They may arrive in different WebSocket messages. Buffer both and correlate.
- **Assuming ordering between transcription and audio:** Official API docs: "The transcription is sent independently... there is no guaranteed ordering." Do NOT assume transcription arrives before, after, or simultaneously with the audio it describes.
- **Using response.Text for subtitles in audio mode:** When ResponseModality is Audio, `response.Text` is empty. Use `OutputTranscription.Text` for subtitle content. OutputTranscription is derived FROM the audio and thus better represents what the AI actually said.
- **Emitting packets on every single chunk:** Small chunks produce flickering subtitles. Buffer to sentence boundaries for stable, readable text display.
- **Blocking the main thread in PacketAssembler:** All PacketAssembler processing must be lightweight. No async calls, no heavy computation in the assembly path.
- **Making SyncPacket a class:** Value type semantics prevent accidental mutation and reduce GC pressure. Follow Firebase SDK convention of readonly struct for data types.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| PCM audio format conversion | Custom byte-to-float conversion | `LiveSessionResponse.AudioAsFloat` (SDK) | Already decoded to float[] at 24kHz mono by SDK |
| Transcription text extraction | Parse raw JSON for transcript text | `LiveSessionContent.OutputTranscription.Value.Text` | SDK parses this from serverContent JSON |
| Function call parsing | Manual JSON extraction of function names/args | `LiveSessionToolCall.FunctionCalls` list of `FunctionCallPart` | SDK provides Name, Args, Id fields already parsed |
| Thread marshaling | Custom synchronization | `MainThreadDispatcher.Enqueue` (Phase 1) | Already built and proven |
| Audio playback pipeline | New playback system | `AudioPlayback.EnqueueAudio` (Phase 2) | Developer routes audio from SyncPacket to existing pipeline |
| Text chunking for TTS | Custom sentence splitter | Sentence boundary detection from C++ reference | Proven pattern with fallback timing; adapts directly |

**Key insight:** PacketAssembler does not generate any new data -- it correlates and packages data that the SDK and existing components already provide. The value is in the correlation and timing, not in data transformation.

## Common Pitfalls

### Pitfall 1: OutputTranscription Arrives Independently from Audio

**What goes wrong:** Developer assumes that when audio data arrives in a LiveSessionContent, the OutputTranscription field in that same message contains the text for that audio. In reality, transcription may arrive in a completely different message.
**Why it happens:** The Gemini Live API reference explicitly states: "The transcription is sent independently of the other server messages and there is no guaranteed ordering."
**How to avoid:** Buffer transcription text and audio data separately. Correlate them by accumulation within a turn, not by co-occurrence in a single message. Use sentence boundary detection on transcription text to determine when to emit a SyncPacket, and pair it with whatever audio has accumulated since the last emit.
**Warning signs:** Subtitles appear at wrong times; empty text in SyncPackets that have audio; text in packets with no audio.

### Pitfall 2: ProcessResponse Refactoring Breaks Existing Events

**What goes wrong:** Phase 3 modifies ProcessResponse to route through PacketAssembler. If existing events (OnTextReceived, OnAISpeakingStarted, OnOutputTranscription, etc.) are removed or broken, code that depends on Phase 2's API breaks.
**Why it happens:** The instinct is to replace all direct event firing with PacketAssembler routing. But some events (OnStateChanged, OnInputTranscription, OnInterrupted, OnTurnComplete) are orthogonal to sync packets.
**How to avoid:** Keep ALL existing PersonaSession events functional. Add OnSyncPacket as a NEW event alongside them. PacketAssembler is an additional layer, not a replacement. The existing events remain for backward compatibility and for use cases that don't need packet correlation.
**Warning signs:** Existing developer code that subscribes to OnTextReceived or OnAISpeakingStarted stops working after Phase 3.

### Pitfall 3: Sentence Buffer Never Flushes at Turn End

**What goes wrong:** Text accumulates in the sentence buffer waiting for a sentence boundary. TurnComplete arrives but the last sentence fragment (e.g., "How about that") never had terminating punctuation. The fragment is lost.
**Why it happens:** Sentence boundary detection only triggers on punctuation. If the AI's final words lack punctuation, the buffer retains undelivered text.
**How to avoid:** When TurnComplete is received, force-flush ALL remaining buffered text and audio as a final SyncPacket with `IsTurnEnd = true`, regardless of whether a sentence boundary was found.
**Warning signs:** Last few words of AI speech are missing from subtitles. Works for well-punctuated responses but fails for casual speech.

### Pitfall 4: Audio Accumulation Mismatch with Text Buffering

**What goes wrong:** Audio arrives continuously but text is emitted at sentence boundaries. If audio accumulation is not properly tracked between sentence emissions, packets either contain too much audio (duplicated playback) or too little audio (gaps).
**Why it happens:** Audio chunks arrive many times per sentence. Each chunk must be accumulated and then drained when a sentence packet is emitted.
**How to avoid:** Maintain a `List<float[]>` of pending audio chunks. When a sentence boundary triggers packet emission, move ALL accumulated audio chunks into the packet and clear the pending list. The next sentence starts accumulating fresh.
**Warning signs:** Audio repeats or has gaps between sentence packets. Total audio duration across all packets doesn't match the original stream.

### Pitfall 5: Tool Calls Arrive as Separate Message Type

**What goes wrong:** Developer expects function calls to arrive within LiveSessionContent alongside audio/text. But `LiveSessionToolCall` is a completely separate message type (`ILiveSessionMessage` union type in the SDK). ProcessResponse must handle both `is LiveSessionContent` and `is LiveSessionToolCall` branches.
**Why it happens:** The Gemini Live API sends tool calls as a `toolCall` JSON key at the top level, separate from `serverContent`. The SDK deserializes this as `LiveSessionToolCall`, not `LiveSessionContent`.
**How to avoid:** PacketAssembler receives function call data through a separate method (e.g., `AddFunctionCall`) that creates a dedicated FunctionCall-type SyncPacket. ProcessResponse routes `LiveSessionToolCall` to this method. Phase 3 stubs this with a placeholder; Phase 4 fully implements it.
**Warning signs:** Function calls are silently dropped. Tool call events never reach the developer through OnSyncPacket.

### Pitfall 6: Interrupted Turn Leaves Stale Data in Buffers

**What goes wrong:** User interrupts (barge-in), Gemini sends `interrupted: true`, but PacketAssembler still has buffered text and audio from the interrupted turn. If not cleared, this stale data leaks into the next turn's packets.
**Why it happens:** The interrupt can arrive mid-sentence, mid-audio-chunk. Buffer state is inconsistent.
**How to avoid:** When an interrupted signal arrives, call PacketAssembler.CancelTurn() which clears ALL buffers: pending text, pending audio, sentence buffer. The existing AudioPlayback.ClearBuffer() call in PersonaSession handles the playback side; PacketAssembler.CancelTurn() handles the assembly side.
**Warning signs:** After barge-in, next AI response starts with a fragment of the interrupted response's text.

## Code Examples

### Example 1: PacketAssembler Core Flow

```csharp
// Source: Designed from CONTEXT.md decisions + C++ reference pattern
public class PacketAssembler
{
    private Action<SyncPacket> _packetCallback;
    private ISyncDriver _syncDriver;

    // Turn state
    private int _currentTurnId;
    private int _nextTurnId;
    private int _sequence;
    private bool _turnActive;

    // Text buffering (sentence boundary)
    private readonly StringBuilder _textBuffer = new();
    private int _textCursor; // Position of last emitted text

    // Audio accumulation
    private readonly List<float[]> _pendingAudio = new();

    // Timing
    private float _lastFlushTime;
    private const float FlushTimeoutSeconds = 0.5f;
    private const int MinFlushChars = 20;

    public void SetPacketCallback(Action<SyncPacket> callback)
    {
        _packetCallback = callback;
    }

    public void StartTurn()
    {
        _currentTurnId = _nextTurnId++;
        _sequence = 0;
        _turnActive = true;
        _textBuffer.Clear();
        _textCursor = 0;
        _pendingAudio.Clear();
        _lastFlushTime = Time.time;
    }

    public void AddTranscription(string text)
    {
        if (!_turnActive || string.IsNullOrEmpty(text)) return;

        _textBuffer.Append(text);
        TryFlush();
    }

    public void AddAudio(float[] samples)
    {
        if (!_turnActive || samples == null || samples.Length == 0) return;

        _pendingAudio.Add(samples);
    }

    public void FinishTurn()
    {
        if (!_turnActive) return;

        // Force-flush remaining text and audio
        FlushAll(isTurnEnd: true);
        _turnActive = false;
    }

    public void CancelTurn()
    {
        _turnActive = false;
        _textBuffer.Clear();
        _textCursor = 0;
        _pendingAudio.Clear();
    }

    private void TryFlush()
    {
        string text = _textBuffer.ToString();
        int boundary = FindSentenceBoundary(text, _textCursor);

        if (boundary > _textCursor)
        {
            EmitTextAudioPacket(text.Substring(_textCursor, boundary - _textCursor));
            _textCursor = boundary;
            _lastFlushTime = Time.time;
        }
        else if (Time.time - _lastFlushTime >= FlushTimeoutSeconds
                 && text.Length - _textCursor >= MinFlushChars)
        {
            // Time-based fallback: flush at last word boundary
            int lastSpace = text.LastIndexOf(' ', text.Length - 1, text.Length - _textCursor);
            if (lastSpace > _textCursor)
            {
                EmitTextAudioPacket(text.Substring(_textCursor, lastSpace - _textCursor));
                _textCursor = lastSpace + 1; // Skip the space
                _lastFlushTime = Time.time;
            }
        }
    }

    private void EmitTextAudioPacket(string text)
    {
        // Merge accumulated audio chunks into single array
        float[] mergedAudio = MergeAudioChunks();

        var packet = new SyncPacket(
            type: SyncPacketType.TextAudio,
            turnId: _currentTurnId,
            sequence: _sequence++,
            text: text.Trim(),
            audio: mergedAudio,
            isTurnEnd: false
        );

        ReleasePacket(packet);
    }

    private void ReleasePacket(SyncPacket packet)
    {
        if (_syncDriver != null)
        {
            _syncDriver.OnPacketReady(packet);
        }
        else
        {
            // No driver registered -- release immediately (Gemini native path)
            _packetCallback?.Invoke(packet);
        }
    }
}
```

### Example 2: PersonaSession Integration Point

```csharp
// Source: Modification to existing PersonaSession.ProcessResponse
// Shows how PacketAssembler slots into the existing receive loop

// NEW field on PersonaSession:
private PacketAssembler _packetAssembler;

// NEW event on PersonaSession:
public event Action<SyncPacket> OnSyncPacket;

// In Connect(), after SetState(Connected):
_packetAssembler = new PacketAssembler();
_packetAssembler.SetPacketCallback(packet =>
{
    OnSyncPacket?.Invoke(packet);
});

// In ProcessResponse (background thread), the EXISTING enqueue pattern:
if (response.Message is LiveSessionContent content)
{
    // Route audio to PacketAssembler (via main thread)
    var audioChunks = response.AudioAsFloat;
    if (audioChunks != null && audioChunks.Count > 0)
    {
        foreach (var chunk in audioChunks)
        {
            // Capture chunk in local to avoid closure issues
            var localChunk = chunk;
            MainThreadDispatcher.Enqueue(() => _packetAssembler.AddAudio(localChunk));
        }
    }

    // Route output transcription to PacketAssembler (via main thread)
    if (content.OutputTranscription.HasValue)
    {
        string transcript = content.OutputTranscription.Value.Text;
        MainThreadDispatcher.Enqueue(() => _packetAssembler.AddTranscription(transcript));
    }

    // TurnComplete -> finish the turn
    if (content.TurnComplete)
    {
        MainThreadDispatcher.Enqueue(() => _packetAssembler.FinishTurn());
    }

    // Interrupted -> cancel the turn
    if (content.Interrupted)
    {
        MainThreadDispatcher.Enqueue(() => _packetAssembler.CancelTurn());
    }

    // KEEP existing event firing (backward compatibility):
    // OnTextReceived, OnAISpeakingStarted, etc. remain unchanged
}
```

### Example 3: Developer Usage

```csharp
// Source: API design from CONTEXT.md -- single event subscription
public class SubtitleDisplay : MonoBehaviour
{
    [SerializeField] private PersonaSession _session;
    [SerializeField] private AudioPlayback _audioPlayback;
    [SerializeField] private TMPro.TextMeshProUGUI _subtitleText;

    private void OnEnable()
    {
        _session.OnSyncPacket += HandlePacket;
    }

    private void OnDisable()
    {
        _session.OnSyncPacket -= HandlePacket;
    }

    private void HandlePacket(SyncPacket packet)
    {
        switch (packet.Type)
        {
            case SyncPacketType.TextAudio:
                // Display subtitle
                _subtitleText.text = packet.Text;

                // Route audio to playback (developer controls this)
                if (packet.Audio != null && _audioPlayback != null)
                {
                    _audioPlayback.EnqueueAudio(packet.Audio);
                }
                break;

            case SyncPacketType.FunctionCall:
                // Handle function call (Phase 4)
                Debug.Log($"Function call: {packet.FunctionName}");
                break;
        }
    }
}
```

### Example 4: Sentence Boundary Detection

```csharp
// Source: Adapted from C++ reference (packet_assembler.cpp findLastSafeFlushPoint)
private static int FindSentenceBoundary(string text, int startIndex)
{
    int lastBoundary = -1;

    for (int i = startIndex; i < text.Length; i++)
    {
        char c = text[i];

        // Sentence-ending punctuation
        if (c == '.' || c == '?' || c == '!')
        {
            // Valid if followed by whitespace or end of text
            if (i + 1 >= text.Length)
            {
                lastBoundary = i + 1;
            }
            else if (char.IsWhiteSpace(text[i + 1]))
            {
                lastBoundary = i + 1;
            }
            // NOT a boundary if followed by non-whitespace
            // (handles "3.14", "U.S.A", etc.)
        }
    }

    return lastBoundary;
}
```

**Note:** The C++ reference also detects clause boundaries (`, ; :`) but the CONTEXT.md says "sentence-like boundaries" which implies sentence-level granularity. The time-based fallback (500ms / 20 chars) handles cases where long text arrives without punctuation, preventing subtitle freezing.

## Gemini Live Response Data Flow (Verified)

Understanding exactly how data arrives from the SDK is critical for PacketAssembler design.

### Message Types from SDK (verified against LiveSessionResponse.cs)

| SDK Type | JSON Key | Contains | Arrives When |
|----------|----------|----------|-------------|
| `LiveSessionContent` | `serverContent` | modelTurn (text/audio parts), outputTranscription, inputTranscription, turnComplete, interrupted | During AI response generation |
| `LiveSessionToolCall` | `toolCall` | List of FunctionCallPart (name, args, id) | When AI triggers a function call |
| `LiveSessionToolCallCancellation` | `toolCallCancellation` | List of function IDs to cancel | When AI cancels pending function calls |

### LiveSessionContent Fields (verified against SDK source)

A single `LiveSessionContent` message may contain ANY combination of:

| Field | Type | When Present |
|-------|------|-------------|
| `Content` (modelTurn) | `ModelContent?` | When AI generates content (audio InlineDataParts, TextParts) |
| `OutputTranscription` | `Transcription?` | When AI has transcription text for its audio output |
| `InputTranscription` | `Transcription?` | When server has transcribed user's audio input |
| `TurnComplete` | `bool` | When AI finishes its response turn |
| `Interrupted` | `bool` | When user barge-in interrupts the AI |

**Critical finding:** These fields arrive INDEPENDENTLY in separate WebSocket messages. The SDK's `LiveSessionContent.FromJson` parses all fields from a single `serverContent` JSON, but each WebSocket message typically contains only a subset. For example:
- Message 1: `{ serverContent: { modelTurn: { parts: [audio] } } }` -- audio only
- Message 2: `{ serverContent: { outputTranscription: { text: "Hello" } } }` -- transcription only
- Message 3: `{ serverContent: { modelTurn: { parts: [audio] } } }` -- more audio
- Message 4: `{ serverContent: { turnComplete: true } }` -- turn end

### Audio Data Path (verified against LiveSessionResponse.cs lines 62-104)

```
LiveSessionResponse.AudioAsFloat
  -> LiveSessionContent.Content.Parts
    -> Filter: OfType<InlineDataPart>().Where(part.MimeType.StartsWith("audio/pcm"))
      -> ConvertBytesToFloat: 16-bit PCM bytes -> float[] (clamped -1 to 1)

Output: IReadOnlyList<float[]> -- list of float arrays, each a chunk of 24kHz mono audio
```

### Transcription Data Path (verified against LiveSessionResponse.cs lines 294-314)

```
LiveSessionContent.OutputTranscription
  -> Transcription struct with .Text property
  -> Parsed from serverContent.outputTranscription.text JSON field

Output: string -- incremental text chunk (NOT the full transcript, just the latest addition)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| response.Text for subtitles | OutputTranscription.Text for subtitles | Firebase AI SDK 13.7.0 | OutputTranscription is derived from audio and thus implicitly synchronized; response.Text is empty when ResponseModality is Audio |
| Wait for TurnComplete then emit all | Stream SyncPackets at sentence boundaries | Design decision | Lower perceived latency for subtitle display |
| Single callback per data type | Single OnSyncPacket event with typed packets | Design decision | Developer subscribes to one event, gets everything |

**Deprecated/outdated:**
- Using `response.Text` for subtitles when in Audio mode: This returns empty string. Use `OutputTranscription.Text` instead.
- Buffering all text until TurnComplete: Creates perception of lag. Stream at sentence boundaries.

## Open Questions

1. **OutputTranscription chunk granularity**
   - What we know: OutputTranscription text arrives incrementally "streamed along with the audio." The SDK provides it as `Transcription.Text` per message.
   - What's unclear: How many characters arrive per transcription message. Is it word-by-word, sentence-by-sentence, or variable-length chunks? This affects how quickly sentence boundaries can be detected.
   - Recommendation: Assume variable-length chunks. The sentence boundary detection with time-based fallback handles all granularities. Validate with real API calls during integration testing.

2. **Audio-to-transcription timing correlation**
   - What we know: Official docs say transcription is "independent" with "no guaranteed ordering." The C++ reference uses chars-per-ms estimation (12.5 chars/sec based on ~150 words/min speaking rate).
   - What's unclear: In practice, for Gemini native audio, does transcription tend to arrive close to its corresponding audio, or can there be significant drift?
   - Recommendation: For Phase 3 (Gemini native), use a simple accumulation model: audio and text both accumulate, emit together at sentence boundaries. The sentence boundary in the text naturally paces the audio emission. If drift is observed in testing, add the chars-per-ms estimation from the C++ reference as a refinement.

3. **Multiple audio chunks per transcription event**
   - What we know: Multiple audio InlineDataParts can arrive in a single LiveSessionContent.Content.Parts. AudioAsFloat returns `IReadOnlyList<float[]>`.
   - What's unclear: Whether a single response message typically has 1 or many audio parts.
   - Recommendation: Always iterate `audioChunks` and accumulate all of them. Do not assume one chunk per message.

4. **Function call timing relative to audio**
   - What we know: `LiveSessionToolCall` is a separate message type from `LiveSessionContent`. It arrives as its own WebSocket message, not embedded in audio content.
   - What's unclear: Whether function calls arrive mid-turn (while audio is still streaming) or only at turn boundaries.
   - Recommendation: Phase 3 stubs function call SyncPackets. Phase 4 fully implements the routing. For now, emit FunctionCall packets immediately when received, with the current turn ID.

## Sources

### Primary (HIGH confidence)
- Firebase AI Logic SDK 13.7.0 source: `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs` -- LiveSessionContent struct, AudioAsFloat, OutputTranscription, FromJson parsing
- Firebase AI Logic SDK 13.7.0 source: `Assets/Firebase/FirebaseAI/LiveSession.cs` -- ReceiveAsync loop, message type routing
- Firebase AI Logic SDK 13.7.0 source: `Assets/Firebase/FirebaseAI/FunctionCalling.cs` -- FunctionCallPart structure
- Firebase AI Logic SDK 13.7.0 source: `Assets/Firebase/FirebaseAI/ModelContent.cs` -- Part types, InlineDataPart, TextPart
- Existing PersonaSession.cs: `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` -- current ProcessResponse implementation, event surface
- Existing AudioPlayback.cs: `Packages/com.google.ai-embodiment/Runtime/AudioPlayback.cs` -- EnqueueAudio signature, audio format
- C++ reference PacketAssembler: `/home/cachy/workspaces/projects/persona/src/voice/packet_assembler.h` and `.cpp` -- proven pattern for sentence boundary buffering, text-audio correlation, chars-per-ms estimation
- C++ reference SyncPacket: `/home/cachy/workspaces/projects/persona/src/voice/sync_packet.h` -- packet structure, audio format constants
- [Gemini Live API reference](https://ai.google.dev/api/live) -- BidiGenerateContentServerContent field structure, transcription independence statement

### Secondary (MEDIUM confidence)
- [Firebase AI Logic Live API Configuration](https://firebase.google.com/docs/ai-logic/live-api/configuration) -- outputAudioTranscription behavior: "transcripts are streamed along with the audio"
- [Firebase AI Logic Live API Capabilities](https://firebase.google.com/docs/ai-logic/live-api/capabilities) -- response modality constraints, tool call documentation status
- [Gemini Live API Capabilities Guide](https://ai.google.dev/gemini-api/docs/live-guide) -- incremental server updates, content generation pacing

### Tertiary (LOW confidence)
- OutputTranscription chunk granularity: Not documented; must validate with real API calls
- Audio-to-transcription practical drift behavior: Not documented for Gemini native audio path; C++ reference assumes close correlation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all from existing project
- Architecture patterns: HIGH -- SyncPacket design verified against C++ reference and CONTEXT.md decisions; PacketAssembler flow verified against SDK source
- Data flow: HIGH -- LiveSessionResponse parsing verified line-by-line against SDK source
- Sentence boundary detection: HIGH -- C++ reference provides proven implementation; algorithm is straightforward
- ISyncDriver interface: MEDIUM -- design is sound but behavior with multiple drivers (Phase 5+) is forward-looking
- Transcription ordering: MEDIUM -- official docs confirm independence; practical behavior for Gemini native path needs validation

**Research date:** 2026-02-05
**Valid until:** 2026-04-05 (60 days -- SDK source is local, patterns are stable)
