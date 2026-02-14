# Phase 11: Integration Verification - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

The complete v0.8 package works end-to-end in the sample scene with both voice backends. AyaLiveStream sample scene connects, sends/receives audio, and displays transcription text using the WebSocket transport. PacketAssembler produces correct SyncPackets from the new transcription streams for both Gemini native audio and Chirp TTS paths.

</domain>

<decisions>
## Implementation Decisions

### Sample scene scope
- Keep all existing features: function calling (emote, start_movie, start_drawing), pre-recorded intro playback, push-to-talk (spacebar), goal injection after 3 exchanges, chat UI
- The sample demonstrates the full API surface, not a minimal transport test

### Scene file approach
- Rebuild the scene file from scratch -- do not patch the existing broken wiring
- Fresh GameObjects with all serialized references wired correctly

### Connection status feedback
- Use AyaChatUI.SetStatus() to show connection state changes (connecting, connected, disconnected, error)
- No new UI elements needed -- leverage existing chat UI status bar

### Voice backend switching
- Inspector-only configuration via PersonaConfig before entering play mode
- No runtime toggle button or key -- developer picks Gemini Native or Chirp TTS in the Inspector
- Both backends must work when configured, but the sample doesn't switch mid-session

### Claude's Discretion
- PacketAssembler stream validation approach (how to verify both paths produce correct SyncPackets)
- Verification methodology and edge cases to cover
- Any AyaChatUI adjustments needed for the rebuilt scene
- Exact GameObject hierarchy and component layout for the rebuilt scene

</decisions>

<specifics>
## Specific Ideas

No specific requirements -- open to standard approaches.

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope.

</deferred>

---

*Phase: 11-integration-verification*
*Context gathered: 2026-02-13*
