---
phase: 02-audio-pipeline
plan: 01
subsystem: audio
tags: [ring-buffer, resampling, OnAudioFilterRead, SPSC, streaming-playback, audio-thread]

# Dependency graph
requires:
  - phase: 01-foundation-and-core-session
    provides: UPM package structure, asmdef, AIEmbodiment namespace
provides:
  - AudioRingBuffer lock-free SPSC circular buffer for float audio samples
  - AudioPlayback MonoBehaviour with OnAudioFilterRead streaming and 24kHz-to-system-rate resampling
affects: [02-02-AudioCapture, 02-03-PersonaSession-integration, 03-synchronization]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SPSC ring buffer with volatile int positions for audio thread safety"
    - "OnAudioFilterRead with pre-allocated buffers for zero-allocation audio thread"
    - "Linear interpolation resampling for sample rate conversion"
    - "Write-ahead watermark buffering for streaming audio jitter absorption"
    - "Dummy silent AudioClip trick to activate OnAudioFilterRead"

key-files:
  created:
    - Packages/com.google.ai-embodiment/Runtime/AudioRingBuffer.cs
    - Packages/com.google.ai-embodiment/Runtime/AudioPlayback.cs
  modified: []

key-decisions:
  - "Ring buffer capacity 2 seconds (48000 samples at 24kHz) for network jitter absorption"
  - "Watermark 150ms (3600 samples) balances latency vs jitter tolerance"
  - "Pre-allocated 4096-sample resample buffer covers all common Unity audio configurations"
  - "IsPlaying checks both buffer availability and buffering state for drain detection"

patterns-established:
  - "Audio thread zero-allocation: no new, no LINQ, no Unity API calls in OnAudioFilterRead"
  - "Resample position tracking with fractional accumulation across callbacks for pitch-accurate continuity"

# Metrics
duration: 2min
completed: 2026-02-05
---

# Phase 2 Plan 1: AudioRingBuffer and AudioPlayback Summary

**Lock-free SPSC ring buffer and streaming AudioPlayback with OnAudioFilterRead resampling from 24kHz Gemini audio to system sample rate**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-05T20:23:41Z
- **Completed:** 2026-02-05T20:25:18Z
- **Tasks:** 2
- **Files created:** 2

## Accomplishments
- Thread-safe single-producer single-consumer ring buffer with volatile int positions, modulo wrap-around, and zero-fill on underrun
- Streaming AudioPlayback MonoBehaviour that resamples 24kHz Gemini audio to system sample rate via linear interpolation in OnAudioFilterRead
- Write-ahead watermark (150ms) prevents pops, clicks, and silence gaps during streaming playback
- Zero allocations on audio thread -- all buffers pre-allocated in Initialize

## Task Commits

Each task was committed atomically:

1. **Task 1: AudioRingBuffer -- thread-safe SPSC ring buffer** - `1045b0a` (feat)
2. **Task 2: AudioPlayback -- streaming playback with OnAudioFilterRead resampling** - `ec042f8` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/AudioRingBuffer.cs` - Lock-free SPSC circular buffer for float audio samples with Write, Read, Available, and Clear operations
- `Packages/com.google.ai-embodiment/Runtime/AudioPlayback.cs` - MonoBehaviour streaming playback with OnAudioFilterRead, linear interpolation resampling, watermark buffering, and SerializeField AudioSource

## Decisions Made
- **Ring buffer capacity = 2 seconds:** 48,000 samples at 24kHz absorbs network jitter up to ~2 seconds without overflow
- **Watermark = 150ms:** 3,600 samples provides enough buffer to absorb typical packet jitter while keeping initial latency low
- **Resample buffer pre-allocation = 4096 samples:** Conservative size covers all common Unity audio configurations (1024-sample callbacks at 48kHz = ~512 source samples needed, well within 4096)
- **IsPlaying = Available > 0 OR not buffering:** Returns true while actively playing, enabling PersonaSession to detect when audio has fully drained after turn complete

## Deviations from Plan

None -- plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None -- no external service configuration required.

## Next Phase Readiness
- AudioRingBuffer and AudioPlayback are ready for PersonaSession integration in Plan 02-03
- AudioCapture (Plan 02-02) is independent and can proceed in parallel -- no dependencies between 02-01 and 02-02
- No existing files were modified -- PersonaSession integration deferred to Plan 02-03

---
*Phase: 02-audio-pipeline*
*Completed: 2026-02-05*
