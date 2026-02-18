using System;

namespace AIEmbodiment
{
    /// <summary>
    /// Lock-free single-producer single-consumer circular buffer for float audio samples.
    /// Main thread writes incoming audio from Gemini, audio thread reads for playback.
    /// Uses volatile int positions -- no locks, no allocations in Read/Write.
    /// </summary>
    public class AudioRingBuffer
    {
        private readonly float[] _buffer;
        private readonly int _capacity;
        private volatile int _writePos;
        private volatile int _readPos;

        /// <summary>
        /// Creates a ring buffer with the specified capacity in samples.
        /// </summary>
        /// <param name="capacity">Maximum number of float samples the buffer can hold.</param>
        public AudioRingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            _capacity = capacity;
            _buffer = new float[capacity];
        }

        /// <summary>
        /// Number of samples currently available to read.
        /// Safe to call from any thread.
        /// </summary>
        public int Available => (_writePos - _readPos + _capacity) % _capacity;

        /// <summary>
        /// Writes samples into the ring buffer. Called from main thread only.
        /// No allocations. Wraps around using modulo arithmetic.
        /// Clamps to available free space to prevent overwriting unread data.
        /// </summary>
        /// <param name="data">Source array containing audio samples.</param>
        /// <param name="offset">Start index in the source array.</param>
        /// <param name="count">Number of samples to write.</param>
        public void Write(float[] data, int offset, int count)
        {
            int freeSpace = _capacity - 1 - Available;
            if (count > freeSpace)
                count = freeSpace;

            for (int i = 0; i < count; i++)
            {
                _buffer[(_writePos + i) % _capacity] = data[offset + i];
            }
            _writePos = (_writePos + count) % _capacity;
        }

        /// <summary>
        /// Reads up to <paramref name="count"/> samples from the ring buffer.
        /// Called from audio thread only -- ZERO allocations.
        /// Zero-fills any remaining samples on underrun (outputs silence, not stale data).
        /// </summary>
        /// <param name="data">Destination array to fill with audio samples.</param>
        /// <param name="offset">Start index in the destination array.</param>
        /// <param name="count">Maximum number of samples to read.</param>
        /// <returns>Number of actual samples read (remainder is zero-filled).</returns>
        public int Read(float[] data, int offset, int count)
        {
            int available = Available;
            int toRead = Math.Min(count, available);

            for (int i = 0; i < toRead; i++)
            {
                data[offset + i] = _buffer[(_readPos + i) % _capacity];
            }
            _readPos = (_readPos + toRead) % _capacity;

            // Zero-fill remainder on underrun
            for (int i = toRead; i < count; i++)
            {
                data[offset + i] = 0f;
            }

            return toRead;
        }

        /// <summary>
        /// Flushes all buffered audio by resetting read position to write position.
        /// Used on interruption to discard stale audio immediately.
        /// </summary>
        public void Clear()
        {
            _readPos = _writePos;
        }
    }
}
