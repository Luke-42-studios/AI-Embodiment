# Phase 3: Synchronization - Context

**Gathered:** 2026-02-05
**Status:** Ready for planning

<domain>
## Phase Boundary

PacketAssembler correlates text, audio, and event timing from Gemini Live into unified SyncPackets. Developers subscribe to a single event on PersonaSession and receive everything needed for subtitles, audio playback, and animation triggers. This phase implements the ISyncDriver interface and the Gemini native audio driver only — Chirp TTS driver is Phase 5.

</domain>

<decisions>
## Implementation Decisions

### SyncPacket as single delivery mechanism
- SyncPacket is the unified container: subtitle text, PCM audio data, function call events, and transcript
- Developer subscribes to a single `OnSyncPacket` event on PersonaSession — packet has a type field (text/audio, function call)
- Function calls get their own dedicated packet — not mixed into text/audio packets
- Each packet carries a turn ID so developers can group content per AI response turn

### Sync driver architecture
- ISyncDriver interface — any component can register as the sync driver that paces packet release
- PacketAssembler receives all events from Gemini, assembles SyncPackets, and the active sync driver controls WHEN packets release to the developer
- Highest-latency driver wins automatically — system detects which driver has the longest pipeline
- If no external driver is registered (pure Gemini native audio), packets release immediately
- Gemini is always the event source (ground truth for timing). Other APIs (Chirp, Face Animation) are processors that add latency. Drivers don't create new events — they gate when existing events get released

### Dual-path behavior (by voice backend)
- Gemini native audio: text + audio arrive together — stream SyncPackets immediately (buffered to sentence boundaries)
- Chirp TTS: text arrives first from Gemini, audio comes later from Chirp — hold ALL events until Chirp audio arrives, then release packets synced to audio timing
- Phase 3 implements Gemini native driver only; Chirp driver added in Phase 5

### Sentence boundary buffering
- For Gemini native audio path, buffer incoming chunks to sentence-like boundaries before firing packets
- Cleaner subtitle text and natural points for animation event alignment

### Developer routes audio
- SyncPackets carry PCM audio data but the developer is responsible for routing it to AudioPlayback
- No automatic audio routing — developer has full control over what happens with each packet component

### Claude's Discretion
- SyncPacket struct/class design and field naming
- Sentence boundary detection algorithm
- Internal queue/buffer implementation for packet assembly
- Turn ID generation strategy
- ISyncDriver interface method signatures

</decisions>

<specifics>
## Specific Ideas

- "Gemini is always the main controller for events/timing — most work comes from Gemini and we sync other APIs to it"
- Future AI Face Animation library will add its own processing delay — the sync driver system must support this becoming the new highest-latency driver without architectural changes
- The pluggable driver model is designed to avoid rewrites when new latency-adding components arrive

</specifics>

<deferred>
## Deferred Ideas

- AI Face Animation sync driver — future phase (not yet scoped in roadmap)
- Chirp TTS sync driver — Phase 5 (when Chirp TTS client exists)

</deferred>

---

*Phase: 03-synchronization*
*Context gathered: 2026-02-05*
