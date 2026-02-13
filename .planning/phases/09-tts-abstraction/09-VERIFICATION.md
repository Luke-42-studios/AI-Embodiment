---
phase: 09-tts-abstraction
verified: 2026-02-13T20:39:00Z
status: passed
score: 7/7 must-haves verified
---

# Phase 9: TTS Abstraction Verification Report

**Phase Goal:** Developers can choose between Gemini native audio and custom TTS backends via a clean ITTSProvider interface, with ChirpTTSClient as the shipped implementation
**Verified:** 2026-02-13T20:39:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | ITTSProvider interface exists with SynthesizeAsync returning Awaitable\<TTSResult\> | VERIFIED | `ITTSProvider.cs` line 10-28: `interface ITTSProvider : IDisposable` with `Awaitable<TTSResult> SynthesizeAsync(string text, string voiceName, string languageCode, Action<TTSResult> onAudioChunk = null)` |
| 2 | ChirpTTSClient implements ITTSProvider with Newtonsoft.Json serialization | VERIFIED | `ChirpTTSClient.cs` line 22: `class ChirpTTSClient : IDisposable, ITTSProvider`; lines 3-4: `using Newtonsoft.Json; using Newtonsoft.Json.Linq;`; zero MiniJSON references |
| 3 | When voice backend is ChirpTTS, Gemini native audio is discarded and outputTranscription text is routed through ITTSProvider | VERIFIED | `PersonaSession.cs` line 440: `if (_ttsProvider == null)` guards native audio path; line 455: `// If _ttsProvider != null: discard Gemini audio`; line 481: `if (_ttsProvider != null) { _ttsTextBuffer.Append(text); }` accumulates transcription for TTS |
| 4 | When voice backend is Custom, a developer-supplied ITTSProvider receives synthesis requests | VERIFIED | `PersonaSession.cs` lines 182-189: `else if (_config.voiceBackend == VoiceBackend.Custom) { _ttsProvider = _config.CustomTTSProvider; }` -- once assigned, all TTS routing uses `_ttsProvider` generically (same path as ChirpTTS) |
| 5 | When voice backend is GeminiNative, no TTS provider is active and native audio plays directly | VERIFIED | `PersonaSession.cs` line 190: `// GeminiNative: _ttsProvider remains null`; line 440-454: when `_ttsProvider == null`, audio goes to `_audioPlayback.EnqueueAudio(ev.AudioData)` directly |
| 6 | VoiceBackend enum has three values: GeminiNative, ChirpTTS, Custom | VERIFIED | `VoiceBackend.cs` lines 8-15: all three values present with XML docs |
| 7 | PersonaConfig exposes synthesisMode (TTSSynthesisMode) and CustomTTSProvider property | VERIFIED | `PersonaConfig.cs` line 39: `public TTSSynthesisMode synthesisMode`; line 47-53: `_customTTSProvider` field + `public ITTSProvider CustomTTSProvider => _customTTSProvider as ITTSProvider` |

**Score:** 7/7 truths verified

### Required Artifacts

| Artifact | Expected | Exists | Substantive | Wired | Status |
|----------|----------|--------|-------------|-------|--------|
| `Packages/com.google.ai-embodiment/Runtime/ITTSProvider.cs` | ITTSProvider interface, TTSResult struct, TTSSynthesisMode enum | YES | 67 lines, no stubs, 3 exported types | Imported by ChirpTTSClient, PersonaSession, PersonaConfig, PersonaConfigEditor | VERIFIED |
| `Packages/com.google.ai-embodiment/Runtime/VoiceBackend.cs` | Three-value VoiceBackend enum | YES | 17 lines, complete enum | Used in PersonaConfig, PersonaSession, PersonaConfigEditor | VERIFIED |
| `Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs` | ITTSProvider implementation with Newtonsoft.Json | YES | 225 lines, full HTTP client, no stubs | Created in PersonaSession.Connect(), implements ITTSProvider | VERIFIED |
| `Packages/com.google.ai-embodiment/Runtime/PersonaConfig.cs` | Config with TTSSynthesisMode and CustomTTSProvider | YES | 55 lines, synthesisMode field + CustomTTSProvider property | Read by PersonaSession and PersonaConfigEditor | VERIFIED |
| `Packages/com.google.ai-embodiment/Runtime/PersonaSession.cs` | Provider-agnostic TTS routing via ITTSProvider | YES | 775 lines, _ttsProvider field, full routing | Central hub, wired to all TTS types | VERIFIED |
| `Packages/com.google.ai-embodiment/Editor/PersonaConfigEditor.cs` | Custom backend UI with validation | YES | 254 lines, DrawCustomFields method, ITTSProvider validation | Editor for PersonaConfig, references all voice backend types | VERIFIED |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| ChirpTTSClient.cs | ITTSProvider | interface implementation | WIRED | Line 22: `class ChirpTTSClient : IDisposable, ITTSProvider` |
| PersonaSession.cs | ITTSProvider.SynthesizeAsync | `_ttsProvider` field | WIRED | Line 599: `TTSResult result = await _ttsProvider.SynthesizeAsync(text, voiceName, languageCode)` |
| PersonaSession.cs | PersonaConfig.CustomTTSProvider | Connect() provider resolution | WIRED | Line 184: `_ttsProvider = _config.CustomTTSProvider` |
| PersonaSession.cs | AudioPlayback.EnqueueAudio | SynthesizeAndEnqueue after TTSResult | WIRED | Line 609: `_audioPlayback.EnqueueAudio(result.Samples)` |
| PersonaConfig.cs | ITTSProvider | CustomTTSProvider property cast | WIRED | Line 53: `_customTTSProvider as ITTSProvider` |
| PersonaSession.cs | SetTTSProvider API | public method with state guard | WIRED | Lines 292-300: method exists, checks Disconnected state, sets `_ttsProvider` |
| PersonaSession.cs | TTS text accumulation | HandleOutputTranscription | WIRED | Line 481-484: `_ttsProvider != null` check, appends to `_ttsTextBuffer` |
| PersonaSession.cs | TTS full-response synthesis | HandleTurnCompleteEvent | WIRED | Lines 504-511: `_ttsProvider != null && synthesisMode == FullResponse && _ttsTextBuffer.Length > 0` triggers SynthesizeAndEnqueue |
| PersonaSession.cs | TTS sentence-by-sentence synthesis | HandleSyncPacket | WIRED | Lines 559-565: `_ttsProvider != null && synthesisMode == SentenceBySentence` triggers SynthesizeAndEnqueue |
| PersonaConfigEditor.cs | VoiceBackend.Custom | DrawCustomFields | WIRED | Lines 104-107: `else if (backend == VoiceBackend.Custom) { DrawCustomFields(); }` |
| PersonaConfigEditor.cs | ITTSProvider validation | DrawCustomFields | WIRED | Line 220: `if (mb != null && !(mb is ITTSProvider))` with error HelpBox |

### Requirements Coverage

| Requirement | Description | Status | Evidence |
|-------------|-------------|--------|----------|
| TTS-01 | ITTSProvider interface with SynthesizeAsync returning PCM audio | SATISFIED | ITTSProvider.cs: interface with `Awaitable<TTSResult> SynthesizeAsync(...)`, TTSResult contains PCM float[] Samples |
| TTS-02 | ChirpTTSClient implements ITTSProvider | SATISFIED | ChirpTTSClient.cs line 22: `class ChirpTTSClient : IDisposable, ITTSProvider` |
| TTS-03 | Chirp TTS path: discard Gemini native audio, route outputTranscription to ITTSProvider | SATISFIED | PersonaSession.cs: HandleAudioEvent discards when `_ttsProvider != null`; HandleOutputTranscription accumulates text; SynthesizeAndEnqueue routes through `_ttsProvider` |
| TTS-04 | ChirpTTSClient uses Newtonsoft.Json for serialization (MiniJSON removed) | SATISFIED | ChirpTTSClient.cs uses `Newtonsoft.Json` and `Newtonsoft.Json.Linq`; zero MiniJSON references in the file |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| ITTSProvider.cs | 57 | "ChirpSynthesisMode" in doc comment | Info | Historical reference in XML doc, not code. No impact. |
| PersonaSession.cs | 281 | TODO: Phase 10 | Info | Function declaration parameter -- Phase 10 scope, not Phase 9 |
| PersonaSession.cs | 660 | TODO: Phase 10 | Info | Function response sending -- Phase 10 scope, not Phase 9 |
| PersonaSession.cs | 676 | TODO: Phase 10 | Info | Mid-session instruction updates -- Phase 10 scope, not Phase 9 |

No blocker or warning anti-patterns found. All TODOs belong to Phase 10 (Function Calling and Goals Migration) and are expected to exist at this point.

### Negative Verification (Legacy Removal)

| Pattern | Scope | Expected | Actual | Status |
|---------|-------|----------|--------|--------|
| `ChirpSynthesisMode` (code) | Entire package | 0 code matches | 0 code matches (1 doc comment in ITTSProvider.cs) | VERIFIED |
| `_chirpClient` | Entire package | 0 matches | 0 matches | VERIFIED |
| `HandleChirpError` | Entire package | 0 matches | 0 matches | VERIFIED |
| `OnError` event | ChirpTTSClient.cs | 0 matches | 0 matches | VERIFIED |
| `MainThreadDispatcher` | PersonaSession.cs | 0 matches | 0 matches | VERIFIED |
| `MiniJSON` | ChirpTTSClient.cs | 0 matches | 0 matches | VERIFIED |
| `_chirpTextBuffer` | PersonaSession.cs | 0 matches | 0 matches | VERIFIED |
| `_chirpSynthesizing` | PersonaSession.cs | 0 matches | 0 matches | VERIFIED |
| `_chirpSynthesisMode` | PersonaConfigEditor.cs | 0 matches | 0 matches | VERIFIED |

### Human Verification Required

### 1. Custom TTS Provider Inspector Workflow

**Test:** In Unity Editor, create a PersonaConfig asset, set VoiceBackend to Custom, assign a MonoBehaviour that does NOT implement ITTSProvider.
**Expected:** Error HelpBox appears: "{ClassName} does not implement ITTSProvider."
**Why human:** Requires Unity Editor runtime to test Inspector validation.

### 2. ChirpTTS Audio Playback End-to-End

**Test:** Connect a PersonaSession with VoiceBackend.ChirpTTS, send a text prompt, verify audio plays.
**Expected:** Gemini native audio is silently discarded. outputTranscription text is routed through ChirpTTSClient. TTSResult audio is enqueued to AudioPlayback. Audio plays at correct speed.
**Why human:** Requires live Gemini API connection and audio output hardware.

### 3. GeminiNative Audio Unaffected

**Test:** Connect a PersonaSession with VoiceBackend.GeminiNative, verify audio plays as before.
**Expected:** Native Gemini audio routes directly to AudioPlayback. No TTS provider involvement. No regressions from Phase 9 changes.
**Why human:** Requires live Gemini API connection.

### 4. SetTTSProvider API

**Test:** Call `personaSession.SetTTSProvider(myProvider)` before Connect(), then Connect().
**Expected:** The custom provider receives SynthesizeAsync calls instead of the config-default provider.
**Why human:** Requires runtime integration testing.

### Gaps Summary

No gaps found. All 7 observable truths are verified at all three levels (existence, substantive, wired). All 4 requirements (TTS-01 through TTS-04) are satisfied. All legacy patterns have been fully removed. The ITTSProvider interface is complete with SynthesizeAsync returning TTSResult, ChirpTTSClient implements it with Newtonsoft.Json serialization, PersonaSession routes TTS through the provider-agnostic _ttsProvider field, and the Inspector supports all three VoiceBackend values with Custom backend validation.

---

_Verified: 2026-02-13T20:39:00Z_
_Verifier: Claude (gsd-verifier)_
