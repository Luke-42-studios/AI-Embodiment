using System;

namespace AIEmbodiment
{
    /// <summary>
    /// Controls when assembled <see cref="SyncPacket"/> instances are released to the
    /// developer. The highest-latency driver wins: packets are held until the slowest
    /// registered driver signals readiness.
    ///
    /// <para>
    /// Use cases: Chirp TTS (Phase 5) holds packets until TTS audio arrives. Future
    /// AI Face Animation holds packets until blend shapes are computed. When no driver
    /// is registered, packets release immediately (Gemini native audio path).
    /// </para>
    /// </summary>
    public interface ISyncDriver
    {
        /// <summary>
        /// Called when a <see cref="SyncPacket"/> is assembled and ready for release.
        /// The driver may hold it and release later (e.g., waiting for TTS audio
        /// or face animation data).
        /// </summary>
        /// <param name="packet">The assembled packet ready for release.</param>
        void OnPacketReady(SyncPacket packet);

        /// <summary>
        /// Register a callback that the driver calls when it is ready to release a packet.
        /// <see cref="PacketAssembler"/> calls this during driver registration so the driver
        /// can route packets back when ready.
        /// </summary>
        /// <param name="releaseCallback">Callback to invoke when releasing a held packet.</param>
        void SetReleaseCallback(Action<SyncPacket> releaseCallback);

        /// <summary>
        /// The estimated pipeline latency of this driver in milliseconds.
        /// Used to determine which driver is the highest-latency (and thus the pacer)
        /// when multiple drivers are registered.
        /// </summary>
        int EstimatedLatencyMs { get; }
    }
}
