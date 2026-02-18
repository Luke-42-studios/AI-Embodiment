using System;

namespace AIEmbodiment
{
    /// <summary>
    /// Pluggable backend for converting audio to facial animation frames.
    /// Implementations range from fake (pre-recorded playback) to real ML inference.
    ///
    /// <para>
    /// <see cref="ProcessAudio"/> may produce frames asynchronously over time, not all at once.
    /// Implementations that require time-based pacing (e.g., <see cref="FakeAnimationModel"/>)
    /// expose a <c>Tick(float deltaTime)</c> method driven by an external caller. Tick is NOT
    /// part of this interface because it is an implementation detail -- a real ML model might
    /// emit frames via the callback directly from ProcessAudio.
    /// </para>
    /// </summary>
    public interface IAnimationModel
    {
        /// <summary>
        /// Process an audio chunk and produce animation frames over time.
        /// Frames are emitted via the callback registered through <see cref="SetFrameCallback"/>.
        ///
        /// <para>
        /// IMPORTANT: This may produce frames asynchronously over time (not all at once).
        /// The fake model accumulates an audio-duration budget and streams frames via Tick().
        /// A real model would emit frames as inference completes.
        /// </para>
        /// </summary>
        /// <param name="audioSamples">PCM audio samples (float[-1..1]).</param>
        /// <param name="sampleRate">Sample rate in Hz (typically 24000 for Gemini Live).</param>
        void ProcessAudio(float[] audioSamples, int sampleRate);

        /// <summary>
        /// Stop any in-progress frame streaming (e.g., on interruption/barge-in).
        /// Clears any accumulated budget or queued frames.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Registers the callback that receives produced <see cref="BlendshapeFrame"/> instances.
        /// Called by <see cref="Audio2Animation"/> during construction to wire model output
        /// to the <see cref="Audio2Animation.OnFrameReady"/> event.
        /// </summary>
        /// <param name="callback">Callback invoked for each animation frame produced.</param>
        void SetFrameCallback(Action<BlendshapeFrame> callback);
    }
}
