using System;

namespace AIEmbodiment
{
    /// <summary>
    /// Orchestrator that wires SyncPacket audio to an <see cref="IAnimationModel"/> and
    /// exposes produced <see cref="BlendshapeFrame"/> events for downstream consumers.
    ///
    /// <para>
    /// Audio2Animation is a plain C# class (not MonoBehaviour), following the
    /// <see cref="PacketAssembler"/> pattern. It sits downstream of the sync pipeline --
    /// it receives <see cref="SyncPacket"/> instances (which contain audio) and delegates
    /// to the pluggable <see cref="IAnimationModel"/> for frame production.
    /// </para>
    ///
    /// <para>
    /// This class does NOT implement <see cref="ISyncDriver"/>. It is a downstream consumer,
    /// not a sync gate. PacketAssembler supports only one ISyncDriver; registering a second
    /// would overwrite the existing one (e.g., ChirpTTS).
    /// </para>
    /// </summary>
    public class Audio2Animation
    {
        private readonly IAnimationModel _model;

        /// <summary>
        /// Fires for each animation frame produced by the model.
        /// Subscribe to receive <see cref="BlendshapeFrame"/> instances for real-time application
        /// via <c>SkinnedMeshRenderer.SetBlendShapeWeight</c>.
        /// </summary>
        public event Action<BlendshapeFrame> OnFrameReady;

        /// <summary>
        /// Creates a new Audio2Animation orchestrator with the given animation model.
        /// Wires the model's frame output to the <see cref="OnFrameReady"/> event.
        /// </summary>
        /// <param name="model">The animation model backend (fake or real ML).</param>
        public Audio2Animation(IAnimationModel model)
        {
            _model = model;
            _model.SetFrameCallback(frame => OnFrameReady?.Invoke(frame));
        }

        /// <summary>
        /// Feed a <see cref="SyncPacket"/>'s audio into the animation model.
        /// Skips non-TextAudio packets and packets with no audio data.
        /// Call this from an <c>OnSyncPacket</c> handler.
        /// </summary>
        /// <param name="packet">The sync packet to process.</param>
        public void ProcessPacket(SyncPacket packet)
        {
            if (packet.Type != SyncPacketType.TextAudio) return;
            if (packet.Audio == null || packet.Audio.Length == 0) return;

            _model.ProcessAudio(packet.Audio, 24000);
        }

        /// <summary>
        /// Cancel in-progress animation (on barge-in/interruption).
        /// Delegates to the model's <see cref="IAnimationModel.Cancel"/>.
        /// </summary>
        public void Cancel()
        {
            _model.Cancel();
        }
    }
}
