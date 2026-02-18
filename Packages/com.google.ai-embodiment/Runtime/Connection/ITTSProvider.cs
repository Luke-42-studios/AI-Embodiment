using System;
using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Abstraction for text-to-speech synthesis backends.
    /// Implementations must be safe to call from the Unity main thread.
    /// </summary>
    public interface ITTSProvider : IDisposable
    {
        /// <summary>
        /// Synthesizes text to PCM audio.
        /// </summary>
        /// <param name="text">Text to synthesize.</param>
        /// <param name="voiceName">Voice identifier (provider-specific interpretation).</param>
        /// <param name="languageCode">BCP-47 language code (e.g., "en-US").</param>
        /// <param name="onAudioChunk">Optional callback for streaming audio chunks.
        /// REST-based providers may ignore this parameter. When null, the full result
        /// is returned at completion. When provided, each chunk is delivered via the
        /// callback and the returned TTSResult contains the final/remaining chunk.</param>
        /// <returns>Complete audio result, or the final chunk when streaming via onAudioChunk.</returns>
        Awaitable<TTSResult> SynthesizeAsync(
            string text,
            string voiceName,
            string languageCode,
            Action<TTSResult> onAudioChunk = null);
    }

    /// <summary>
    /// Self-describing PCM audio result from a TTS provider.
    /// </summary>
    public readonly struct TTSResult
    {
        /// <summary>PCM audio samples in float[-1..1] range.</summary>
        public readonly float[] Samples;

        /// <summary>Sample rate in Hz (e.g., 24000 for Chirp 3 HD).</summary>
        public readonly int SampleRate;

        /// <summary>Number of audio channels (typically 1 for mono TTS).</summary>
        public readonly int Channels;

        public TTSResult(float[] samples, int sampleRate, int channels)
        {
            Samples = samples;
            SampleRate = sampleRate;
            Channels = channels;
        }

        /// <summary>True if this result contains valid audio data.</summary>
        public bool HasAudio => Samples != null && Samples.Length > 0;
    }

    /// <summary>
    /// Controls how TTS providers synthesize audio from AI text output.
    /// Provider-agnostic replacement for ChirpSynthesisMode.
    /// </summary>
    public enum TTSSynthesisMode
    {
        /// <summary>Synthesize each sentence as PacketAssembler emits it. Lower latency to first audio.</summary>
        SentenceBySentence,

        /// <summary>Wait for complete AI response, synthesize once. Higher quality, higher latency.</summary>
        FullResponse
    }
}
