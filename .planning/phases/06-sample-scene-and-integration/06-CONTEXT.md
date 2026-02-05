# Phase 6: Sample Scene and Integration - Context

**Gathered:** 2026-02-05
**Status:** Ready for planning

<domain>
## Phase Boundary

A UPM sample scene demonstrating the full AI Embodiment pipeline end-to-end. Developer installs the package, imports the sample via Package Manager, and runs a scene featuring Aya — a bubbly digital artist — with synchronized voice, text, animation function calls, and conversational goals. The scene proves the entire package works together in a live-stream-style interaction.

</domain>

<decisions>
## Implementation Decisions

### Scene content
- 2D UI avatar panel for Aya — dialogue panel with character name and text, no portrait image
- Blank/minimal environment — default camera background or solid color, all focus on the UI
- No art assets shipped — text-only panel, no avatar image
- UPM sample (Samples~ folder in package.json) — developer imports via Package Manager UI, standard convention

### Demo script / conversation flow
- Aya persona based on `/home/cachy/workspaces/projects/persona/samples/personas/aya.json` — bubbly digital artist, early 20s, live stream energy
- Three function calls registered:
  1. **emote(animation_name)** — triggers animations from Aya's animation list (idle, wave, think, talk, laugh, shrug, fidgets, nods_emphatically, leans_forward, etc.)
  2. **start_movie()** — triggers the "movie cutaway" moment (Princess Bride style — interview cuts to movie)
  3. **start_drawing()** — Aya returns to drawing when idle, ambient live-stream behavior
- Pre-recorded intro: ships as an audio clip asset in the sample — plays before the live session starts (deterministic, no latency)
- Flow: pre-recorded intro + animation → transition to live stream mode → push-to-talk enabled → user speaks → sees their transcription streaming → Aya responds
- Conversational goal (steer toward life story of drawing her characters) activates **after warm-up**, not from the start — demonstrates dynamic goal injection at runtime

### UI and feedback
- Dual text display: scrolling chat log (both user and Aya messages) + live stream chat feel
- User's transcribed speech streams in real-time as Gemini transcribes (shows STT pipeline working)
- Aya's name/panel border glows or highlights while audio is playing — visual speaking state indicator
- Push-to-talk: both on-screen UI button AND keyboard key (spacebar) — works for mouse and keyboard users

### Claude's Discretion
- Exact UI layout, colors, and typography for the dialogue panel and chat log
- Intro animation timing and transition to live mode
- How "start_movie" and "start_drawing" function calls manifest visually in the sample (log message, UI change, etc.)
- Chat log scrolling behavior and message formatting
- Exact warm-up duration or exchange count before goal activates

</decisions>

<specifics>
## Specific Ideas

- "Princess Bride style" — interview/live-stream moments that cut to a movie; Aya should be able to trigger when the movie starts via `start_movie()` function call
- Aya personality from existing persona definition: bubbly, creative, encouraging, uses "totally", "awesome", "honestly", "literally obsessed" — no emojis in speech (TTS reads them awkwardly)
- Live stream aesthetic — she's drawing while chatting, talks to "chat" not just one person, streamer energy
- If nobody talks to her, she goes back to drawing (start_drawing function call) — ambient idle behavior

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 06-sample-scene-and-integration*
*Context gathered: 2026-02-05*
