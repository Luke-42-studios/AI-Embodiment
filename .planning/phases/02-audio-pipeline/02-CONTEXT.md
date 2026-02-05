# Phase 2: Audio Pipeline - Context

**Gathered:** 2026-02-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Microphone capture, streaming audio playback via ring buffer, and Gemini native audio voice path. User speaks into microphone, audio streams to Gemini Live, and AI voice response plays back through AudioSource without gaps or artifacts. User input transcript exposed via event.

</domain>

<decisions>
## Implementation Decisions

### Session audio lifecycle
- Push-to-talk model: developer controls when audio flows via `PersonaSession.StartListening()` / `StopListening()`
- Developer can hold listening open permanently for always-on behavior, but default interaction is gated
- Gemini handles barge-in natively on server side — we always stream audio up and always play audio down, no client-side interruption logic
- AudioCapture and AudioPlayback are separate MonoBehaviour components, wired to PersonaSession via Inspector references (explicit, no auto-discovery)
- Audio components are optional — if not assigned, session falls back to text-only mode (Phase 1 behavior preserved)
- PersonaSession auto-stops AudioCapture and AudioPlayback on disconnect — clean teardown
- Push-to-talk API lives on PersonaSession (not AudioCapture) — session is the main developer-facing API
- PersonaSession fires OnAISpeakingStarted / OnAISpeakingStopped events for animation triggers and UI
- PersonaSession fires OnUserSpeakingStarted / OnUserSpeakingStopped events — based on Gemini server signals, not local amplitude detection
- PersonaSession fires OnUserTranscript(string) event for Gemini's speech-to-text of user input

### Microphone behavior
- System default microphone only — no device selection API
- AudioCapture handles microphone permission requests automatically, fires event on denied
- Target platforms: Editor + Desktop (Windows, Mac, Linux) + Android
- AudioCapture is a raw data pipe — no amplitude tracking or debug properties

### Claude's Discretion
- Ring buffer sizing and write-ahead watermark tuning
- Audio format details (sample rate, chunk size) based on Gemini API requirements
- Streaming playback implementation details
- Error handling for mic access failures and audio device issues
- AudioPlayback internal buffering strategy

</decisions>

<specifics>
## Specific Ideas

- Gemini Live handles conversational turn-taking server-side — our pipeline is a faithful bidirectional audio pipe, not a conversation manager
- Push-to-talk pattern aligns with game input systems where developers bind StartListening/StopListening to their own input actions

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-audio-pipeline*
*Context gathered: 2026-02-05*
