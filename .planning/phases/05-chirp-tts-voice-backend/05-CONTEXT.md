# Phase 5: Chirp TTS Voice Backend - Context

**Gathered:** 2026-02-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Alternative voice path — developers can select Chirp 3 HD as the TTS backend for a persona, routing Gemini Live text output through Cloud TTS HTTP API instead of Gemini's native audio. Includes per-persona backend selection, voice dropdown editor, custom voice cloning support, and sentence-level or full-response synthesis modes.

</domain>

<decisions>
## Implementation Decisions

### Voice Selection UX
- Dropdown in Inspector with hardcoded list of Chirp 3 HD voices (flat alphabetical)
- Separate language dropdown that filters the voice list (not fixed to en-US)
- Friendly short names in dropdown (e.g., "Achernar"), full Cloud TTS name stored internally
- "Custom" option in dropdown reveals two fields: custom voice name and cloning key
- Cloning key field always visible when Custom is selected (not behind foldout)
- Both custom voice name and cloning key fields on PersonaConfig ScriptableObject
- No voice preview/audition button — developer tests by running the scene

### TTS Request Strategy
- Developer chooses between sentence-by-sentence and full-response synthesis via PersonaConfig field (design-time setting)
- Sentence-by-sentence mode: synthesize each sentence as PacketAssembler emits it, queue audio back-to-back through ring buffer (no crossfade)
- Full-response mode: wait for complete text, synthesize once
- Sequential TTS requests only (no prefetch of next sentence)
- Fixed audio format: LINEAR16 PCM at 24kHz (matches existing playback pipeline) — no developer config
- Default Cloud TTS speed and pitch (no configurable fields)
- SSML enabled for standard Chirp 3 HD voices; researcher to verify which APIs support it (custom/cloned voices may not)
- On TTS failure: silent skip + fire error event, text still displays via OnSyncPacket, conversation continues

### Authentication & Config
- Goal: zero extra config for the developer — reuse existing Firebase credentials from google-services.json
- Researcher to investigate: best auth method for Cloud TTS (API key reuse vs Firebase Auth token vs other) and pick the easiest developer path
- Hardcoded standard endpoint (texttospeech.googleapis.com) — not configurable
- Cloning key stored on PersonaConfig alongside voice selection

### Claude's Discretion
- Exact auth implementation (researcher will determine best approach)
- ChirpTTSClient internal architecture
- How to handle the Gemini Live session config when Chirp is selected (text-only mode vs. suppressing inline audio)
- Error message formatting for missing TTS API permissions

</decisions>

<specifics>
## Specific Ideas

- "Whatever is easier for a developer — they should just be able to provide" (re: auth — minimize credential setup)
- Custom voice support is important: dropdown should have a Custom option that exposes cloning key
- Developer should be able to choose synthesis granularity (sentence vs full) as a design-time decision
- SSML awareness: user knows custom Chirp voices don't support SSML — researcher should verify exact boundaries

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-chirp-tts-voice-backend*
*Context gathered: 2026-02-05*
