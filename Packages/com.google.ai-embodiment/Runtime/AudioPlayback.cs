using System;
using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Streaming audio playback MonoBehaviour that plays Gemini AI voice through
    /// a developer-assigned AudioSource. Uses <see cref="AudioRingBuffer"/> for
    /// thread-safe bridging between main-thread audio data arrival and audio-thread
    /// playback via <see cref="OnAudioFilterRead"/>.
    ///
    /// Resamples from Gemini's 24kHz output to the system sample rate using linear
    /// interpolation. Implements write-ahead watermark buffering to prevent pops,
    /// clicks, and silence gaps during streaming playback.
    /// </summary>
    public class AudioPlayback : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;

        private AudioRingBuffer _ringBuffer;

        private const int GEMINI_SAMPLE_RATE = 24000;
        private const int BUFFER_SECONDS = 30;
        private const float WATERMARK_SECONDS = 0.3f;

        private int _watermarkSamples;
        private double _resampleRatio;
        private bool _isBuffering;
        private double _resamplePosition;
        private float[] _resampleBuffer;
        private bool _initialized;

        /// <summary>
        /// Returns true if the ring buffer has audio data remaining to play.
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                if (_ringBuffer == null) return false;
                return _ringBuffer.Available > 0;
            }
        }

        /// <summary>
        /// Initializes the playback pipeline. Call once when PersonaSession wires up audio.
        /// Queries system sample rate, allocates the ring buffer and resample buffer,
        /// creates a silent dummy AudioClip so OnAudioFilterRead fires, and starts playback.
        /// </summary>
        public void Initialize()
        {
            if (_audioSource == null)
            {
                Debug.LogError("AudioPlayback: No AudioSource assigned. Assign one in the Inspector.");
                return;
            }

            int systemRate = AudioSettings.outputSampleRate;
            _resampleRatio = (double)GEMINI_SAMPLE_RATE / systemRate;

            // Ring buffer holds 2 seconds of 24kHz mono audio
            _ringBuffer = new AudioRingBuffer(GEMINI_SAMPLE_RATE * BUFFER_SECONDS);

            // Watermark: don't start playback until this many samples are buffered
            _watermarkSamples = (int)(GEMINI_SAMPLE_RATE * WATERMARK_SECONDS);

            // Pre-allocate resample buffer for the maximum source samples needed per callback.
            // Worst case: system rate callback with max buffer size.
            // OnAudioFilterRead buffer is typically 1024 samples per channel.
            // Source samples needed = outputSamples * resampleRatio + 2 (interpolation lookahead).
            // Conservative pre-allocation: 4096 source samples covers all common configurations.
            _resampleBuffer = new float[4096];

            _isBuffering = true;
            _resamplePosition = 0.0;

            // Create a 1-second silent dummy AudioClip so AudioSource is "playing" and
            // OnAudioFilterRead fires continuously (Research Pitfall 8).
            AudioClip dummyClip = AudioClip.Create("AIVoice", systemRate, 1, systemRate, false);
            float[] silence = new float[systemRate];
            dummyClip.SetData(silence, 0);
            _audioSource.clip = dummyClip;
            _audioSource.loop = true;
            _audioSource.Play();

            _initialized = true;
        }

        /// <summary>
        /// Enqueues incoming audio samples from Gemini for playback.
        /// Called from main thread when audio data arrives via ProcessResponse.
        /// </summary>
        /// <param name="samples">Float array of 24kHz mono audio samples from Gemini.</param>
        public void EnqueueAudio(float[] samples)
        {
            if (_ringBuffer == null) return;
            _ringBuffer.Write(samples, 0, samples.Length);
        }

        /// <summary>
        /// Clears all buffered audio and resets resampling state.
        /// Called from main thread on interruption to stop stale audio immediately.
        /// </summary>
        public void ClearBuffer()
        {
            if (_ringBuffer == null) return;
            _ringBuffer.Clear();
            _isBuffering = true;
            _resamplePosition = 0.0;
        }

        /// <summary>
        /// Stops audio playback and clears the ring buffer.
        /// Called on session disconnect for clean teardown.
        /// </summary>
        public void Stop()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }

            if (_ringBuffer != null)
            {
                _ringBuffer.Clear();
            }

            _isBuffering = true;
            _resamplePosition = 0.0;
        }

        /// <summary>
        /// Audio thread callback. Reads from ring buffer and resamples from 24kHz
        /// to system sample rate via linear interpolation.
        ///
        /// CRITICAL: This runs on the AUDIO THREAD with a real-time deadline.
        /// ZERO allocations (no 'new', no LINQ, no string ops).
        /// ZERO Unity API calls (only array access and arithmetic).
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            // Not initialized yet -- output silence
            if (_ringBuffer == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            int outputSamples = data.Length / channels;

            // Buffering: wait until watermark is met before playing
            if (_isBuffering)
            {
                if (_ringBuffer.Available < _watermarkSamples)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }
                // Watermark met -- resume playback
                _isBuffering = false;
            }

            // Calculate how many 24kHz source samples we need for this output buffer
            int sourceSamplesNeeded = (int)Math.Ceiling(outputSamples * _resampleRatio) + 2;

            // Ensure resample buffer is large enough (should always be, but clamp for safety)
            if (sourceSamplesNeeded > _resampleBuffer.Length)
            {
                sourceSamplesNeeded = _resampleBuffer.Length;
            }

            // Read source samples from ring buffer into pre-allocated resample buffer
            int actualRead = _ringBuffer.Read(_resampleBuffer, 0, sourceSamplesNeeded);

            // Underrun: no data available -- output silence but DON'T re-enter buffering.
            // Re-buffering would add a 150ms watermark delay every time the network
            // briefly falls behind, causing audible gaps between words.
            // Instead, output silence and resume immediately when data arrives.
            if (actualRead == 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            // Linear interpolation resampling from 24kHz to system sample rate
            for (int i = 0; i < outputSamples; i++)
            {
                double srcPos = _resamplePosition + i * _resampleRatio;
                int idx0 = (int)srcPos;
                int idx1 = idx0 + 1;

                // Clamp indices to available data
                if (idx0 >= actualRead) idx0 = actualRead - 1;
                if (idx1 >= actualRead) idx1 = actualRead - 1;
                if (idx0 < 0) idx0 = 0;
                if (idx1 < 0) idx1 = 0;

                float frac = (float)(srcPos - (int)srcPos);
                float sample = _resampleBuffer[idx0] * (1f - frac) + _resampleBuffer[idx1] * frac;

                // Write mono sample to all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] = sample;
                }
            }

            // Update resample position for continuity across callbacks.
            // Advance by the total source samples logically consumed, then subtract
            // the integer part (which was the data we already read from the ring buffer).
            double totalConsumed = _resamplePosition + outputSamples * _resampleRatio;
            _resamplePosition = totalConsumed - Math.Floor(totalConsumed);
        }
    }
}
