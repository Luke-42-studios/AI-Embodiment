---
phase: 05-chirp-tts-voice-backend
verified: 2026-02-05T23:30:00Z
status: passed
score: 3/3 must-haves verified
---

# Phase 5: Chirp TTS Voice Backend Verification Report

**Phase Goal:** Developer can select Chirp 3 HD as the voice backend for a persona, getting access to 30+ high-quality voices as an alternative to Gemini native audio
**Verified:** 2026-02-05T23:30:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | ChirpTTSClient sends text to Cloud TTS API via UnityWebRequest and returns PCM audio | VERIFIED | ChirpTTSClient.cs (234 lines): SynthesizeAsync sends POST to `texttospeech.googleapis.com/v1/text:synthesize` via UnityWebRequest with `x-goog-api-key` header. Decodes base64 response, strips 44-byte WAV header (with RIFF validation), converts LINEAR16 to float[] at 24kHz. Handles both standard voices (SSML) and custom voices (plain text + voiceCloningKey). |
| 2 | When a persona is configured with Chirp backend, text from Gemini Live is routed to Chirp TTS instead of using inline audio | VERIFIED | PersonaSession.cs: ProcessResponse gates Gemini native audio on `VoiceBackend.GeminiNative` (lines 653, 664, 679). When ChirpTTS selected, native audio is discarded. Output transcription text is captured into `_chirpTextBuffer` (line 761-764). Sentence-by-sentence mode: HandleSyncPacket calls SynthesizeAndEnqueue per sentence (lines 330-336). Full-response mode: TurnComplete triggers synthesis of accumulated text (lines 707-714). SynthesizeAndEnqueue calls `_chirpClient.SynthesizeAsync` and feeds PCM result to `_audioPlayback.EnqueueAudio` (lines 370-379). |
| 3 | Voice backend selection (Gemini native vs Chirp) is configured per-persona in the ScriptableObject and applied at session creation time | VERIFIED | PersonaConfig.cs: ScriptableObject has `voiceBackend` field (VoiceBackend enum: GeminiNative/ChirpTTS), plus chirpLanguageCode, chirpVoiceShortName, chirpVoiceName, chirpSynthesisMode, customVoiceName, voiceCloningKey fields. PersonaSession.Connect() reads `_config.voiceBackend` and conditionally initializes ChirpTTSClient with Firebase API key (lines 152-157). PersonaConfigEditor.cs (206 lines) provides custom Inspector with conditional visibility, language/voice dropdowns using ChirpVoiceList data, and auto-synced API voice name. |

**Score:** 3/3 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs` | HTTP client for Cloud TTS synthesis | VERIFIED (234 lines, no stubs, wired) | Plain C# class implementing IDisposable. Exports SynthesizeAsync returning Awaitable<float[]>. Uses UnityWebRequest with x-goog-api-key auth. Handles standard voices (SSML), custom voices (voiceCloningKey), WAV header stripping, PCM conversion. OnError event for non-throwing error reporting. |
| `Packages/com.google.ai-embodiment/Runtime/ChirpVoiceList.cs` | Static voice and language data | VERIFIED (155 lines, no stubs, wired) | Static class with 30 voice names, 49 language locales (47 GA + 2 Preview), GetApiVoiceName builder, GetVoiceDisplayNames helper, CustomVoice sentinel. Referenced by PersonaConfigEditor (4 references) and PersonaConfig (IsCustomChirpVoice property). |
| `Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs` | Extended persona config with Chirp fields | VERIFIED (59 lines, no stubs, wired) | Contains chirpLanguageCode, chirpVoiceShortName, chirpVoiceName, chirpSynthesisMode, customVoiceName, voiceCloningKey. ChirpSynthesisMode enum with SentenceBySentence and FullResponse. IsCustomChirpVoice convenience property. |
| `Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs` | Custom Inspector with conditional dropdowns | VERIFIED (206 lines, no stubs, wired) | CustomEditor for PersonaConfig. Caches all SerializedProperty references in OnEnable. Conditional visibility: GeminiNative shows voice name, ChirpTTS shows language dropdown, voice dropdown (30 voices + Custom), custom voice fields, synthesis mode with HelpBox descriptions. Auto-syncs chirpVoiceName on language/voice change. |
| `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` | Chirp TTS routing in ProcessResponse | VERIFIED (791 lines, no stubs, wired) | ChirpTTSClient field, _chirpTextBuffer. Backend-gated audio routing. SynthesizeAndEnqueue async method. HandleChirpError. Chirp client init in Connect(), disposed in Disconnect() and OnDestroy(). Text buffer cleared on interruption. Both sentence-by-sentence and full-response synthesis modes. |
| `Packages/com.google.ai-embodiment/Runtime/VoiceBackend.cs` | Enum for backend selection | VERIFIED (14 lines, no stubs, wired) | GeminiNative and ChirpTTS values. Referenced throughout PersonaSession for routing decisions. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| ChirpTTSClient.cs | texttospeech.googleapis.com | UnityWebRequest POST with x-goog-api-key header | WIRED | TTS_ENDPOINT constant at line 23, SetRequestHeader at line 82. Full HTTP request/response cycle with error handling. |
| ChirpTTSClient.cs | Firebase API key | Constructor parameter from FirebaseApp.DefaultInstance.Options.ApiKey | WIRED | PersonaSession.Connect() retrieves ApiKey at line 154, passes to ChirpTTSClient constructor at line 155. |
| PersonaSession.cs | ChirpTTSClient.cs | _chirpClient.SynthesizeAsync call | WIRED | SynthesizeAndEnqueue calls SynthesizeAsync with voiceName, languageCode, voiceCloningKey at line 370. Result PCM fed to AudioPlayback at line 379. |
| PersonaSession.cs | AudioPlayback.cs | EnqueueAudio with Chirp PCM output | WIRED | Line 379: `_audioPlayback.EnqueueAudio(pcm)` for Chirp path. Line 657: `_audioPlayback.EnqueueAudio(chunk)` for GeminiNative path. Both paths feed the same ring buffer playback pipeline. |
| PersonaSession.cs | PersonaConfig.cs | voiceBackend and chirpSynthesisMode checks | WIRED | 4 checks for VoiceBackend.ChirpTTS (lines 152, 330, 707, 761). 3 checks for VoiceBackend.GeminiNative (lines 653, 664, 679). 2 checks for ChirpSynthesisMode (lines 331, 708). |
| PersonaConfigEditor.cs | ChirpVoiceList.cs | Voice and language data for dropdown population | WIRED | 4 references: Languages (line 111), GetVoiceDisplayNames (line 137), GetApiVoiceName (line 166), CustomVoice (line 170). |
| PersonaConfigEditor.cs | PersonaConfig.cs | SerializedProperty access to Chirp fields | WIRED | 16 FindProperty calls in OnEnable, all matching PersonaConfig field names including 7 Chirp-specific fields. |
| Editor asmdef | Runtime asmdef | Assembly reference | WIRED | com.google.ai-embodiment.editor.asmdef references "com.google.ai-embodiment" in its references array. |

### Requirements Coverage

| Requirement | Status | Blocking Issue |
|-------------|--------|----------------|
| VOICE-02: Chirp 3 HD TTS path -- text from LiveSession sent via HTTP to Cloud TTS, PCM audio returned | SATISFIED | None |
| VOICE-03: Voice backend selected per-persona in ScriptableObject config | SATISFIED | None |
| VOICE-04: ChirpTTSClient handles HTTP requests to texttospeech.googleapis.com via UnityWebRequest | SATISFIED | None |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| ChirpTTSClient.cs | 87 | `return null` | Info | Legitimate disposed guard -- ObjectDisposedException already thrown line 73, this is post-await safety check |
| ChirpTTSClient.cs | 184 | `return null` | Info | Legitimate missing-data case in ExtractAudioContent -- caller handles null with descriptive exception |

No blocker or warning anti-patterns found across any Phase 5 files.

### Human Verification Required

### 1. Chirp TTS Audio Quality

**Test:** Select ChirpTTS backend in PersonaConfig Inspector, configure a voice and language, run the scene, speak to the persona, and listen to the AI response audio.
**Expected:** AI voice should play clearly through AudioSource with the selected Chirp 3 HD voice character, without pops, gaps, or garbled audio.
**Why human:** Audio quality and voice character cannot be verified programmatically -- requires listening to actual playback.

### 2. Inspector Dropdown UX

**Test:** Create a PersonaConfig ScriptableObject, toggle VoiceBackend between GeminiNative and ChirpTTS, select different languages and voices, select "Custom" voice.
**Expected:** Chirp fields appear/disappear on backend toggle. Language and voice dropdowns populate correctly. Custom voice fields appear only when "Custom" selected. HelpBox messages display for synthesis mode selection.
**Why human:** Visual Inspector behavior and UX flow cannot be verified programmatically.

### 3. Sentence-by-Sentence vs Full-Response Latency

**Test:** Configure a persona with ChirpTTS backend, test with SentenceBySentence mode then FullResponse mode.
**Expected:** SentenceBySentence produces audio sooner (first sentence plays before full response is complete). FullResponse waits for entire AI response before any audio plays.
**Why human:** Latency perception and mode behavioral difference requires real-time observation.

### 4. TTS Failure Graceful Degradation

**Test:** Configure ChirpTTS backend without enabling Cloud TTS API in Google Cloud Console, then have a conversation.
**Expected:** Conversation continues with text events firing normally. OnError events fire with descriptive 403 error messages. No crash or session disconnect.
**Why human:** Error recovery behavior during live conversation requires runtime observation.

### Gaps Summary

No gaps found. All three must-haves are verified with full three-level artifact checks (existence, substantive implementation, wired to the system). All key links are confirmed wired. No stub patterns, no TODO/FIXME markers, no placeholder implementations detected.

The implementation is comprehensive:
- ChirpTTSClient (234 lines) is a complete HTTP client with MiniJSON serialization, dual voice path support (standard SSML vs custom plain text), base64 decoding, WAV header stripping with RIFF validation, and PCM float conversion.
- ChirpVoiceList (155 lines) provides the full inventory of 30 voices and 49 languages with API name building.
- PersonaConfig (59 lines) has all 6 Chirp-specific fields, the ChirpSynthesisMode enum, and the IsCustomChirpVoice convenience property.
- PersonaConfigEditor (206 lines) is a complete custom Inspector with conditional visibility, dropdown population from ChirpVoiceList, auto-synced API voice name, and custom voice field support.
- PersonaSession (791 lines) has full backend-gated audio routing: Gemini native audio discarded for Chirp path, output transcription text captured and routed through ChirpTTSClient, both synthesis modes implemented, clean lifecycle management (init/dispose), and interrupt handling.

---

_Verified: 2026-02-05T23:30:00Z_
_Verifier: Claude (gsd-verifier)_
