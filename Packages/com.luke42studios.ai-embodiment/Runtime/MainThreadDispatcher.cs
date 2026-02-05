using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace AIEmbodiment
{
    /// <summary>
    /// Singleton MonoBehaviour that safely executes actions enqueued from background
    /// threads on the Unity main thread. Auto-initializes before any scene loads and
    /// survives scene transitions.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static MainThreadDispatcher _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance == null)
            {
                var go = new GameObject("[MainThreadDispatcher]");
                go.hideFlags = HideFlags.HideAndDontSave;
                _instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }

        /// <summary>
        /// Enqueues an action to be executed on the main thread during the next Update.
        /// Thread-safe -- can be called from any thread.
        /// </summary>
        /// <param name="action">The action to execute on the main thread.</param>
        /// <exception cref="ArgumentNullException">Thrown if action is null.</exception>
        public static void Enqueue(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _queue.Enqueue(action);
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
