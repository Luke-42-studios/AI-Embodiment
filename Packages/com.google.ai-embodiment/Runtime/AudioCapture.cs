using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace AIEmbodiment
{
    /// <summary>
    /// Captures audio from the system default microphone at 16kHz mono and delivers
    /// chunked PCM float[] data via <see cref="OnAudioCaptured"/>. Handles cross-platform
    /// microphone permission requests (Android runtime permissions and Desktop/Editor authorization).
    ///
    /// Usage: Attach to a GameObject, subscribe to <see cref="OnAudioCaptured"/>,
    /// then call <see cref="StartCapture"/>. PersonaSession wires the callback to
    /// GeminiLiveClient.SendAudio via HandleAudioCaptured.
    /// </summary>
    public class AudioCapture : MonoBehaviour
    {
        /// <summary>
        /// Fires with each 100ms chunk of 16kHz mono PCM audio data.
        /// PersonaSession subscribes and forwards to SendAudioAsync.
        /// </summary>
        public event Action<float[]> OnAudioCaptured;

        /// <summary>
        /// Fires if the user denies microphone permission.
        /// </summary>
        public event Action OnPermissionDenied;

        private AudioClip _micClip;
        private int _lastSamplePos;
        private bool _isCapturing;
        private Coroutine _captureCoroutine;

        private const int MIC_FREQUENCY = 16000;
        private const int MIC_CLIP_LENGTH_SEC = 1;
        private const int CHUNK_SAMPLES = 1600; // 100ms at 16kHz

        /// <summary>
        /// Requests microphone permission (platform-specific) and starts capturing audio.
        /// If already capturing, this is a no-op.
        /// </summary>
        public void StartCapture()
        {
            if (_isCapturing) return;
            _captureCoroutine = StartCoroutine(RequestPermissionAndCapture());
        }

        /// <summary>
        /// Stops audio capture, releases the microphone, and resets internal state.
        /// If not capturing, this is a no-op.
        /// </summary>
        public void StopCapture()
        {
            if (!_isCapturing) return;

            _isCapturing = false;

            if (_captureCoroutine != null)
            {
                StopCoroutine(_captureCoroutine);
                _captureCoroutine = null;
            }

            Microphone.End(null);
            _lastSamplePos = 0;
        }

        private IEnumerator RequestPermissionAndCapture()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                bool responded = false;
                bool granted = false;

                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { responded = true; granted = true; };
                callbacks.PermissionDenied += _ => { responded = true; granted = false; };
                callbacks.PermissionDeniedAndDontAskAgain += _ => { responded = true; granted = false; };

                Permission.RequestUserPermission(Permission.Microphone, callbacks);

                while (!responded) yield return null;

                if (!granted)
                {
                    OnPermissionDenied?.Invoke();
                    yield break;
                }
            }
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                OnPermissionDenied?.Invoke();
                yield break;
            }
#endif

            _micClip = Microphone.Start(null, true, MIC_CLIP_LENGTH_SEC, MIC_FREQUENCY);

            if (_micClip == null)
            {
                Debug.LogError("AudioCapture: Microphone.Start returned null -- no microphone available or permission denied.");
                yield break;
            }

            // Wait until the microphone is actually recording
            while (!Microphone.IsRecording(null)) yield return null;

            _isCapturing = true;
            _lastSamplePos = 0;
            _captureCoroutine = StartCoroutine(CaptureLoop());
        }

        private IEnumerator CaptureLoop()
        {
            float[] buffer = new float[CHUNK_SAMPLES]; // pre-allocate once

            while (_isCapturing)
            {
                yield return null; // poll every frame

                int currentPos = Microphone.GetPosition(null);
                if (currentPos == _lastSamplePos) continue;

                // Calculate samples available, handling wrap-around (Research Pitfall 2)
                int samplesToRead;
                if (currentPos > _lastSamplePos)
                {
                    samplesToRead = currentPos - _lastSamplePos;
                }
                else
                {
                    // Buffer has wrapped around
                    samplesToRead = (_micClip.samples - _lastSamplePos) + currentPos;
                }

                // Accumulate at least one chunk (100ms = 1600 samples) to avoid flooding (Pitfall 6)
                if (samplesToRead < CHUNK_SAMPLES) continue;

                // Read and send in chunk-sized pieces
                while (samplesToRead >= CHUNK_SAMPLES)
                {
                    _micClip.GetData(buffer, _lastSamplePos);
                    _lastSamplePos = (_lastSamplePos + CHUNK_SAMPLES) % _micClip.samples;
                    samplesToRead -= CHUNK_SAMPLES;

                    // Copy to new array for callback safety (buffer is reused each iteration)
                    float[] chunk = new float[CHUNK_SAMPLES];
                    Array.Copy(buffer, chunk, CHUNK_SAMPLES);
                    OnAudioCaptured?.Invoke(chunk);
                }
            }
        }

        private void OnDestroy()
        {
            if (_isCapturing)
            {
                StopCapture();
            }
        }
    }
}
