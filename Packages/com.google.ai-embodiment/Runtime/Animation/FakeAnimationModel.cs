using System;

namespace AIEmbodiment
{
    /// <summary>
    /// Fake animation model that streams pre-recorded <see cref="BlendshapeFrame"/> data
    /// synchronized to audio timing using a time-accumulator pattern.
    ///
    /// <para>
    /// When <see cref="ProcessAudio"/> receives audio, it calculates the audio duration
    /// and adds it to an animation budget. <see cref="Tick"/> drains the budget by emitting
    /// frames at the pre-recorded data's native FPS. This naturally paces frame emission
    /// to match audio arrival rate.
    /// </para>
    ///
    /// <para>
    /// This is a plain C# class (not MonoBehaviour), following the <see cref="PacketAssembler"/>
    /// pattern. The owning MonoBehaviour (e.g., FaceAnimationPlayer) must call
    /// <see cref="Tick"/> every frame with <c>Time.deltaTime</c>.
    /// </para>
    ///
    /// <para>
    /// When pre-recorded frames run out, the frame index wraps via modulo to loop the data.
    /// </para>
    /// </summary>
    public class FakeAnimationModel : IAnimationModel
    {
        private readonly BlendshapeAnimationData _data;
        private readonly float _frameDuration;

        private Action<BlendshapeFrame> _onFrameReady;
        private int _currentFrameIndex;
        private float _animationBudgetSeconds;
        private float _timeAccumulator;

        /// <summary>
        /// Creates a new FakeAnimationModel with the given pre-recorded animation data.
        /// </summary>
        /// <param name="data">Pre-recorded animation data loaded from JSON.</param>
        public FakeAnimationModel(BlendshapeAnimationData data)
        {
            _data = data;

            // Calculate frame duration from data. timeSeconds is per-frame duration
            // (e.g., 0.0333s for 30fps, 0.04s for 25fps).
            if (_data.frames != null && _data.frames.Count > 0 && _data.frames[0].timeSeconds > 0f)
            {
                _frameDuration = _data.frames[0].timeSeconds;
            }
            else
            {
                _frameDuration = 1f / 30f; // Default to 30fps
            }

            _currentFrameIndex = 0;
            _animationBudgetSeconds = 0f;
            _timeAccumulator = 0f;
        }

        /// <inheritdoc/>
        public void SetFrameCallback(Action<BlendshapeFrame> callback)
        {
            _onFrameReady = callback;
        }

        /// <summary>
        /// Accumulates audio duration into the animation budget.
        /// Frames are emitted in <see cref="Tick"/>, not here.
        /// </summary>
        /// <param name="audioSamples">PCM audio samples (float[-1..1]).</param>
        /// <param name="sampleRate">Sample rate in Hz (typically 24000).</param>
        public void ProcessAudio(float[] audioSamples, int sampleRate)
        {
            float audioDuration = (float)audioSamples.Length / sampleRate;
            _animationBudgetSeconds += audioDuration;
        }

        /// <summary>
        /// Stops frame emission and clears budget. Does NOT reset the frame index --
        /// the next audio chunk continues from where playback left off.
        /// </summary>
        public void Cancel()
        {
            _animationBudgetSeconds = 0f;
            _timeAccumulator = 0f;
        }

        /// <summary>
        /// Drains the animation budget by emitting frames at the pre-recorded data's native FPS.
        /// Must be called every frame by the owning MonoBehaviour with <c>Time.deltaTime</c>.
        ///
        /// <para>
        /// When the frame index exceeds the pre-recorded data length, it wraps via modulo
        /// to loop the animation data continuously.
        /// </para>
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame (typically Time.deltaTime).</param>
        public void Tick(float deltaTime)
        {
            if (_animationBudgetSeconds <= 0f || _data.frames == null || _data.frames.Count == 0)
                return;

            _timeAccumulator += deltaTime;

            while (_timeAccumulator >= _frameDuration && _animationBudgetSeconds > 0f)
            {
                // Emit frame with modulo wrapping for looping (Pitfall 5)
                _onFrameReady?.Invoke(_data.frames[_currentFrameIndex % _data.frames.Count]);

                _currentFrameIndex++;
                _timeAccumulator -= _frameDuration;
                _animationBudgetSeconds -= _frameDuration;
            }

            // Clamp budget to 0 and reset frame index so next audio starts fresh
            if (_animationBudgetSeconds <= 0f)
            {
                _animationBudgetSeconds = 0f;
                _currentFrameIndex = 0;
            }
        }
    }
}
