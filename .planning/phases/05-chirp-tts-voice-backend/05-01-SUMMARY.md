---
phase: 05-chirp-tts-voice-backend
plan: 01
subsystem: audio
tags: [cloud-tts, chirp-3-hd, unity-web-request, pcm-audio, voice-synthesis, minijson]

# Dependency graph
requires:
  - phase: 01-foundation-and-core-session
    provides: "UPM package structure, Firebase.AI asmdef, namespace AIEmbodiment"
  - phase: 02-audio-pipeline
    provides: "AudioPlayback with 24kHz ring buffer playback pipeline"
provides:
  - "ChirpTTSClient HTTP client for Cloud TTS synthesis returning PCM float[]"
  - "ChirpVoiceList with 30 Chirp 3 HD voices, 49 languages, API name builder"
affects: [05-02-PLAN, 05-03-PLAN]

# Tech tracking
tech-stack:
  added: [Google Cloud Text-to-Speech REST API v1]
  patterns: [UnityWebRequest with Awaitable async, MiniJSON for REST serialization, WAV header stripping for LINEAR16]

key-files:
  created:
    - Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs
    - Packages/com.google.ai-embodiment/Runtime/ChirpVoiceList.cs
  modified: []

key-decisions:
  - "MiniJSON (Google.MiniJson.dll) for JSON serialization -- already available via Firebase SDK, handles nested dictionaries cleanly"
  - "SSML wrapping for standard voices, plain text for custom/cloned voices (Pitfall 7 compliance)"
  - "RIFF header validation with fallback to raw PCM -- defensive against non-standard responses"
  - "OnError event for non-throwing error reporting -- allows conversation to continue on TTS failure"

patterns-established:
  - "Plain C# class with IDisposable for HTTP clients (follows PacketAssembler pattern)"
  - "Static readonly data class for Inspector-friendly inventory (ChirpVoiceList)"
  - "x-goog-api-key authentication via Firebase API key for Google Cloud APIs"

# Metrics
duration: 3min
completed: 2026-02-05
---

# Phase 5 Plan 1: Cloud TTS Client and Voice Data Summary

**ChirpTTSClient HTTP client for Chirp 3 HD synthesis with MiniJSON serialization, WAV header stripping, and ChirpVoiceList providing 30 voices and 49 language locales**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-05T23:00:38Z
- **Completed:** 2026-02-05T23:03:25Z
- **Tasks:** 2
- **Files created:** 2

## Accomplishments
- ChirpVoiceList provides complete inventory of 30 Chirp 3 HD voices and 49 language locales with API name builder
- ChirpTTSClient sends text to Cloud TTS REST API via UnityWebRequest, decodes base64 LINEAR16 audio, strips 44-byte WAV header, returns PCM float[] at 24kHz
- Dual voice path: standard voices use SSML wrapping with voice.name, custom/cloned voices use plain text with voice.voiceClone.voiceCloningKey
- Authentication via x-goog-api-key header using existing Firebase API key (zero extra developer config)

## Task Commits

Each task was committed atomically:

1. **Task 1: ChirpVoiceList static data** - `d90ace7` (feat)
2. **Task 2: ChirpTTSClient HTTP client** - `0d54df4` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/ChirpVoiceList.cs` - Static inventory of 30 Chirp 3 HD voice names, 49 language locales, API name builder, and Inspector dropdown helper
- `Packages/com.google.ai-embodiment/Runtime/ChirpTTSClient.cs` - HTTP client for Cloud TTS synthesis with MiniJSON serialization, base64 decoding, WAV header stripping, PCM conversion, OnError event, and IDisposable

## Decisions Made
- [05-01]: MiniJSON (Google.MiniJson.dll) for JSON serialization instead of manual string building -- already available via Firebase SDK, handles nested voice/audioConfig dictionaries cleanly
- [05-01]: SSML wrapping (`<speak>{text}</speak>`) for standard voices, plain text for custom/cloned voices -- per Research Pitfall 7 on SSML incompatibility
- [05-01]: RIFF header validation (0x52494646) before skipping 44 bytes, with fallback to raw PCM on missing header -- defensive against non-standard responses
- [05-01]: OnError event + throw pattern -- event fires for logging/UI, exception propagates for caller control; conversation continues on TTS failure per CONTEXT.md

## Deviations from Plan

None -- plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None -- no external service configuration required for this plan. (Note: developers will need to enable Cloud Text-to-Speech API in Google Cloud Console when integrating, but that is documented in Plan 03 integration.)

## Next Phase Readiness
- ChirpTTSClient ready for PersonaSession integration in Plan 03 (audio routing modification)
- ChirpVoiceList ready for PersonaConfigEditor dropdown in Plan 02 (Inspector UI)
- Both files use namespace AIEmbodiment and compile within the existing asmdef

---
*Phase: 05-chirp-tts-voice-backend*
*Completed: 2026-02-05*
