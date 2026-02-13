# Phase 9: TTS Abstraction - Context

**Gathered:** 2026-02-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Developers can choose between Gemini native audio and custom TTS backends via a clean ITTSProvider interface, with ChirpTTSClient as the shipped implementation. When voice backend is ChirpTTS, Gemini native audio is discarded and outputTranscription text is routed through ITTSProvider for synthesis and playback.

</domain>

<decisions>
## Implementation Decisions

### Provider Registration
- VoiceBackend enum becomes { GeminiNative, ChirpTTS, Custom }
- Both code API (PersonaSession.SetTTSProvider) and Inspector field supported
- Custom backend shows a MonoBehaviour reference slot in Inspector (consistent with AudioCapture/AudioPlayback pattern)
- ChirpTTSClient handles both HD and Custom/cloned voices internally (one provider, both paths)

### Interface Contract
- ITTSProvider.SynthesizeAsync takes (text, voiceName, languageCode) -- common params only
- Cloning key and other provider-specific config passed at construction time, not through the interface
- Returns a TTSResult struct { float[] Samples, int SampleRate, int Channels } -- self-describing for safety across providers
- Streaming supported via callback pattern: Action<TTSResult> onAudioChunk callback parameter
- Errors are exceptions from SynthesizeAsync -- no OnError event on the interface (PersonaSession catches and routes)
- ITTSProvider extends IDisposable

### Synthesis Mode Ownership
- PersonaConfig decides the synthesis mode (not the provider)
- ChirpSynthesisMode renamed to TTSSynthesisMode (provider-agnostic)
- PersonaConfig.chirpSynthesisMode renamed to PersonaConfig.synthesisMode
- PersonaSession splits text for sentence-by-sentence mode (via PacketAssembler, like today) -- provider just synthesizes whatever text it receives
- Inspector hides synthesisMode when VoiceBackend is GeminiNative
- ChirpTTS selected: shows voice picker + language code + synthesis mode
- Custom selected: shows MonoBehaviour slot + synthesis mode
- GeminiNative selected: shows nothing extra

### Provider Lifecycle
- PersonaSession auto-creates ChirpTTSClient in Connect() when backend is ChirpTTS (like today)
- PersonaSession always disposes the active provider on Disconnect (whether it created it or developer supplied it)
- Provider is fixed for session lifetime -- must be set before Connect(), no mid-session swaps

### Claude's Discretion
- TTSResult struct exact field names and any convenience methods
- How the MonoBehaviour Inspector reference is serialized (interface reference patterns in Unity)
- Internal callback plumbing for streaming audio chunks
- Error message formatting and logging patterns

</decisions>

<specifics>
## Specific Ideas

- Auth for Chirp custom/cloned voices may require OAuth tokens instead of API keys -- researcher should verify Cloud TTS auth requirements for custom voices vs standard HD voices
- Streaming audio support desired -- researcher should check if Cloud TTS REST API supports streaming responses or if sentence-by-sentence splitting is the only latency optimization path

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 09-tts-abstraction*
*Context gathered: 2026-02-13*
