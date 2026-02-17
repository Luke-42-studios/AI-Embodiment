# Phase 15: Scene Transition & Animation - Context

**Gathered:** 2026-02-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Aya triggers pre-authored animations via Gemini function calls during the livestream, and the narrative climax triggers a scene transition to the movie clip. Animation handlers log/toast in the sample for developer extensibility. Scene transition is a clean exit (no additive loading, no WebSocket preservation).

</domain>

<decisions>
## Implementation Decisions

### Animation function calls
- Animations defined via ScriptableObject (consistent with ChatBotConfig pattern) — lists available animations with name and description for Gemini
- Sample includes 3-5 named animations (wave, point, laugh, think, etc.) to give Gemini variety and show developers the pattern
- Function call handlers log to console AND show a toast message in the UI so the animation trigger is visible during the demo
- Toast call is the replaceable hook — developers swap the toast for an actual Animator clip trigger when they have a 3D model

### Movie clip scene transition
- Clean exit: livestream scene fully unloads, movie clip scene loads independently, WebSocket disconnects — session is over
- No additive scene loading needed (roadmap originally planned this, user simplified to clean exit)
- No pre-loading needed — a brief loading screen/moment is acceptable
- Instant cut transition — no fade to black, no crossfade. Livestream disappears, movie clip appears

### Claude's Discretion
- Specific animation names and descriptions beyond the core examples
- Toast message visual design and duration
- Scene loading implementation details (loading indicator style, etc.)
- How the narrative director signals the scene transition trigger

</decisions>

<specifics>
## Specific Ideas

- "The library lets us set what animations she has access to, these are then called as functions by Gemini" — the existing package pattern for function calls should be leveraged
- "Just print some log and make sure it's easy for a developer to hook it up to an animator later" — developer extensibility is the priority, not visual polish
- Toast message approach: visible feedback during demo that maps 1:1 to where a real animation call would go

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 15-scene-transition-animation*
*Context gathered: 2026-02-17*
