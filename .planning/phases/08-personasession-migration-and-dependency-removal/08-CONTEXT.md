# Phase 8: PersonaSession Migration and Dependency Removal - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Rewire PersonaSession to use GeminiLiveClient (from Phase 7) instead of Firebase LiveSession. Remove all Firebase.AI references from runtime code. The project must compile at the end of this phase. The public API surface (events, methods, properties) stays functionally identical, though parameter types change where Firebase types are replaced.

</domain>

<decisions>
## Implementation Decisions

### API key configuration
- Project-wide settings asset: new `AIEmbodimentSettings` ScriptableObject (singleton)
- Discovered via `Resources.Load` from a known path (e.g. `Resources/AIEmbodimentSettings`)
- PersonaSession loads the API key automatically at connect time from this singleton
- API key field is password-masked in Inspector with a reveal toggle
- When Connect() is called with no API key configured: log a clear error message pointing to the settings asset path and how to create one; do not attempt connection

### Firebase cleanup scope
- **Full purge**: delete entire `Assets/Firebase/` directory, remove Firebase entries from `Packages/manifest.json`, remove Firebase .dlls, meta files, and any other Firebase references found in the project
- Clean slate: no Firebase code preserved for reference
- Claude should search the entire project for all Firebase touchpoints and remove everything found
- Sample scene (`Assets/AyaLiveStream/`) Firebase references cleaned up in this phase, not deferred to Phase 11

### Sample scene Firebase references
- AyaSampleController uses `FunctionDeclaration`, `Schema`, `Dictionary<string, Schema>` from Firebase.AI
- Stub these out with `// TODO: Phase 10` comment markers so the scene compiles
- Functions won't work until Phase 10 (Function Calling Migration) provides replacement types

### Compilation requirement
- Project MUST compile at the end of Phase 8
- Any code that references removed Firebase types must be stubbed, commented out, or replaced with temporary equivalents
- This applies to FunctionRegistry (uses `FunctionDeclaration`, `Tool`), SystemInstructionBuilder (uses `ModelContent`), PersonaSession (uses `LiveSession`, `ModelContent`), and the sample scene

### Claude's Discretion
- Event mapping between GeminiEvent types and PersonaSession public events
- Threading model change (Firebase ReceiveAsync background thread vs GeminiLiveClient ProcessEvents polling)
- Audio format conversion (float[] to PCM16 bytes for SendAudio)
- Internal architecture of the AIEmbodimentSettings editor (custom inspector details)
- Function declaration replacement type design (deferred to Phase 10, but stubs needed here)

</decisions>

<specifics>
## Specific Ideas

- API key experience should feel like other Unity SDK settings (e.g. how Addressables or Analytics have a project-wide settings asset in Resources)
- Password masking is important for screen-sharing safety during tutorials/streams

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 08-personasession-migration-and-dependency-removal*
*Context gathered: 2026-02-13*
