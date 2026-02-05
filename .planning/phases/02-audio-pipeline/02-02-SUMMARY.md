---
phase: 02-audio-pipeline
plan: 02
subsystem: audio
tags: [microphone, capture, pcm, 16khz, coroutine, permissions, android]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: UPM package structure and asmdef for compilation
provides:
  - AudioCapture MonoBehaviour with 16kHz mono mic recording and chunked PCM callback
affects: [02-03-integration, 03-synchronization]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Coroutine-based mic polling with looping AudioClip"
    - "Circular buffer wrap-around arithmetic for Microphone.GetPosition"
    - "Platform-specific permission handling via preprocessor directives"

key-files:
  created:
    - Packages/com.google.ai-embodiment/Runtime/AudioCapture.cs
  modified: []

key-decisions:
  - "System default microphone only (null device) per CONTEXT.md"
  - "100ms chunk accumulation before callback to prevent WebSocket flooding"
  - "Preprocessor guards for Android using directive to avoid compile errors on non-Android"

patterns-established:
  - "AudioCapture as raw data pipe: no amplitude tracking, no device selection"
  - "Event-based output (OnAudioCaptured callback) decoupled from send logic"

# Metrics
duration: 1min
completed: 2026-02-05
---

# Phase 2 Plan 2: AudioCapture Summary

**Microphone capture MonoBehaviour with 16kHz mono coroutine polling, cross-platform permission handling, and 100ms chunked PCM callback**

## Performance

- **Duration:** 1 min
- **Started:** 2026-02-05T20:23:43Z
- **Completed:** 2026-02-05T20:24:44Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- AudioCapture records from system default microphone at 16kHz mono via looping AudioClip
- Cross-platform microphone permission handling: Android runtime permissions with PermissionCallbacks, Desktop/Editor via Application.RequestUserAuthorization
- Coroutine-based polling accumulates 100ms chunks (1600 samples) before firing OnAudioCaptured callback
- Circular buffer wrap-around arithmetic prevents audio glitches at buffer boundary
- Clean start/stop lifecycle with OnDestroy safety net

## Task Commits

Each task was committed atomically:

1. **Task 1: AudioCapture -- microphone recording with coroutine polling and chunked output** - `08eb755` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/AudioCapture.cs` - Microphone capture MonoBehaviour with StartCapture/StopCapture, permission handling, coroutine polling, and OnAudioCaptured event

## Decisions Made
None - followed plan as specified.

## Deviations from Plan
None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- AudioCapture is ready for PersonaSession integration in Plan 02-03
- OnAudioCaptured callback will be wired to LiveSession.SendAudioAsync by PersonaSession
- Plan 02-01 (AudioPlayback/RingBuffer) is independent and can execute in any order relative to this plan

---
*Phase: 02-audio-pipeline*
*Completed: 2026-02-05*
