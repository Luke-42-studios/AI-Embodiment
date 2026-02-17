using AIEmbodiment;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Handles the clean scene transition from the livestream to the movie clip
    /// when the NarrativeDirector signals all beats are complete.
    ///
    /// Transition is a clean exit: PersonaSession disconnects (WebSocket closes,
    /// audio stops, TTS disposes), then SceneManager loads the movie scene with
    /// LoadSceneMode.Single (destroying the current scene entirely).
    ///
    /// Wire in Inspector: assign NarrativeDirector, PersonaSession, and the movie
    /// scene name. The movie scene MUST be added to File > Build Settings > Scenes In Build.
    /// </summary>
    public class SceneTransitionHandler : MonoBehaviour
    {
        [SerializeField] private NarrativeDirector _narrativeDirector;
        [SerializeField] private PersonaSession _session;

        [Header("Movie Scene")]
        [Tooltip("Name of the movie scene to load. Must be in Build Settings.")]
        [SerializeField] private string _movieSceneName = "MovieScene";

        private bool _transitioning;

        private void OnEnable()
        {
            if (_narrativeDirector != null)
                _narrativeDirector.OnAllBeatsComplete += HandleAllBeatsComplete;
        }

        private void OnDisable()
        {
            if (_narrativeDirector != null)
                _narrativeDirector.OnAllBeatsComplete -= HandleAllBeatsComplete;
        }

        private void HandleAllBeatsComplete()
        {
            if (_transitioning) return;
            _transitioning = true;

            Debug.Log($"[SceneTransition] Narrative complete. Transitioning to '{_movieSceneName}'.");

            // Clean disconnect first -- WebSocket closes, audio stops, TTS disposes
            // (Pitfall 4: explicit Disconnect before scene load avoids destruction-order races)
            if (_session != null)
                _session.Disconnect();

            // Verify scene is in Build Settings before attempting load
            if (Application.CanStreamedLevelBeLoaded(_movieSceneName))
            {
                SceneManager.LoadSceneAsync(_movieSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError(
                    $"[SceneTransition] Scene '{_movieSceneName}' not found in Build Settings. " +
                    "Add it via File > Build Settings > Scenes In Build.");
                _transitioning = false; // Reset so it can be retried after fixing
            }
        }
    }
}
