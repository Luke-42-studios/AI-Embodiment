# Phase 9: TTS Abstraction - Research

**Researched:** 2026-02-13
**Domain:** C# interface design for TTS provider abstraction, Google Cloud TTS auth for custom voices, Unity Inspector interface serialization, streaming TTS support
**Confidence:** HIGH

## Summary

This research investigates the technical requirements for abstracting the existing ChirpTTSClient behind an ITTSProvider interface, adding support for custom third-party TTS providers via Unity Inspector, and the two specific research questions from the CONTEXT.md: Cloud TTS auth requirements for custom/cloned voices and streaming audio response support.

The standard approach is a clean `ITTSProvider : IDisposable` interface with `SynthesizeAsync` returning a `TTSResult` struct. ChirpTTSClient already implements the exact pattern needed -- the refactoring is largely mechanical. For the Custom VoiceBackend Inspector slot, the recommended pattern is a `[SerializeField] private MonoBehaviour _customTTSProvider` field with runtime cast to `ITTSProvider`, validated by the custom editor. The `VoiceBackend` enum gains a third value (`Custom`) and `ChirpSynthesisMode` is renamed to `TTSSynthesisMode`.

Two critical findings from the specific research questions:
1. **Custom/cloned voice auth:** The Chirp 3 Instant Custom Voice feature uses the `v1beta1` endpoint and all official code examples show OAuth Bearer token auth (`Authorization: Bearer <token>`), not API keys. Standard HD voices work fine with API keys on the v1 endpoint. This means the existing ChirpTTSClient (API key + v1) handles HD voices but custom/cloned voices may require v1beta1 + OAuth. The existing code already has `voiceCloningKey` support -- it sends requests to v1 with API key auth. This may or may not work in practice (Google's docs show v1beta1 + OAuth for custom voices).
2. **Streaming TTS:** Cloud TTS bidirectional streaming (`StreamingSynthesize`) is **gRPC-only** -- there is no REST streaming endpoint. Since the project uses `UnityWebRequest` (HTTP/REST), true streaming is not available. Sentence-by-sentence splitting via `PacketAssembler` remains the only viable latency optimization path.

**Primary recommendation:** Extract `ITTSProvider` interface from the existing `ChirpTTSClient` signature. The refactoring is straightforward because the current code already follows the pattern needed. Focus effort on the VoiceBackend enum expansion, PersonaSession routing generalization, and custom provider Inspector support.

## Standard Stack

### Core
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| ITTSProvider interface | N/A (project-defined) | TTS backend abstraction | Clean separation of synthesis contract from implementation |
| ChirpTTSClient | Existing (refactored) | Cloud TTS v1 REST synthesis via UnityWebRequest | Already proven; gains ITTSProvider interface |
| TTSResult struct | N/A (project-defined) | Self-describing audio return type | Decouples providers from AudioPlayback sample rate assumptions |
| VoiceBackend enum | Existing (extended) | Three-way backend selection | Already exists with GeminiNative/ChirpTTS; adds Custom |
| Newtonsoft.Json | Already in project | JSON serialization for ChirpTTSClient | Replaces MiniJSON per prior phase decisions |

### Supporting
| Component | Purpose | When to Use |
|-----------|---------|-------------|
| RequireInterfaceAttribute + PropertyDrawer | Inspector interface field validation | Custom VoiceBackend Inspector slot |
| PersonaConfigEditor (existing) | Custom Inspector for voice settings | Extended for Custom backend UI |
| MonoBehaviour base class | Unity-serializable reference for custom providers | Required for Inspector drag-drop |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| MonoBehaviour reference for Custom | [SerializeReference] | SerializeReference cannot reference UnityEngine.Object derivatives (MonoBehaviour, ScriptableObject) -- won't work |
| Manual Object field cast | Third-party SerializableInterface<T> packages | External dependency for marginal gain; simple pattern is sufficient |
| Streaming TTS via gRPC | Stay with REST sentence-by-sentence | gRPC requires native plugins, adds massive complexity; REST + sentence splitting is proven |

## Architecture Patterns

### Recommended Project Structure
```
Packages/com.google.ai-embodiment/Runtime/
  ITTSProvider.cs            # NEW: interface definition + TTSResult struct + TTSSynthesisMode enum
  ChirpTTSClient.cs          # MODIFIED: implements ITTSProvider
  PersonaConfig.cs           # MODIFIED: ChirpSynthesisMode -> TTSSynthesisMode, add Custom MonoBehaviour field
  PersonaSession.cs          # MODIFIED: generalize Chirp-specific code to ITTSProvider
  VoiceBackend.cs            # MODIFIED: add Custom enum value

Packages/com.google.ai-embodiment/Runtime/Attributes/
  RequireInterfaceAttribute.cs  # NEW: attribute for interface field validation

Packages/com.google.ai-embodiment/Editor/
  PersonaConfigEditor.cs     # MODIFIED: Custom backend UI, synthesisMode rename
  RequireInterfaceDrawer.cs  # NEW: property drawer for interface validation
```

### Pattern 1: ITTSProvider Interface Design
**What:** A minimal interface that any TTS backend implements. Extends IDisposable for resource cleanup.
**When to use:** Always -- this is the core abstraction.

```csharp
using System;

namespace AIEmbodiment
{
    /// <summary>
    /// Abstraction for text-to-speech synthesis backends.
    /// Implementations must be safe to call from the Unity main thread.
    /// </summary>
    public interface ITTSProvider : IDisposable
    {
        /// <summary>
        /// Synthesizes text to PCM audio.
        /// </summary>
        /// <param name="text">Text to synthesize.</param>
        /// <param name="voiceName">Voice identifier (provider-specific interpretation).</param>
        /// <param name="languageCode">BCP-47 language code (e.g., "en-US").</param>
        /// <param name="onAudioChunk">Optional callback for streaming audio chunks.
        /// When null, the full result is returned at completion.</param>
        /// <returns>Complete audio result. When streaming via onAudioChunk,
        /// the returned TTSResult contains the final/remaining chunk.</returns>
        Awaitable<TTSResult> SynthesizeAsync(
            string text,
            string voiceName,
            string languageCode,
            Action<TTSResult> onAudioChunk = null);
    }
}
```

### Pattern 2: TTSResult Struct
**What:** Self-describing audio result that decouples providers from sample rate assumptions.
**When to use:** Return type from all ITTSProvider.SynthesizeAsync calls.

```csharp
namespace AIEmbodiment
{
    /// <summary>
    /// Self-describing PCM audio result from a TTS provider.
    /// </summary>
    public readonly struct TTSResult
    {
        /// <summary>PCM audio samples in float[-1..1] range.</summary>
        public readonly float[] Samples;

        /// <summary>Sample rate in Hz (e.g., 24000 for Chirp 3 HD).</summary>
        public readonly int SampleRate;

        /// <summary>Number of audio channels (typically 1 for mono TTS).</summary>
        public readonly int Channels;

        public TTSResult(float[] samples, int sampleRate, int channels)
        {
            Samples = samples;
            SampleRate = sampleRate;
            Channels = channels;
        }

        /// <summary>True if this result contains valid audio data.</summary>
        public bool HasAudio => Samples != null && Samples.Length > 0;
    }
}
```

### Pattern 3: MonoBehaviour Interface Reference in Inspector
**What:** A `[SerializeField] private MonoBehaviour _customTTSProvider` field that Unity can serialize, validated at runtime via cast to ITTSProvider.
**When to use:** When VoiceBackend.Custom is selected in PersonaConfig.

```csharp
// In PersonaConfig:
[SerializeField] private MonoBehaviour _customTTSProvider;

/// <summary>
/// Returns the custom TTS provider MonoBehaviour cast to ITTSProvider.
/// Null if not assigned or the component does not implement ITTSProvider.
/// </summary>
public ITTSProvider CustomTTSProvider =>
    _customTTSProvider as ITTSProvider;
```

```csharp
// In PersonaConfigEditor (Custom backend section):
if (backend == VoiceBackend.Custom)
{
    EditorGUILayout.PropertyField(_customTTSProvider,
        new GUIContent("TTS Provider", "MonoBehaviour implementing ITTSProvider"));

    // Validate at edit time
    var mb = _customTTSProvider.objectReferenceValue as MonoBehaviour;
    if (mb != null && !(mb is ITTSProvider))
    {
        EditorGUILayout.HelpBox(
            $"{mb.GetType().Name} does not implement ITTSProvider.",
            MessageType.Error);
    }
    else if (mb == null && _customTTSProvider.objectReferenceValue != null)
    {
        EditorGUILayout.HelpBox(
            "Assigned object is not a MonoBehaviour.",
            MessageType.Error);
    }
}
```

**Why MonoBehaviour field (not RequireInterface attribute):** The simplest approach uses a plain `MonoBehaviour` field (which Unity serializes natively) plus custom editor validation. This avoids needing a custom attribute + property drawer entirely. The PersonaConfigEditor already exists and handles field visibility -- adding the validation inline is simpler than a separate RequireInterfaceAttribute + drawer system. The cast check happens both in the editor (warning message) and at runtime (null check).

### Pattern 4: ChirpTTSClient Signature Change
**What:** The existing `SynthesizeAsync` method signature changes from returning `Awaitable<float[]>` to `Awaitable<TTSResult>`. The voiceCloningKey parameter moves from SynthesizeAsync to the constructor.
**When to use:** The core refactoring of ChirpTTSClient.

Current signature:
```csharp
public async Awaitable<float[]> SynthesizeAsync(
    string text, string voiceName, string languageCode, string voiceCloningKey = null)
```

New signature (matching ITTSProvider):
```csharp
public async Awaitable<TTSResult> SynthesizeAsync(
    string text, string voiceName, string languageCode, Action<TTSResult> onAudioChunk = null)
```

The `voiceCloningKey` becomes a constructor parameter (provider-specific config at construction time, per CONTEXT.md decision). The `onAudioChunk` parameter is accepted but not used by ChirpTTSClient (REST is non-streaming); the full result is returned at completion.

### Pattern 5: PersonaSession Provider Generalization
**What:** Replace all `_chirpClient` references with `_ttsProvider` (type ITTSProvider). The routing logic in HandleOutputTranscription and HandleTurnCompleteEvent checks `_ttsProvider != null` instead of `_config.voiceBackend == VoiceBackend.ChirpTTS`.
**When to use:** The core refactoring of PersonaSession for TTS abstraction.

```csharp
// PersonaSession fields:
private ITTSProvider _ttsProvider;  // replaces ChirpTTSClient _chirpClient

// In Connect():
if (_config.voiceBackend == VoiceBackend.ChirpTTS)
{
    _ttsProvider = new ChirpTTSClient(
        settings.ApiKey,
        _config.IsCustomChirpVoice ? _config.voiceCloningKey : null);
}
else if (_config.voiceBackend == VoiceBackend.Custom)
{
    _ttsProvider = _config.CustomTTSProvider;
    if (_ttsProvider == null)
    {
        Debug.LogError("PersonaSession: VoiceBackend.Custom selected but no ITTSProvider assigned.");
        // Fall through -- will behave like GeminiNative (no TTS routing)
    }
}
// GeminiNative: _ttsProvider remains null

// In routing code:
// Replace: _config.voiceBackend == VoiceBackend.ChirpTTS
// With:    _ttsProvider != null
```

### Anti-Patterns to Avoid
- **Putting provider-specific params in ITTSProvider:** The interface takes only (text, voiceName, languageCode, onAudioChunk). Voice cloning keys, API keys, auth tokens -- all go in the constructor/config of the concrete class. Interface consumers never see them.
- **Checking VoiceBackend enum in routing code:** After Connect() resolves the provider, all routing should check `_ttsProvider != null`, not `voiceBackend == ChirpTTS`. The enum is only used during provider creation.
- **Using [SerializeReference] for MonoBehaviour interface field:** SerializeReference explicitly cannot reference UnityEngine.Object derivatives. Use a plain MonoBehaviour field with editor validation instead.
- **Attempting gRPC streaming for Cloud TTS:** The bidirectional streaming API is gRPC-only. Unity has no built-in gRPC support. Adding gRPC via native plugins is massive complexity for marginal gain over sentence-by-sentence REST.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Interface field in Inspector | Custom RequireInterface attribute + PropertyDrawer system | Plain MonoBehaviour field + PersonaConfigEditor inline validation | The custom editor already exists; inline validation is 5 lines vs a whole attribute/drawer system |
| Audio sample rate resampling across providers | Per-provider resampling logic | TTSResult.SampleRate + AudioPlayback's existing resampling (24kHz to system rate) | AudioPlayback already resamples from 24kHz; if a custom provider returns a different rate, a small resampler adapter can be added later |
| Streaming audio delivery | Complex streaming infrastructure | onAudioChunk callback parameter (unused by ChirpTTSClient REST, available for future providers) | REST Cloud TTS is non-streaming; the callback is a forward-compatible slot, not a real streaming pipe |
| Provider lifecycle management | Custom lifecycle manager | IDisposable + PersonaSession.Disconnect() | IDisposable is the C# standard; PersonaSession already disposes ChirpTTSClient on disconnect |

**Key insight:** The existing ChirpTTSClient code already implements 95% of what ITTSProvider needs. The refactoring is primarily:
1. Extract interface from existing method signature
2. Wrap return value in TTSResult struct
3. Move voiceCloningKey from method parameter to constructor
4. Replace ChirpTTSClient field type with ITTSProvider in PersonaSession
5. Rename ChirpSynthesisMode to TTSSynthesisMode

## Common Pitfalls

### Pitfall 1: Custom Voice Auth Mismatch
**What goes wrong:** Developer configures a voice cloning key and expects it to work with the same API key auth used for standard HD voices. The request may fail with 403 or auth error because Google's official docs show custom voices using v1beta1 + OAuth.
**Why it happens:** The existing ChirpTTSClient uses v1 + API key auth. Google's custom voice docs show v1beta1 + OAuth Bearer token. It is unclear whether v1 + API key works for custom voices in practice.
**How to avoid:** Document this limitation clearly. The current implementation sends custom voice requests to v1 with API key -- this works for standard HD voices and may work for custom voices (Google's API may accept it even though docs show OAuth). If it fails, the error handler fires and conversation continues with text-only. A future improvement could add OAuth support.
**Warning signs:** 403 Forbidden or auth errors only when using voice cloning keys, not standard voices.

### Pitfall 2: TTSResult Sample Rate Assumption
**What goes wrong:** A custom ITTSProvider returns audio at 44100Hz or 48000Hz, but AudioPlayback assumes everything is 24000Hz. Audio plays at wrong speed (chipmunk or slow-motion).
**Why it happens:** AudioPlayback.GEMINI_SAMPLE_RATE is hardcoded to 24000. The resampling ratio is calculated from this constant.
**How to avoid:** When feeding TTSResult audio to AudioPlayback, verify TTSResult.SampleRate matches GEMINI_SAMPLE_RATE (24000). If not, either: (a) document that custom providers must output 24kHz, or (b) add a simple resampling step before EnqueueAudio. For Phase 9, option (a) is simplest with a runtime warning.
**Warning signs:** Audio pitch distortion only with custom providers, not with ChirpTTS.

### Pitfall 3: Custom Provider Disposed by PersonaSession
**What goes wrong:** Developer assigns a custom MonoBehaviour ITTSProvider in Inspector. PersonaSession calls Dispose() on it during Disconnect(). The MonoBehaviour is now in a "disposed" state but still exists on the GameObject, confusing subsequent Connect() calls.
**Why it happens:** CONTEXT.md decision: "PersonaSession always disposes the active provider on Disconnect (whether it created it or developer supplied it)."
**How to avoid:** The custom provider's Dispose() implementation should reset internal state rather than destroying the MonoBehaviour. Document this contract: Dispose() means "end the current session's use" not "destroy the component." After Dispose(), the provider should be re-usable for the next Connect() call if it's a developer-supplied MonoBehaviour. Alternatively, only dispose providers that PersonaSession created (ChirpTTS), and skip disposal for developer-supplied Custom providers.
**Warning signs:** NullReferenceException or ObjectDisposedException on second Connect() with Custom backend.

### Pitfall 4: SetTTSProvider Called Mid-Session
**What goes wrong:** Developer calls PersonaSession.SetTTSProvider() while a session is active. The provider swap causes audio routing confusion -- partially synthesized text goes to the old provider, new text to the new one.
**Why it happens:** CONTEXT.md says "Provider is fixed for session lifetime" but the API method exists and could be called anytime.
**How to avoid:** SetTTSProvider should throw or warn if State != Disconnected. Document the requirement to set provider before Connect().
**Warning signs:** Mixed audio artifacts, partial sentences synthesized by wrong provider.

### Pitfall 5: OnError Event Removed from ITTSProvider
**What goes wrong:** The existing ChirpTTSClient has an `OnError` event that PersonaSession subscribes to. When refactoring to ITTSProvider, the interface uses exceptions (per CONTEXT.md), but the existing `HandleChirpError` subscription pattern in PersonaSession breaks.
**Why it happens:** The CONTEXT.md decision says "Errors are exceptions from SynthesizeAsync -- no OnError event on the interface."
**How to avoid:** Remove the OnError event from ChirpTTSClient. The SynthesizeAndEnqueue method in PersonaSession already catches exceptions from SynthesizeAsync and routes them to OnError. The existing `HandleChirpError` subscription and `MainThreadDispatcher.Enqueue` wrapper become unnecessary because SynthesizeAsync already runs on the main thread.
**Warning signs:** Dead code (HandleChirpError) remaining after refactoring.

### Pitfall 6: Chirp Text Buffer References Remain Backend-Specific
**What goes wrong:** The `_chirpTextBuffer` and `_chirpSynthesizing` fields in PersonaSession still reference "chirp" after refactoring, creating confusion about whether they apply to all providers.
**Why it happens:** Incomplete rename during refactoring.
**How to avoid:** Rename to `_ttsTextBuffer` and use for all ITTSProvider backends (both ChirpTTS and Custom). The text accumulation logic is provider-agnostic -- any TTS backend needs the same sentence-by-sentence or full-response buffering.
**Warning signs:** Code review finds "chirp" references in provider-generic code paths.

## Code Examples

### ITTSProvider Full Interface Definition
```csharp
// Source: Derived from existing ChirpTTSClient signature + CONTEXT.md decisions
using System;

namespace AIEmbodiment
{
    public interface ITTSProvider : IDisposable
    {
        Awaitable<TTSResult> SynthesizeAsync(
            string text,
            string voiceName,
            string languageCode,
            Action<TTSResult> onAudioChunk = null);
    }

    public readonly struct TTSResult
    {
        public readonly float[] Samples;
        public readonly int SampleRate;
        public readonly int Channels;

        public TTSResult(float[] samples, int sampleRate, int channels)
        {
            Samples = samples;
            SampleRate = sampleRate;
            Channels = channels;
        }

        public bool HasAudio => Samples != null && Samples.Length > 0;
    }

    /// <summary>
    /// Controls how TTS providers synthesize audio from AI text output.
    /// Provider-agnostic replacement for ChirpSynthesisMode.
    /// </summary>
    public enum TTSSynthesisMode
    {
        /// <summary>Synthesize each sentence as PacketAssembler emits it.</summary>
        SentenceBySentence,

        /// <summary>Wait for complete AI response, synthesize once.</summary>
        FullResponse
    }
}
```

### ChirpTTSClient Constructor Change
```csharp
// Current constructor:
public ChirpTTSClient(string apiKey)

// New constructor (voiceCloningKey moves here from SynthesizeAsync):
public ChirpTTSClient(string apiKey, string voiceCloningKey = null)
{
    _apiKey = apiKey ?? throw new ArgumentException("API key required.", nameof(apiKey));
    _voiceCloningKey = voiceCloningKey;
}
```

### PersonaSession SynthesizeAndEnqueue Generalized
```csharp
// Source: Refactored from existing PersonaSession.SynthesizeAndEnqueue
private async void SynthesizeAndEnqueue(string text)
{
    if (_ttsProvider == null || _audioPlayback == null || string.IsNullOrEmpty(text)) return;

    try
    {
        string voiceName = _config.voiceBackend == VoiceBackend.ChirpTTS
            ? (_config.IsCustomChirpVoice ? _config.customVoiceName : _config.chirpVoiceName)
            : _config.customVoiceName;  // Custom backend uses whatever voice name is configured
        string languageCode = _config.chirpLanguageCode;

        if (!_aiSpeaking)
        {
            _aiSpeaking = true;
            OnAISpeakingStarted?.Invoke();
        }

        TTSResult result = await _ttsProvider.SynthesizeAsync(text, voiceName, languageCode);

        if (result.HasAudio && _audioPlayback != null)
        {
            // Note: AudioPlayback assumes 24kHz. If result.SampleRate differs, warn.
            if (result.SampleRate != 24000)
            {
                Debug.LogWarning(
                    $"PersonaSession: TTS provider returned {result.SampleRate}Hz audio. " +
                    "AudioPlayback expects 24000Hz. Audio may play at wrong speed.");
            }
            _audioPlayback.EnqueueAudio(result.Samples);
        }
    }
    catch (Exception ex)
    {
        OnError?.Invoke(ex);
        Debug.LogWarning($"PersonaSession: TTS synthesis failed: {ex.Message}");
    }
}
```

### VoiceBackend Enum Extension
```csharp
namespace AIEmbodiment
{
    public enum VoiceBackend
    {
        /// <summary>Uses Gemini Live API's built-in voice synthesis.</summary>
        GeminiNative,

        /// <summary>Routes text to Cloud TTS Chirp 3 HD.</summary>
        ChirpTTS,

        /// <summary>Routes text to a developer-supplied ITTSProvider MonoBehaviour.</summary>
        Custom
    }
}
```

### PersonaConfig Field Changes
```csharp
// In PersonaConfig:

// RENAMED: chirpSynthesisMode -> synthesisMode, ChirpSynthesisMode -> TTSSynthesisMode
public TTSSynthesisMode synthesisMode = TTSSynthesisMode.SentenceBySentence;

// NEW: Custom backend MonoBehaviour reference
[SerializeField] private MonoBehaviour _customTTSProvider;

/// <summary>
/// Returns the custom TTS provider if assigned and valid (implements ITTSProvider).
/// Null otherwise.
/// </summary>
public ITTSProvider CustomTTSProvider => _customTTSProvider as ITTSProvider;
```

### Custom Provider Example (for documentation)
```csharp
// Example: How a developer would implement a custom TTS provider
using System;
using UnityEngine;

namespace MyGame
{
    public class ElevenLabsTTSProvider : MonoBehaviour, ITTSProvider
    {
        [SerializeField] private string _apiKey;
        [SerializeField] private string _modelId = "eleven_turbo_v2_5";

        private bool _disposed;

        public async Awaitable<TTSResult> SynthesizeAsync(
            string text, string voiceName, string languageCode,
            Action<TTSResult> onAudioChunk = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ElevenLabsTTSProvider));

            // ... HTTP request to ElevenLabs API ...
            // ... decode response audio ...

            return new TTSResult(pcmSamples, sampleRate: 24000, channels: 1);
        }

        public void Dispose()
        {
            _disposed = true;
            // Reset for reuse on next Connect()
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ChirpTTSClient hardcoded in PersonaSession | ITTSProvider interface abstraction | Phase 9 (this phase) | Any TTS backend can be plugged in |
| ChirpSynthesisMode enum | TTSSynthesisMode enum (provider-agnostic) | Phase 9 (this phase) | Synthesis mode applies to all providers |
| VoiceBackend { GeminiNative, ChirpTTS } | VoiceBackend { GeminiNative, ChirpTTS, Custom } | Phase 9 (this phase) | Third-party TTS support |
| MiniJSON in ChirpTTSClient | Newtonsoft.Json (already done in Phase 8) | Phase 8 | Already completed -- no work needed |

**Deprecated/outdated:**
- `ChirpSynthesisMode` enum: Renamed to `TTSSynthesisMode` in this phase
- `PersonaConfig.chirpSynthesisMode` field: Renamed to `PersonaConfig.synthesisMode`
- `ChirpTTSClient.OnError` event: Removed; exceptions from SynthesizeAsync are the error mechanism

## Specific Research Questions (from CONTEXT.md)

### Q1: Cloud TTS Auth Requirements for Custom vs Standard Voices
**Confidence: MEDIUM** (official docs verified but not hands-on tested)

**Finding:** The official Google Cloud documentation for Chirp 3 Instant Custom Voice exclusively shows:
- Endpoint: `v1beta1/text:synthesize` (not v1)
- Auth: `Authorization: Bearer <access_token>` + `x-goog-user-project: <project_id>` headers
- No API key examples are provided for custom voices

The existing ChirpTTSClient uses:
- Endpoint: `v1/text:synthesize`
- Auth: `x-goog-api-key: <api_key>` header

**For standard HD voices:** API key + v1 works (confirmed by the existing implementation and Phase 5 research).

**For custom/cloned voices:** Official docs show v1beta1 + OAuth. Whether v1 + API key also works is unverified. The Google Cloud API key docs state "Not all Google Cloud APIs accept API keys to authorize usage." Custom voice support is listed as requiring allow-listing by Google Cloud team, which may imply stricter auth requirements.

**Recommendation for Phase 9:** Keep the existing API key auth. The voiceCloningKey feature already works in the current code (shipped in Phase 5). If it works in practice, don't change it. If it fails for specific users, the error handler gracefully degrades. Document the potential OAuth requirement as a known limitation. A future phase could add OAuth support if needed.

### Q2: Cloud TTS REST API Streaming Support
**Confidence: HIGH** (verified via official REST API reference)

**Finding:** Cloud TTS bidirectional streaming (`StreamingSynthesize`) is **gRPC-only**. The REST API reference lists only:
- `text.synthesize` (synchronous -- returns full response)
- `voices.list`
- `projects.locations.synthesizeLongAudio` (async long-form, polling-based)

There is no REST streaming endpoint. The `StreamingSynthesizeRequest` / `StreamingSynthesizeResponse` types exist only in the gRPC API surface (`google.cloud.texttospeech.v1beta1` package).

**gRPC in Unity:** Unity has no built-in gRPC support. Adding it requires native plugins (e.g., `grpc-dotnet` or Google's C core gRPC), platform-specific builds, and significant complexity. This is not viable for a Unity package.

**Recommendation:** Sentence-by-sentence splitting via `PacketAssembler` remains the only practical latency optimization for REST-based TTS. This is already implemented and works well. The `Action<TTSResult> onAudioChunk` callback parameter in ITTSProvider is a forward-compatible slot that a hypothetical future gRPC-based provider could use, but the REST-based ChirpTTSClient ignores it.

**Alternative discovery:** The Gemini API itself now offers TTS generation (separate from Live API) using models like `gemini-2.5-flash-preview-tts`. This uses API key auth via `x-goog-api-key` and offers 30 voices. However, this is a different API from Cloud TTS and may not support custom/cloned voices. It could be a future ITTSProvider implementation but is out of scope for Phase 9.

## Open Questions

1. **Custom voice auth in practice**
   - What we know: Official docs show v1beta1 + OAuth for custom voices. Existing code uses v1 + API key.
   - What's unclear: Whether v1 + API key actually works for custom voice cloning requests in practice.
   - Recommendation: Keep current auth. If it breaks, error handling gracefully degrades. Document as known limitation.

2. **AudioPlayback sample rate flexibility**
   - What we know: AudioPlayback hardcodes GEMINI_SAMPLE_RATE = 24000. ChirpTTS returns 24kHz. Custom providers may return other rates.
   - What's unclear: Whether to require 24kHz from all providers or add resampling support.
   - Recommendation: For Phase 9, document that providers should return 24kHz. Add a runtime warning if SampleRate differs. Defer resampling support to a future phase.

3. **Custom provider Dispose() semantics**
   - What we know: PersonaSession disposes the provider on Disconnect. Developer-supplied MonoBehaviours survive Disconnect.
   - What's unclear: Whether to always dispose (per CONTEXT.md) or skip disposal for developer-supplied providers.
   - Recommendation: Dispose all providers uniformly (per CONTEXT.md decision). Document that custom providers' Dispose() should reset state for reuse, not permanently destroy the component.

## Sources

### Primary (HIGH confidence)
- Existing codebase: `ChirpTTSClient.cs`, `PersonaSession.cs`, `PersonaConfig.cs`, `VoiceBackend.cs`, `AudioPlayback.cs`, `PacketAssembler.cs`, `PersonaConfigEditor.cs` -- all read and analyzed in full
- Phase 5 research (`05-RESEARCH.md`) -- prior Cloud TTS research, auth patterns, voice inventory
- Phase 9 CONTEXT.md -- locked decisions for interface design, provider lifecycle, synthesis modes
- [Cloud TTS REST API reference](https://docs.cloud.google.com/text-to-speech/docs/reference/rest) -- confirmed no streaming REST endpoint
- [Cloud TTS authentication docs](https://docs.cloud.google.com/text-to-speech/docs/authentication) -- auth methods overview
- [Google Cloud API key usage](https://docs.cloud.google.com/docs/authentication/api-keys-use) -- x-goog-api-key header, "not all APIs accept API keys"

### Secondary (MEDIUM confidence)
- [Chirp 3 Instant Custom Voice docs](https://docs.cloud.google.com/text-to-speech/docs/chirp3-instant-custom-voice) -- v1beta1 endpoint + OAuth auth for custom voices
- [Cloud TTS streaming synthesis quickstart](https://docs.cloud.google.com/text-to-speech/docs/create-audio-text-streaming) -- confirmed gRPC-based streaming
- [Unity interface serialization patterns](https://www.patrykgalach.com/2020/01/27/assigning-interface-in-unity-inspector/) -- MonoBehaviour field + cast pattern
- [Gemini API TTS generation](https://ai.google.dev/gemini-api/docs/speech-generation) -- alternative TTS via Gemini API (discovered, not in scope)

### Tertiary (LOW confidence)
- Unity discussions on SerializeReference limitations with UnityEngine.Object -- confirms MonoBehaviour field approach is necessary
- Community reports on gRPC in Unity -- confirms complexity of native plugin approach

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- interface design is straightforward C#; existing code provides the template
- Architecture: HIGH -- refactoring is mechanical from existing ChirpTTSClient; patterns verified against codebase
- Inspector integration: HIGH -- MonoBehaviour field + editor validation is proven Unity pattern
- Custom voice auth: MEDIUM -- official docs show different auth than current implementation; untested in practice
- Streaming support: HIGH -- REST API reference definitively has no streaming endpoint
- Pitfalls: HIGH -- most are direct observations from the codebase analysis

**Research date:** 2026-02-13
**Valid until:** 2026-03-15 (30 days -- interface design is stable; Cloud TTS auth may evolve)
