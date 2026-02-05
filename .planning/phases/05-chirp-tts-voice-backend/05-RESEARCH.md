# Phase 5: Chirp TTS Voice Backend - Research

**Researched:** 2026-02-05
**Domain:** Google Cloud Text-to-Speech (Chirp 3 HD), Unity HTTP integration, Gemini Live API audio routing
**Confidence:** HIGH (API surface verified via official docs; auth approach confirmed via multiple sources)

## Summary

This research investigates the Cloud Text-to-Speech REST API for Chirp 3 HD voice synthesis, authentication options compatible with the existing Firebase setup, the complete Chirp 3 HD voice inventory, custom voice cloning integration, and the critical question of how to route Gemini Live text to an external TTS engine.

The standard approach is a `ChirpTTSClient` class that issues `POST` requests to `texttospeech.googleapis.com/v1/text:synthesize` using the Firebase API key (already present in `google-services.json`) via `x-goog-api-key` header. The response is base64-encoded LINEAR16 audio with a 44-byte WAV header that must be stripped before feeding raw PCM into the existing `AudioPlayback.EnqueueAudio()` pipeline.

The most significant architectural finding is that **the native audio model (`gemini-2.5-flash-native-audio-preview-12-2025`) cannot be switched to text-only mode**. The recommended approach is to keep the Gemini Live session producing audio normally but **use the output transcription text** (already captured by `PacketAssembler.AddTranscription()`) to drive Chirp TTS synthesis, while discarding the Gemini native audio chunks when Chirp backend is selected.

**Primary recommendation:** Keep Gemini Live in audio mode with output transcription enabled. When `VoiceBackend.ChirpTTS` is selected, suppress routing of Gemini audio to `AudioPlayback` and instead route the output transcription text through `ChirpTTSClient` for synthesis, feeding the resulting PCM back through the existing ring buffer playback path.

## Standard Stack

### Core
| Component | Version/Endpoint | Purpose | Why Standard |
|-----------|-----------------|---------|--------------|
| Cloud TTS REST API v1 | `POST https://texttospeech.googleapis.com/v1/text:synthesize` | Chirp 3 HD voice synthesis | Official Google Cloud endpoint for TTS |
| UnityWebRequest | Unity 6 built-in | HTTP client for TTS requests | Unity-native, no external deps, works on all platforms |
| Firebase API Key | From `google-services.json` | Authentication via `x-goog-api-key` header | Already available in project, zero extra config |

### Supporting
| Component | Purpose | When to Use |
|-----------|---------|-------------|
| `System.Convert.FromBase64String` | Decode base64 audio response | Every TTS response |
| `System.BitConverter` | Convert LINEAR16 bytes to float[] | Every TTS response |
| `JsonUtility` (Unity) or MiniJSON | Serialize/deserialize JSON request/response | Every TTS request |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| API key auth | OAuth 2.0 / service account | Much more complex setup; requires credential file management; API key is sufficient for client-side use |
| UnityWebRequest | HttpClient | HttpClient has threading issues in Unity; UWR is the blessed Unity HTTP client |
| Text-only Gemini mode | Output transcription from audio mode | Native audio model rejects `ResponseModality.Text`; output transcription is already implemented and working |

## Architecture Patterns

### Pattern 1: Output Transcription Routing (Critical Discovery)

**What:** The Gemini Live session stays in `ResponseModality.Audio` mode (unchanged from current behavior). The `ProcessResponse` method in `PersonaSession` checks the voice backend. When `ChirpTTS` is selected, it skips routing audio to `AudioPlayback` but still routes output transcription text to `PacketAssembler`. A new `ChirpTTSClient` synthesizes audio from the transcription text and feeds it back to `AudioPlayback`.

**Why not text-only mode:** The `gemini-2.5-flash-native-audio-preview-12-2025` model rejects `ResponseModality.Text` with error: "Cannot extract voices from a non-audio request." This is a confirmed server-side limitation (verified via GitHub issue livekit/agents#4423 and Google AI developer forums). Non-native-audio models (e.g., `gemini-2.0-flash-live-001`) support text mode but are being retired March 2026 and lack the audio comprehension quality.

**When to use:** Always -- this is the only viable approach for the current model.

**Integration points in existing code:**
```csharp
// In PersonaSession.ProcessResponse(), the audio routing already has this structure:
// Lines 558-588: audioChunks processing with AudioPlayback and PacketAssembler
// Lines 636-646: output transcription processing with PacketAssembler

// For Chirp path: skip the AudioPlayback.EnqueueAudio() calls when backend is ChirpTTS
// The PacketAssembler still gets transcription text and emits SyncPackets
// ChirpTTSClient synthesizes from SyncPacket text, feeds audio to AudioPlayback
```

### Pattern 2: ChirpTTSClient as Plain C# Class

**What:** `ChirpTTSClient` is a non-MonoBehaviour class that accepts synthesis requests and returns PCM audio. It uses coroutines or async/await (via Unity's Awaitable in Unity 6) for HTTP requests. It holds a reference to the Firebase API key (obtained once at session creation).

**Why plain C#:** Matches the project pattern (PacketAssembler is plain C#). Only Unity dependency is `UnityWebRequest` which can be used from main thread.

**Key constraint:** `UnityWebRequest` MUST run on the main thread. All TTS requests must be dispatched via `MainThreadDispatcher` or coroutine.

### Pattern 3: Sentence-by-Sentence vs Full-Response Synthesis

**What:** Two synthesis modes configured per-persona in `PersonaConfig`:
- **Sentence mode:** As `PacketAssembler` emits each `SyncPacket` with text, immediately send that text to Cloud TTS. Queue resulting audio into `AudioPlayback` sequentially.
- **Full-response mode:** Accumulate all text across an entire turn, then synthesize once when `FinishTurn` fires.

**Integration point:** The `OnSyncPacket` callback already fires per-sentence (from PacketAssembler's sentence boundary detection). For sentence mode, each `SyncPacket` triggers a TTS request. For full-response mode, buffer text until `IsTurnEnd` is true.

### Recommended Project Structure
```
Packages/com.google.ai-embodiment/Runtime/
  ChirpTTSClient.cs       # HTTP client for Cloud TTS API
  ChirpVoiceList.cs        # Static voice/language data for Inspector dropdown
  PersonaConfig.cs         # Extended with Chirp fields
  PersonaSession.cs        # Modified audio routing for Chirp path
  VoiceBackend.cs          # Already exists (GeminiNative, ChirpTTS)

Packages/com.google.ai-embodiment/Editor/
  PersonaConfigEditor.cs   # Custom Inspector for voice dropdown
```

### Anti-Patterns to Avoid
- **Switching Gemini to text-only mode:** The native audio model rejects it. Do not attempt.
- **Using HttpClient instead of UnityWebRequest:** Will cause threading issues on some platforms (Android, WebGL).
- **Allocating byte arrays per audio callback:** WAV header stripping and PCM conversion should reuse buffers where possible.
- **Blocking the main thread on TTS response:** UnityWebRequest is async by design; never use synchronous waits.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| WAV header parsing | Custom WAV parser | Skip first 44 bytes | LINEAR16 from Cloud TTS always has a standard 44-byte WAV header; no need for full parser |
| JSON serialization | Manual string building | Unity's JsonUtility or MiniJSON (already in project) | MiniJSON is already used by Firebase SDK (`Google.MiniJSON`) |
| Base64 decoding | Custom decoder | `System.Convert.FromBase64String` | Standard library, zero allocation overhead vs custom |
| HTTP request management | Custom HTTP stack | `UnityWebRequest` with `UploadHandlerRaw`/`DownloadHandlerBuffer` | Unity's official HTTP API, handles platform differences |
| Voice list data | Runtime API call to list voices | Hardcoded static data from official docs | Voice list is stable; avoids extra API call and simplifies Inspector |

## Common Pitfalls

### Pitfall 1: LINEAR16 Response Includes WAV Header
**What goes wrong:** Developer treats the entire base64-decoded response as raw PCM and feeds it to the ring buffer. The 44-byte WAV header gets interpreted as audio samples, producing a loud click/pop at the start of every TTS response.
**Why it happens:** The Cloud TTS docs state "Audio content returned as LINEAR16 also contains a WAV header."
**How to avoid:** Always skip the first 44 bytes of the decoded byte array before converting to float[]. Validate by checking bytes 0-3 are "RIFF" (0x52494646).
**Warning signs:** Audible click at the start of each synthesized sentence.

### Pitfall 2: Native Audio Model Rejects Text-Only Mode
**What goes wrong:** Setting `responseModalities: [ResponseModality.Text]` on `gemini-2.5-flash-native-audio-preview-12-2025` causes WebSocket error 1007: "Cannot extract voices from a non-audio request."
**Why it happens:** Native audio models require audio output mode. This is a server-side constraint.
**How to avoid:** Keep the session in audio mode. Use output transcription text for Chirp TTS input. Suppress Gemini audio routing to AudioPlayback when Chirp is selected.
**Warning signs:** WebSocket disconnection immediately after session setup.

### Pitfall 3: UnityWebRequest Must Run on Main Thread
**What goes wrong:** Attempting to create or send a `UnityWebRequest` from a background thread throws an exception or silently fails.
**Why it happens:** Unity's web request system is tied to the main thread's coroutine/player loop system.
**How to avoid:** All TTS requests must be initiated from the main thread. Use `MainThreadDispatcher.Enqueue()` if triggered from a callback on another thread, or use coroutines.
**Warning signs:** "Can only be called from the main thread" exceptions.

### Pitfall 4: Sentence Boundary Latency Accumulation
**What goes wrong:** In sentence-by-sentence mode, each TTS request adds 200-500ms of network latency. If sentences arrive faster than TTS can synthesize, a growing backlog creates increasing delay between AI text and audio.
**Why it happens:** Sequential TTS requests (per CONTEXT.md decision) don't prefetch.
**How to avoid:** Queue TTS requests sequentially. Accept the latency tradeoff (this is a design-time choice the developer makes). For lower latency, developer can select Gemini native audio.
**Warning signs:** Growing gap between text display and audio playback as conversation progresses.

### Pitfall 5: API Key Not Enabled for Cloud TTS
**What goes wrong:** The Firebase API key from `google-services.json` returns 403 Forbidden when calling Cloud TTS.
**Why it happens:** The Cloud Text-to-Speech API must be explicitly enabled in the Google Cloud Console for the project, and the API key must not have API restrictions that exclude it.
**How to avoid:** Document clearly that developers must enable "Cloud Text-to-Speech API" in their Google Cloud Console project. Fire a descriptive error event when 403 is received.
**Warning signs:** 403 Forbidden with message about API not enabled or key restrictions.

### Pitfall 6: PCM Sample Rate Mismatch
**What goes wrong:** Cloud TTS returns audio at a different sample rate than expected, causing pitch distortion.
**Why it happens:** If `sampleRateHertz` is not specified in the request, the API uses the voice's natural sample rate.
**How to avoid:** Always specify `"sampleRateHertz": 24000` in the `audioConfig`. This matches the existing playback pipeline's `GEMINI_SAMPLE_RATE = 24000`.
**Warning signs:** Audio plays too fast (chipmunk) or too slow (deep).

### Pitfall 7: Custom Voice SSML Incompatibility
**What goes wrong:** Sending SSML-wrapped text to a custom (cloned) voice causes an API error.
**Why it happens:** Chirp 3 HD standard voices support SSML; custom/cloned voices have limited or no SSML support.
**How to avoid:** When `voice_clone` is specified in the request, always use plain text input (not SSML). Only use SSML for standard Chirp 3 HD voice names.
**Warning signs:** API error about unsupported SSML for custom voice.

## Code Examples

### Cloud TTS REST Request (Standard Voice)
```json
// Source: https://docs.cloud.google.com/text-to-speech/docs/reference/rest/v1/text/synthesize
{
  "input": {
    "text": "Hello, how are you today?"
  },
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

### Cloud TTS REST Request (SSML, Standard Voice)
```json
{
  "input": {
    "ssml": "<speak>Hello, how are you <break time='500ms'/> today?</speak>"
  },
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

### Cloud TTS REST Request (Custom/Cloned Voice)
```json
// Source: https://docs.cloud.google.com/text-to-speech/docs/chirp3-instant-custom-voice
{
  "input": {
    "text": "Hello, how are you today?"
  },
  "voice": {
    "languageCode": "en-US",
    "voiceClone": {
      "voiceCloningKey": "<the-cloning-key-string>"
    }
  },
  "audioConfig": {
    "audioEncoding": "LINEAR16",
    "sampleRateHertz": 24000
  }
}
```

### Cloud TTS REST Response
```json
{
  "audioContent": "<base64-encoded-bytes>"
}
```

### Authentication Header
```
// Source: https://docs.cloud.google.com/docs/authentication/api-keys-use
POST https://texttospeech.googleapis.com/v1/text:synthesize
Headers:
  x-goog-api-key: <API_KEY_FROM_GOOGLE_SERVICES_JSON>
  Content-Type: application/json; charset=utf-8
```

### Unity UnityWebRequest Pattern for TTS
```csharp
// Source: Unity 6 docs + project patterns
using UnityEngine.Networking;

// Must run on main thread (coroutine or Awaitable)
public async Awaitable<float[]> SynthesizeAsync(string text, string voiceName, string languageCode)
{
    string json = BuildRequestJson(text, voiceName, languageCode);
    byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(json);

    using var request = new UnityWebRequest(
        "https://texttospeech.googleapis.com/v1/text:synthesize",
        "POST"
    );
    request.uploadHandler = new UploadHandlerRaw(bodyBytes);
    request.downloadHandler = new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
    request.SetRequestHeader("x-goog-api-key", _apiKey);

    await request.SendWebRequest();

    if (request.result != UnityWebRequest.Result.Success)
    {
        throw new System.Exception($"TTS request failed: {request.error} - {request.downloadHandler.text}");
    }

    // Parse response JSON to extract audioContent
    string responseJson = request.downloadHandler.text;
    string audioBase64 = ExtractAudioContent(responseJson);

    // Decode and strip WAV header
    byte[] audioBytes = System.Convert.FromBase64String(audioBase64);
    return ConvertLinear16ToFloat(audioBytes, wavHeaderSize: 44);
}

private static float[] ConvertLinear16ToFloat(byte[] audioBytes, int wavHeaderSize)
{
    int pcmStart = wavHeaderSize;
    int sampleCount = (audioBytes.Length - pcmStart) / 2; // 16-bit = 2 bytes per sample
    float[] samples = new float[sampleCount];

    for (int i = 0; i < sampleCount; i++)
    {
        short sample = System.BitConverter.ToInt16(audioBytes, pcmStart + i * 2);
        samples[i] = sample / 32768f;
    }

    return samples;
}
```

### Audio Routing Modification in PersonaSession
```csharp
// In ProcessResponse, current audio routing (lines 558-588):
// When VoiceBackend.ChirpTTS is selected, skip AudioPlayback but keep PacketAssembler

var audioChunks = response.AudioAsFloat;
if (audioChunks != null && audioChunks.Count > 0)
{
    // Only route to AudioPlayback for Gemini native voice
    if (_config.voiceBackend == VoiceBackend.GeminiNative && _audioPlayback != null)
    {
        foreach (var chunk in audioChunks)
            _audioPlayback.EnqueueAudio(chunk);
    }

    // AI speaking state and PacketAssembler routing unchanged
    // (PacketAssembler still needs audio for sync timing in Gemini native mode)
}
```

### Accessing Firebase API Key
```csharp
// Source: Observed in LiveGenerativeModel.GetURL() (line 87)
// The API key is available via: FirebaseApp.DefaultInstance.Options.ApiKey
// OR from the existing Firebase SDK pattern

string apiKey = Firebase.FirebaseApp.DefaultInstance.Options.ApiKey;
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Gemini 2.0 Flash Live with text mode | Gemini 2.5 Flash native audio (audio-only) | Late 2025 | Cannot use text-only mode; must use output transcription |
| 8 Chirp 3 HD voices, 31 locales | 30 voices, 45+ locales | Nov 2025-Jan 2026 | Much wider voice selection |
| Custom voice via separate API | voice_clone field in standard synthesize request | 2025 | Unified API surface for standard and custom voices |

**Deprecated/outdated:**
- `gemini-2.0-flash-live-001`: Being retired March 2026. Was the only model supporting text-only Live mode. Do not depend on it.
- Chirp 2 / Chirp 1 voices: Replaced by Chirp 3 HD. Use `Chirp3-HD` voice names.

## Voice Inventory

### Complete Chirp 3 HD Voice Names (30 voices)
Achernar, Achird, Algenib, Algieba, Alnilam, Aoede, Autonoe, Callirrhoe, Charon, Despina, Enceladus, Erinome, Fenrir, Gacrux, Iapetus, Kore, Laomedeia, Leda, Orus, Puck, Pulcherrima, Rasalgethi, Sadachbia, Sadaltager, Schedar, Sulafat, Umbriel, Vindemiatrix, Zephyr, Zubenelgenubi

### API Voice Name Format
`{languageCode}-Chirp3-HD-{VoiceName}`
Example: `en-US-Chirp3-HD-Achernar`, `ja-JP-Chirp3-HD-Puck`

### Supported Language Locales (45+)
**GA (General Availability):**
ar-XA, bg-BG, bn-IN, cmn-CN, cs-CZ, da-DK, de-DE, el-GR, en-AU, en-GB, en-IN, en-US, es-ES, es-US, et-EE, fi-FI, fr-CA, fr-FR, gu-IN, he-IL, hi-IN, hr-HR, hu-HU, id-ID, it-IT, ja-JP, kn-IN, ko-KR, lt-LT, lv-LV, ml-IN, mr-IN, nl-BE, nl-NL, pl-PL, pt-BR, ro-RO, ru-RU, sk-SK, sl-SI, sr-RS, sw-KE, ta-IN, te-IN, th-TH, tr-TR, vi-VN

**Preview:**
pa-IN (Punjabi), yue-HK (Chinese Hong Kong)

### SSML Support Matrix
| Voice Type | SSML Support | Supported Tags |
|------------|-------------|----------------|
| Standard Chirp 3 HD | Yes (batch/sync only) | `<speak>`, `<say-as>`, `<p>`, `<s>`, `<phoneme>`, `<sub>`, `<break>`, `<audio>`, `<prosody>`, `<voice>` |
| Custom (voice clone) | No (plain text only) | N/A |
| Streaming requests | No | N/A |

## Authentication

### Recommended Approach: Firebase API Key via x-goog-api-key Header
**Confidence: HIGH** (verified via existing Firebase SDK pattern + Android TTS library + Google Cloud docs)

The Firebase SDK already uses the API key from `google-services.json` for all Google API calls. The `LiveGenerativeModel.GetURL()` method (line 87 of `LiveGenerativeModel.cs`) appends `?key={_firebaseApp.Options.ApiKey}`. The `HttpHelpers.SetRequestHeaders()` method uses `request.Headers.Add("x-goog-api-key", firebaseApp.Options.ApiKey)`.

**Steps for the developer:**
1. Enable "Cloud Text-to-Speech API" in Google Cloud Console for their project
2. Ensure API key restrictions don't block `texttospeech.googleapis.com`
3. No additional configuration needed -- the same `google-services.json` works

**How to access the API key:**
```csharp
string apiKey = Firebase.FirebaseApp.DefaultInstance.Options.ApiKey;
```

### Prerequisite for Developers
The developer must enable the **Cloud Text-to-Speech API** in their Google Cloud project (the same project linked to their Firebase app). This is a one-time console action. The existing Firebase API key will then work for TTS calls.

## Gemini Live Session Configuration for Chirp Path

### Critical Decision: Keep Audio Mode, Use Output Transcription
**Confidence: HIGH** (verified limitation + existing code already supports this)

When `VoiceBackend.ChirpTTS` is selected:

1. **Do NOT change** `responseModalities` -- keep `ResponseModality.Audio`
2. **Do NOT change** `speechConfig` -- any Gemini voice name is fine (it will be discarded)
3. **Keep** `outputAudioTranscription: new AudioTranscriptionConfig()` -- this provides the text
4. **In `ProcessResponse`:** Skip routing `AudioAsFloat` to `AudioPlayback`, but still process `OutputTranscription` normally
5. **Route SyncPacket text** through `ChirpTTSClient` for synthesis
6. **Feed synthesized PCM** back to `AudioPlayback.EnqueueAudio()`

This means the Gemini session produces audio that gets thrown away, but the output transcription provides the text needed for Chirp synthesis. The overhead of unused Gemini audio is negligible compared to the complexity of managing separate model variants.

## Custom Voice Cloning Integration

### API Difference for Custom Voices
**Confidence: HIGH** (official docs verified)

Standard voice request uses `voice.name` field:
```json
"voice": { "languageCode": "en-US", "name": "en-US-Chirp3-HD-Achernar" }
```

Custom voice request uses `voice.voiceClone.voiceCloningKey` field:
```json
"voice": { "languageCode": "en-US", "voiceClone": { "voiceCloningKey": "..." } }
```

### Cloning Key Details
- Text string representation of voice data (can be long)
- Generated via `voices:generateVoiceCloningKey` endpoint (out of scope for this phase)
- Stored client-side (on PersonaConfig ScriptableObject per CONTEXT.md decision)
- No limit on number of keys; same key usable by multiple clients simultaneously
- Access requires allow-listing by Google Cloud team (safety restriction)

### Custom Voice Limitations
- No SSML support (plain text only)
- Multilingual transfer: en-US keys can synthesize in de-DE, es-US, es-ES, fr-CA, fr-FR, pt-BR
- Available in global, us, eu, asia-southeast1, asia-northeast1, europe-west2 regions

## Open Questions

1. **Awaitable vs Coroutine for UnityWebRequest in Unity 6**
   - What we know: Unity 6 supports `await request.SendWebRequest()` natively via Awaitable. Coroutines also work.
   - What's unclear: Whether the Awaitable pattern requires specific project settings or has edge cases in Unity 6000.3.7f1.
   - Recommendation: Use Awaitable (async/await) as the primary approach since the project already uses async throughout. Fall back to coroutine if issues arise.

2. **Exact WAV Header Size**
   - What we know: Standard WAV header is 44 bytes. Cloud TTS docs confirm LINEAR16 includes WAV header.
   - What's unclear: Whether Cloud TTS ever produces non-standard WAV headers (extended chunks).
   - Recommendation: Validate first 4 bytes are "RIFF", then read the data chunk offset from the header rather than assuming fixed 44 bytes. Or simply hardcode 44 and validate.

3. **Gemini Audio Bandwidth Waste**
   - What we know: When Chirp is selected, Gemini still generates and transmits audio that gets discarded.
   - What's unclear: Whether there's a way to reduce Gemini audio quality/bandwidth when it will be discarded.
   - Recommendation: Accept the waste. The alternative (switching models or disabling audio) is worse. The transcription quality from native audio models is better.

## Sources

### Primary (HIGH confidence)
- [Cloud TTS REST API reference](https://docs.cloud.google.com/text-to-speech/docs/reference/rest/v1/text/synthesize) - endpoint, request/response format
- [Chirp 3 HD voices docs](https://docs.cloud.google.com/text-to-speech/docs/chirp3-hd) - voice names, languages, SSML support, audio encoding
- [Chirp 3 Instant Custom Voice docs](https://docs.cloud.google.com/text-to-speech/docs/chirp3-instant-custom-voice) - cloning key API, voice_clone field
- [Cloud TTS release notes](https://docs.cloud.google.com/text-to-speech/docs/release-notes) - language additions timeline
- [Supported voices and languages](https://docs.cloud.google.com/text-to-speech/docs/list-voices-and-types) - complete voice inventory
- [Google Cloud API key usage docs](https://docs.cloud.google.com/docs/authentication/api-keys-use) - x-goog-api-key header pattern
- Existing Firebase SDK source (`LiveGenerativeModel.cs`, `HttpHelpers.cs`, `FirebaseInterops.cs`) - API key access pattern
- Existing project source (`PersonaSession.cs`, `PacketAssembler.cs`, `AudioPlayback.cs`) - integration points

### Secondary (MEDIUM confidence)
- [livekit/agents issue #4423](https://github.com/livekit/agents/issues/4423) - native audio model text-only mode failure confirmed
- [Google-Cloud-TTS-Android](https://github.com/changemyminds/Google-Cloud-TTS-Android) - API key auth pattern confirmed for Cloud TTS
- [Gemini Live API guide](https://ai.google.dev/gemini-api/docs/live-guide) - text vs audio modality constraints
- [Cloud TTS authentication docs](https://docs.cloud.google.com/text-to-speech/docs/authentication) - auth methods

### Tertiary (LOW confidence)
- Community reports that `gemini-2.0-flash-live-001` supports text mode (model retiring March 2026, not recommended)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - API endpoint and auth pattern verified via official docs and existing Firebase SDK code
- Architecture (output transcription routing): HIGH - verified native audio model limitation; existing code already handles transcription
- Voice inventory: HIGH - official docs list, cross-verified with release notes
- Authentication: HIGH - Firebase SDK already uses same API key pattern; Android library confirms TTS accepts API keys
- Custom voice cloning: MEDIUM - official docs verified but no hands-on testing of voiceClone field
- Pitfalls: HIGH - WAV header, text-mode failure, and UWR threading are well-documented

**Research date:** 2026-02-05
**Valid until:** 2026-03-05 (30 days - API surface is stable; voice list may expand)
