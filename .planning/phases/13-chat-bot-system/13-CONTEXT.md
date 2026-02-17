# Phase 13: Chat Bot System - Context

**Gathered:** 2026-02-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Chat bots post messages in the livestream chat with organic timing, per-bot personality, and optional dynamic responses to user input — creating the illusion of a small live audience. Scripted message pools are already authored in ChatBotConfig ScriptableObjects. This phase wires scheduling, burst timing, dynamic Gemini responses, and the TrackedChatMessage system. Narrative steering (bots nudging Aya toward beats) is Phase 16.

</domain>

<decisions>
## Implementation Decisions

### Dynamic response tone matching
- Pass the bot's `personality` field as system prompt context to Gemini — this is the sole voice-direction source
- Do NOT include scripted messages as few-shot examples; the personality description is sufficient
- Dynamic responses must match the scripted message length for that bot (Dad_John: short encouraging phrases, Miko: very short bursts, etc.)

### Dynamic response batching
- One Gemini structured output call per user speech event — returns an array of bot reactions
- Structured output includes: which bots react (1-3 depending on how engaging the user's input is), each bot's message text, and per-bot response delay (staggered timing so they don't all appear at once)
- Gemini decides how many bots react based on the quality/relevance of the user's speech
- All 6 bot personalities are included in the prompt context so Gemini can pick the most natural responders

### Dynamic response triggers
- Dynamic responses are triggered only by user push-to-talk speech — not by Aya's dialogue and not by other bots' messages
- No chain reactions between bots — keeps dynamic responses bounded and API costs predictable

### Claude's Discretion
- Exact structured output JSON schema for the batched response
- How to inject per-bot personality into a single batched prompt efficiently
- Burst timing algorithm (within the 0.8-3.0s delay range from roadmap)
- TrackedChatMessage data structure and tracking logic

</decisions>

<specifics>
## Specific Ideas

- The staggered timing in structured output makes the dynamic responses feel organic — some bots "type faster" than others, matching their `typingSpeed` field
- Lurker (Ghost404) is still eligible for dynamic responses but would naturally be selected less often by Gemini given its personality

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 13-chat-bot-system*
*Context gathered: 2026-02-17*
