using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AIEmbodiment;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Main controller for the Aya Live Stream sample scene.
    /// Orchestrates function registration, pre-recorded intro playback,
    /// push-to-talk keyboard input, and conversational goal injection after warm-up.
    /// </summary>
    public class AyaSampleController : MonoBehaviour
    {
        [SerializeField] private PersonaSession _session;
        [SerializeField] private AyaChatUI _chatUI;
        [SerializeField] private AudioSource _introAudioSource;
        [SerializeField] private AudioClip _introClip;

        private bool _liveMode = false;
        private int _exchangeCount = 0;
        private bool _goalActivated = false;

        private const int WarmUpExchanges = 3;

        private void Start()
        {
            RegisterFunctions();

            _session.OnTurnComplete += HandleTurnComplete;
            _session.OnFunctionError += HandleFunctionError;

            StartCoroutine(PlayIntroThenGoLive());
        }

        private void RegisterFunctions()
        {
            // Register handlers only (declarations deferred to Phase 10)
            _session.RegisterFunction("emote", HandleEmote);
            _session.RegisterFunction("start_movie", HandleStartMovie);
            _session.RegisterFunction("start_drawing", HandleStartDrawing);
        }

        private IDictionary<string, object> HandleEmote(FunctionCallContext ctx)
        {
            string animName = ctx.GetString("animation_name", "idle");
            _chatUI.LogSystemMessage($"[Emote: {animName}]");
            return null; // fire-and-forget
        }

        private IDictionary<string, object> HandleStartMovie(FunctionCallContext ctx)
        {
            _chatUI.LogSystemMessage("[Movie mode activated]");
            return null; // fire-and-forget
        }

        private IDictionary<string, object> HandleStartDrawing(FunctionCallContext ctx)
        {
            _chatUI.LogSystemMessage("[Drawing mode activated]");
            return null; // fire-and-forget
        }

        private void HandleFunctionError(string functionName, Exception ex)
        {
            _chatUI.LogSystemMessage($"[Function error: {functionName} -- {ex.Message}]");
        }

        private IEnumerator PlayIntroThenGoLive()
        {
            _chatUI.SetStatus("Aya's intro playing...");
            _chatUI.LogSystemMessage("Welcome to Aya's Live Stream!");

            if (_introClip != null && _introAudioSource != null)
            {
                _introAudioSource.clip = _introClip;
                _introAudioSource.Play();
                yield return new WaitWhile(() => _introAudioSource.isPlaying);
            }
            else
            {
                // No intro clip -- brief pause then go live
                yield return new WaitForSeconds(1f);
            }

            _chatUI.LogSystemMessage("Going live...");
            _session.Connect();
            _liveMode = true;
        }

        private void Update()
        {
            if (!_liveMode) return;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                    _session.StartListening();
                if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                    _session.StopListening();
            }
        }

        private void HandleTurnComplete()
        {
            _exchangeCount++;
            if (_exchangeCount == WarmUpExchanges && !_goalActivated)
            {
                _goalActivated = true;
                _session.AddGoal(
                    "life_story",
                    "Steer the conversation toward talking about the life story behind your characters and what inspired you to start drawing them. Share personal anecdotes about your creative journey.",
                    GoalPriority.Medium
                );
                _chatUI.LogSystemMessage("[Goal activated: Steer toward character life stories]");
            }
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnTurnComplete -= HandleTurnComplete;
                _session.OnFunctionError -= HandleFunctionError;
            }
        }
    }
}
