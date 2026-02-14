# Phase 11: Integration Verification - Research

**Researched:** 2026-02-13
**Domain:** Unity scene wiring, end-to-end integration validation, YAML scene files
**Confidence:** HIGH

## Summary

This phase rebuilds the AyaSampleScene.unity file from scratch and validates that the complete v0.8 pipeline works end-to-end with both voice backends (Gemini Native and Chirp TTS). The research domain is entirely internal to this codebase -- no external libraries are being added.

The existing scene file has specific wiring gaps that were identified by examining the YAML on disk. The critical gap is that `AudioPlayback._audioSource` is `{fileID: 0}` (null), meaning AI voice audio will never play. Additionally, no `AIEmbodimentSettings` asset exists in a Resources folder, so `PersonaSession.Connect()` will fail before reaching the WebSocket. The `AudioSource` component on the AyaSession GameObject also has `PlayOnAwake: 1`, which conflicts with AudioPlayback's initialization pattern that creates its own dummy clip.

**Primary recommendation:** Rebuild the scene YAML with all serialized references correctly wired, create the AIEmbodimentSettings asset in a Resources folder, synchronize the Samples~ canonical copy, and add connection status feedback to AyaSampleController for the state changes that AyaChatUI already handles but that are not surfaced during the intro-to-connect transition.

## Standard Stack

No new libraries or dependencies. This phase uses only existing project components:

### Core Components (all exist, no changes needed to source)
| Component | Location | Purpose | Wiring |
|-----------|----------|---------|--------|
| PersonaSession | Runtime/PersonaSession.cs | Session lifecycle, event routing | SerializeField: _config, _audioCapture, _audioPlayback |
| AudioCapture | Runtime/AudioCapture.cs | Microphone capture | No SerializeField deps |
| AudioPlayback | Runtime/AudioPlayback.cs | Streaming audio output | SerializeField: _audioSource |
| AyaSampleController | Samples~/AyaSampleController.cs | Scene orchestration | SerializeField: _session, _chatUI, _introAudioSource, _introClip |
| AyaChatUI | Samples~/AyaChatUI.cs | UI Toolkit chat UI | SerializeField: _uiDocument, _session |
| PersonaConfig | ScriptableObject asset | Persona configuration | Exists at Assets/AyaLiveStream/AyaPersonaConfig.asset |
| AIEmbodimentSettings | ScriptableObject asset | API key storage | MISSING -- must be created |

### UI Assets (all exist, no changes needed)
| Asset | Location | Purpose |
|-------|----------|---------|
| AyaPanel.uxml | Assets/AyaLiveStream/UI/AyaPanel.uxml | UI Toolkit layout |
| AyaPanel.uss | Assets/AyaLiveStream/UI/AyaPanel.uss | UI Toolkit styles |
| PanelSettings | Assets/UI Toolkit/PanelSettings.asset | UIDocument panel config |

## Architecture Patterns

### Scene File YAML Structure

Unity scene files are YAML documents with `%YAML 1.1` header and `%TAG !u! tag:unity3d.com,2011:` prefix. Each GameObject and Component gets a unique `fileID` that other components reference.

**Critical wiring pattern for serialized references:**
```yaml
# Component A references Component B via fileID
MonoBehaviour:
  m_GameObject: {fileID: <owning-gameobject>}
  _someField: {fileID: <target-component-fileID>}

# Cross-GameObject reference (ScriptableObject asset)
  _config: {fileID: 11400000, guid: <asset-guid>, type: 2}
```

### Recommended GameObject Hierarchy (rebuilt scene)

```
AyaSampleScene.unity
  Main Camera                    (Camera, AudioListener, URP Camera Data)
  Directional Light              (Light, URP Light Data)
  Global Volume                  (Volume)
  UIDocument                     (UIDocument, AyaChatUI)
  AyaSession                     (PersonaSession, AudioCapture, AudioPlayback, AudioSource)
  AyaSampleController            (AyaSampleController)
```

This is the same hierarchy as the existing scene -- 6 root GameObjects. The only changes are to serialized field values inside the YAML, not to the hierarchy.

### Wiring Matrix (what references what)

| Source Component | Source Field | Target | Target fileID |
|-----------------|--------------|--------|---------------|
| PersonaSession | _config | AyaPersonaConfig.asset | {fileID: 11400000, guid: 35b572ed..., type: 2} |
| PersonaSession | _audioCapture | AudioCapture (same GO) | same-GO reference |
| PersonaSession | _audioPlayback | AudioPlayback (same GO) | same-GO reference |
| AudioPlayback | _audioSource | AudioSource (same GO) | **MUST BE WIRED** |
| AyaSampleController | _session | PersonaSession (AyaSession GO) | cross-GO reference |
| AyaSampleController | _chatUI | AyaChatUI (UIDocument GO) | cross-GO reference |
| AyaSampleController | _introAudioSource | AudioSource (AyaSession GO) | cross-GO reference |
| AyaSampleController | _introClip | null (no clip provided) | {fileID: 0} -- OK |
| AyaChatUI | _uiDocument | UIDocument (same GO) | same-GO reference |
| AyaChatUI | _session | PersonaSession (AyaSession GO) | cross-GO reference |

### Two-AudioSource Pattern

The scene currently uses a single AudioSource for both intro playback and AI voice streaming. This creates a conflict:

- **Intro playback:** AyaSampleController uses `_introAudioSource` to play a pre-recorded clip via standard `AudioSource.Play()`. Requires `PlayOnAwake: false` since it is triggered programmatically.
- **AI voice streaming:** AudioPlayback.Initialize() creates a silent dummy AudioClip, sets `loop = true`, and calls `Play()` so that `OnAudioFilterRead` fires continuously for resampling.

Using the same AudioSource for both is problematic because:
1. AudioPlayback.Initialize() replaces the clip and starts playback -- if intro is still playing, it gets interrupted.
2. The intro finishes, then `_session.Connect()` is called, which calls `_audioPlayback.Initialize()`. This is safe because the sequence is: intro plays -> intro finishes -> Connect() -> Initialize(). The coroutine `PlayIntroThenGoLive` waits for intro to finish with `WaitWhile(() => _introAudioSource.isPlaying)`.

**Conclusion:** A single AudioSource works if the intro AudioSource IS a different AudioSource, OR if the timing guarantees hold. The current code uses `_introAudioSource` referencing the same AudioSource on AyaSession. This is safe because the intro completes before Connect(). However, it is cleaner to use a separate AudioSource for intro on the AyaSampleController GameObject. **Recommendation: keep the existing single-AudioSource pattern** since the intro-then-connect sequence is guaranteed safe, and adding a second AudioSource adds complexity without benefit.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Scene file editing | Manual YAML authoring from memory | Copy-and-fix existing scene YAML | fileID references are fragile; one wrong ID breaks the whole scene |
| Connection status display | New UI elements or separate status system | AyaChatUI.SetStatus() and HandleStateChanged | Already implemented -- just ensure the wiring is correct |
| PacketAssembler validation | Custom test harness | Debug.Log in HandleSyncPacket callback | PacketAssembler is already integrated; validation is observational |
| API key setup | Code-based key injection | AIEmbodimentSettings ScriptableObject in Resources folder | This is the established pattern from Phase 8 |

## Common Pitfalls

### Pitfall 1: AudioPlayback._audioSource is null
**What goes wrong:** AudioPlayback.Initialize() calls `_audioSource.clip = dummyClip` which throws NullReferenceException. No AI voice audio plays.
**Why it happens:** In the existing scene YAML, `_audioSource: {fileID: 0}`. The AudioSource component exists on the same GameObject but is not wired to the AudioPlayback SerializeField.
**How to avoid:** In the rebuilt scene, AudioPlayback's `_audioSource` must reference the AudioSource component on the same AyaSession GameObject via its fileID.
**Warning signs:** NullReferenceException in AudioPlayback.Initialize() at play time.

### Pitfall 2: Missing AIEmbodimentSettings asset
**What goes wrong:** `AIEmbodimentSettings.Instance` returns null because `Resources.Load<AIEmbodimentSettings>("AIEmbodimentSettings")` finds nothing. PersonaSession.Connect() logs error and returns without connecting.
**Why it happens:** No Resources folder exists in the project, and no AIEmbodimentSettings.asset has been created.
**How to avoid:** Create `Assets/Resources/AIEmbodimentSettings.asset` with a valid API key. The asset must be named exactly "AIEmbodimentSettings" (matching the `ResourcePath` const) and placed in any folder named "Resources" under Assets.
**Warning signs:** Console log: "PersonaSession: No API key configured."

### Pitfall 3: AudioSource PlayOnAwake conflicts with AudioPlayback
**What goes wrong:** The AudioSource plays silence on scene load (PlayOnAwake), then AudioPlayback.Initialize() replaces the clip and restarts. Minor issue but wasteful.
**Why it happens:** The existing scene has `m_PlayOnAwake: 1` on the AudioSource.
**How to avoid:** Set `m_PlayOnAwake: 0` on the AudioSource in the rebuilt scene. AudioPlayback.Initialize() will call Play() when needed.
**Warning signs:** Brief audio pop on scene load before connect.

### Pitfall 4: Samples~ not synchronized with Assets/AyaLiveStream
**What goes wrong:** The package's canonical sample source (Packages/com.google.ai-embodiment/Samples~/AyaLiveStream/) is out of sync with the actual working copy (Assets/AyaLiveStream/). When users import the sample from Package Manager, they get stale code.
**Why it happens:** Both directories contain the same files but edits during v0.8 phases were applied to both. The scene file only exists in Assets/Scenes/ (not in Samples~).
**How to avoid:** After rebuilding the scene, ensure all sample source files in Assets/AyaLiveStream/ are identical to Packages/.../Samples~/AyaLiveStream/. The scene file lives in Assets/Scenes/ only (standard Unity pattern for imported samples).
**Warning signs:** Package Manager sample import produces different behavior than the working project.

### Pitfall 5: YAML fileID collisions when rebuilding scene
**What goes wrong:** If fileIDs in the rebuilt scene collide with each other, Unity silently drops or corrupts components.
**Why it happens:** Manually writing YAML with arbitrary fileIDs.
**How to avoid:** Reuse the existing fileIDs from the current scene file. The IDs themselves are arbitrary -- what matters is internal consistency. By keeping the same IDs, we also preserve any external references (e.g., build settings listing the scene).
**Warning signs:** Missing components in Inspector, "Missing (Mono Script)" warnings.

### Pitfall 6: SceneRoots section missing or incorrect
**What goes wrong:** Unity 2022+ scene files include a `SceneRoots` entry listing all root Transforms. If this is missing or lists wrong fileIDs, the scene may not load correctly.
**Why it happens:** Overlooking the SceneRoots section at the bottom of the scene YAML.
**How to avoid:** Include the SceneRoots entry with all root Transform fileIDs listed.
**Warning signs:** Empty scene in Editor despite valid YAML above.

### Pitfall 7: Chirp TTS path -- PacketAssembler receives transcription but no audio
**What goes wrong:** In Chirp TTS mode, Gemini native audio is discarded by HandleAudioEvent (because `_ttsProvider != null`). PacketAssembler.AddAudio() is never called. SyncPackets are emitted with text but null audio arrays.
**Why it happens:** This is BY DESIGN. In Chirp TTS mode, audio comes from ChirpTTSClient, not from Gemini. PacketAssembler only correlates Gemini-native audio with transcription.
**How to avoid:** When validating Chirp TTS path, expect SyncPackets to have text but null Audio. The TTS audio goes directly to AudioPlayback via SynthesizeAndEnqueue, bypassing PacketAssembler.
**Warning signs:** This is not a bug -- it is expected behavior.

### Pitfall 8: PersonaConfig.modelName stale model version
**What goes wrong:** The model name in the config asset may reference a deprecated or removed model.
**Why it happens:** Gemini model versions rotate. The asset currently references `gemini-2.5-flash-native-audio-preview-12-2025`.
**How to avoid:** Verify the model name is current. The config asset already has the correct value from Phase 7, but this should be checked during verification.
**Warning signs:** 404 or "model not found" error in WebSocket connect.

## Code Examples

### Scene YAML: Correct AudioPlayback wiring
```yaml
# AudioPlayback component on AyaSession GameObject
# The critical fix: _audioSource must point to the AudioSource fileID
--- !u!114 &406905698
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 406905696}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: b4ab2db297c0a86f19772e35a4fb7d17, type: 3}
  m_Name:
  m_EditorClassIdentifier: com.google.ai-embodiment::AIEmbodiment.AudioPlayback
  _audioSource: {fileID: 406905700}  # <-- WAS {fileID: 0}, NOW points to AudioSource
```

### Scene YAML: AudioSource with PlayOnAwake off
```yaml
# AudioSource on AyaSession -- PlayOnAwake must be 0
--- !u!82 &406905700
AudioSource:
  # ... (other fields unchanged)
  m_PlayOnAwake: 0  # <-- WAS 1, NOW 0
```

### AIEmbodimentSettings asset YAML
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <AIEmbodimentSettings-script-guid>, type: 3}
  m_Name: AIEmbodimentSettings
  m_EditorClassIdentifier:
  _apiKey:
```
Note: The actual script GUID must match the AIEmbodimentSettings.cs meta file. The `_apiKey` is left empty -- the developer fills it in via Inspector.

### PacketAssembler validation approach
```csharp
// In AyaSampleController.Start(), subscribe to OnSyncPacket for validation logging:
_session.OnSyncPacket += (packet) =>
{
    Debug.Log($"[SyncPacket] Turn={packet.TurnId} Seq={packet.Sequence} " +
              $"Type={packet.Type} Text=\"{packet.Text}\" " +
              $"Audio={packet.Audio?.Length ?? 0} samples " +
              $"IsTurnEnd={packet.IsTurnEnd}");
};
```

### Expected SyncPacket patterns per backend

**Gemini Native Audio path:**
- SyncPackets have both Text (from outputTranscription) and Audio (from inlineData)
- Multiple TextAudio packets per turn (sentence-boundary splitting)
- Final packet has IsTurnEnd=true
- FunctionCall packets interleaved when AI calls functions

**Chirp TTS path:**
- SyncPackets have Text but Audio=null (Gemini native audio discarded)
- TTS audio goes directly to AudioPlayback via SynthesizeAndEnqueue
- Same sentence-boundary splitting and turn structure
- FunctionCall packets still arrive normally

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Firebase LiveSession | GeminiLiveClient (direct WebSocket) | v0.8, Phase 7-8 | All session management rewritten |
| MiniJSON | Newtonsoft.Json | v0.8, Phase 8 | ChirpTTSClient, GeminiLiveClient use JObject |
| VoiceBackend enum routing | _ttsProvider == null check | v0.8, Phase 9 | Decoupled audio routing in HandleAudioEvent |
| Firebase tool declarations | FunctionDeclaration fluent builder | v0.8, Phase 10 | WebSocket-native function calling |

## Open Questions

1. **Model availability**
   - What we know: `gemini-2.5-flash-native-audio-preview-12-2025` was set in Phase 7
   - What's unclear: Whether this specific model version is still available (preview models may rotate)
   - Recommendation: Attempt connection; if model fails, update PersonaConfig.modelName. This is a runtime concern, not a scene wiring concern.

2. **AIEmbodimentSettings script GUID**
   - What we know: The ScriptableObject class exists at `Packages/com.google.ai-embodiment/Runtime/AIEmbodimentSettings.cs`
   - What's unclear: The exact GUID from the .meta file is needed to create the .asset file
   - Recommendation: Look up the GUID from `AIEmbodimentSettings.cs.meta` during implementation

3. **Intro audio clip**
   - What we know: `_introClip: {fileID: 0}` -- no clip is assigned
   - What's unclear: Whether a clip should be provided or if the 1-second fallback delay is intentional
   - Recommendation: Keep `{fileID: 0}` (no clip). The fallback `WaitForSeconds(1f)` path works fine for development. Users can assign their own intro clip.

## Sources

### Primary (HIGH confidence)
- Direct examination of `Assets/Scenes/AyaSampleScene.unity` YAML -- identified all wiring gaps
- Direct examination of all runtime C# source files in `Packages/com.google.ai-embodiment/Runtime/`
- Direct examination of sample C# source files in `Assets/AyaLiveStream/` and `Packages/.../Samples~/AyaLiveStream/`
- Direct examination of `Assets/AyaLiveStream/AyaPersonaConfig.asset` -- confirmed current config values
- Project `STATE.md` and `ROADMAP.md` -- confirmed phase dependencies and completed work

### Secondary (MEDIUM confidence)
- Unity YAML scene file format understanding from training data -- verified against actual scene file structure

## Metadata

**Confidence breakdown:**
- Scene wiring gaps: HIGH -- directly verified from YAML on disk
- Component relationships: HIGH -- directly verified from C# source code
- PacketAssembler behavior per backend: HIGH -- directly traced through PersonaSession event handlers
- AIEmbodimentSettings requirement: HIGH -- directly verified (no Resources folder exists)

**Research date:** 2026-02-13
**Valid until:** indefinite -- this is codebase-internal research, not dependent on external libraries
