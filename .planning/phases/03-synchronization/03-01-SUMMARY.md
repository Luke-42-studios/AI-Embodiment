---
phase: 03-synchronization
plan: 01
subsystem: synchronization
tags: [sync-packet, sentence-boundary, audio-correlation, readonly-struct, sync-driver]

# Dependency graph
requires:
  - phase: 02-audio-pipeline
    provides: AudioPlayback.EnqueueAudio audio format (float[] 24kHz mono)
provides:
  - SyncPacket readonly struct (unified text/audio/function call container)
  - ISyncDriver interface for pluggable sync timing control
  - PacketAssembler class with sentence boundary buffering and audio accumulation
affects: [03-02 PersonaSession integration, 04 function calling routing, 05 Chirp TTS sync driver]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sentence boundary detection: punctuation (. ? !) + whitespace, with 500ms/20-char time-based fallback"
    - "Audio chunk accumulation: List<float[]> merged into single float[] on packet emission"
    - "Sync driver routing: ISyncDriver.OnPacketReady when driver registered, direct callback when not"

key-files:
  created:
    - Packages/com.google.ai-embodiment/Runtime/SyncPacket.cs
    - Packages/com.google.ai-embodiment/Runtime/ISyncDriver.cs
    - Packages/com.google.ai-embodiment/Runtime/PacketAssembler.cs
  modified: []

key-decisions:
  - "SyncPacket as readonly struct with constructor -- follows Firebase SDK convention, zero GC pressure"
  - "SyncPacketType enum discriminator (TextAudio, FunctionCall) -- avoids polymorphism per CONTEXT.md"
  - "PacketAssembler as plain C# class -- no Unity lifecycle needs, only Time.time for flush timeout"
  - "FindSentenceBoundary returns last boundary found -- allows greedy sentence emission per scan"
  - "FunctionCall packets emit immediately (no sentence buffering) -- function calls are discrete events"

patterns-established:
  - "Pattern: SyncPacket readonly struct as unified delivery type across the sync layer"
  - "Pattern: ISyncDriver interface for future Chirp TTS and Face Animation sync drivers"
  - "Pattern: PacketAssembler turn lifecycle (Start/Finish/Cancel/Reset) mirroring Gemini Live turn lifecycle"

# Metrics
duration: 2min
completed: 2026-02-05
---

# Phase 3 Plan 1: SyncPacket and PacketAssembler Summary

**SyncPacket readonly struct with 9-property constructor, ISyncDriver pluggable timing interface, and PacketAssembler with sentence boundary buffering and audio chunk accumulation**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-05T21:08:38Z
- **Completed:** 2026-02-05T21:10:36Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- SyncPacket readonly struct carrying correlated text, audio, and function call data with turn ID and sequence number
- ISyncDriver interface enabling future sync timing control (Chirp TTS, Face Animation) without architectural changes
- PacketAssembler with sentence boundary detection, audio accumulation, turn lifecycle management, and optional driver routing

## Task Commits

Each task was committed atomically:

1. **Task 1: SyncPacket readonly struct and ISyncDriver interface** - `7902f8d` (feat)
2. **Task 2: PacketAssembler with sentence boundary buffering and audio accumulation** - `08c9086` (feat)

## Files Created/Modified
- `Packages/com.google.ai-embodiment/Runtime/SyncPacket.cs` - SyncPacketType enum and SyncPacket readonly struct with 9 properties
- `Packages/com.google.ai-embodiment/Runtime/ISyncDriver.cs` - ISyncDriver interface with OnPacketReady, SetReleaseCallback, EstimatedLatencyMs
- `Packages/com.google.ai-embodiment/Runtime/PacketAssembler.cs` - Sentence boundary buffering, audio accumulation, turn lifecycle, sync driver routing

## Decisions Made
- SyncPacket uses a full 9-parameter constructor (no factory methods) -- keeps the readonly struct simple and explicit
- FindSentenceBoundary scans for the LAST boundary in the text rather than the first -- allows greedy emission when multiple sentences arrive in one chunk
- FunctionCall packets bypass sentence buffering and emit immediately -- function calls are discrete events, not text streams
- PacketAssembler.Reset() included beyond the 8 methods in the plan -- needed for clean disconnect state per research patterns

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- SyncPacket, ISyncDriver, and PacketAssembler ready for Plan 03-02 to integrate into PersonaSession
- PersonaSession.ProcessResponse needs routing through PacketAssembler (Plan 03-02)
- OnSyncPacket event needs to be added to PersonaSession (Plan 03-02)
- No blockers for next plan

---
*Phase: 03-synchronization*
*Completed: 2026-02-05*
