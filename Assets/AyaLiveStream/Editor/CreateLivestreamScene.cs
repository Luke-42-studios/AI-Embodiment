using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AIEmbodiment.Samples;

namespace AIEmbodiment.Samples.Editor
{
    /// <summary>
    /// One-click editor menu script that programmatically sets up a LivestreamSampleScene
    /// from AyaSampleScene. Opens the template scene, swaps AyaSampleController for the
    /// livestream subsystem components (LivestreamController, NarrativeDirector, ChatBotManager,
    /// PushToTalkController, SceneTransitionHandler), wires all SerializeField references via
    /// SerializedObject/SerializedProperty, and saves as a new scene.
    ///
    /// Run via: AI Embodiment > Samples > Create Livestream Scene
    /// </summary>
    public static class CreateLivestreamScene
    {
        private const string TemplateScenePath = "Assets/Scenes/AyaSampleScene.unity";
        private const string OutputScenePath = "Assets/Scenes/LivestreamSampleScene.unity";
        private const string BeatDataFolder = "Assets/AyaLiveStream/Data";
        private const string BotConfigFolder = "Assets/AyaLiveStream/ChatBotConfigs";
        private const string AnimationConfigFolder = "Assets/AyaLiveStream/Data";

        [MenuItem("AI Embodiment/Samples/Create Livestream Scene")]
        public static void Create()
        {
            // --- Step 1: Open template scene ---

            var templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Single);
            if (!templateScene.IsValid())
            {
                Debug.LogError($"[CreateLivestreamScene] Failed to open template scene: {TemplateScenePath}");
                return;
            }

            // --- Step 2: Find existing scene GameObjects by component type ---

            var ayaController = Object.FindFirstObjectByType<AyaSampleController>();
            if (ayaController == null)
            {
                Debug.LogError("[CreateLivestreamScene] AyaSampleController not found in scene.");
                return;
            }

            var personaSession = Object.FindFirstObjectByType<PersonaSession>();
            if (personaSession == null)
            {
                Debug.LogWarning("[CreateLivestreamScene] PersonaSession not found in scene. References will be null.");
            }

            var livestreamUI = Object.FindFirstObjectByType<LivestreamUI>();
            if (livestreamUI == null)
            {
                Debug.LogWarning("[CreateLivestreamScene] LivestreamUI not found in scene. References will be null.");
            }

            // --- Step 3: Swap components on the controller GameObject ---

            GameObject controllerGO = ayaController.gameObject;

            // Remove AyaSampleController
            Object.DestroyImmediate(ayaController);

            // Add new components
            var livestreamController = controllerGO.AddComponent<LivestreamController>();
            var narrativeDirector = controllerGO.AddComponent<NarrativeDirector>();
            var chatBotManager = controllerGO.AddComponent<ChatBotManager>();
            var pttController = controllerGO.AddComponent<PushToTalkController>();
            var sceneTransitionHandler = controllerGO.AddComponent<SceneTransitionHandler>();

            // --- Step 4: Load ScriptableObject assets ---

            // Load AnimationConfig
            AnimationConfig animConfig = FindAssetOfType<AnimationConfig>(AnimationConfigFolder);
            if (animConfig == null)
            {
                // Also search the root AyaLiveStream folder
                animConfig = FindAssetOfType<AnimationConfig>("Assets/AyaLiveStream");
            }
            if (animConfig == null)
            {
                Debug.LogWarning("[CreateLivestreamScene] AnimationConfig asset not found. Searching entire project...");
                animConfig = FindAssetOfTypeGlobal<AnimationConfig>();
            }
            if (animConfig == null)
            {
                Debug.LogWarning("[CreateLivestreamScene] No AnimationConfig asset found anywhere in project.");
            }

            // Load NarrativeBeatConfig assets
            NarrativeBeatConfig[] beats = FindAllAssetsOfType<NarrativeBeatConfig>(BeatDataFolder);
            if (beats.Length == 0)
            {
                Debug.LogWarning($"[CreateLivestreamScene] No NarrativeBeatConfig assets found in {BeatDataFolder}. " +
                    "Run 'AI Embodiment > Samples > Create Demo Beat Assets' first.");
            }

            // Load ChatBotConfig assets
            ChatBotConfig[] bots = FindAllAssetsOfType<ChatBotConfig>(BotConfigFolder);
            if (bots.Length == 0)
            {
                Debug.LogWarning($"[CreateLivestreamScene] No ChatBotConfig assets found in {BotConfigFolder}. " +
                    "Run 'AI Embodiment > Samples > Migrate Chat Bot Configs' first.");
            }

            // --- Step 5: Wire SerializeField references via SerializedObject/SerializedProperty ---

            // LivestreamController fields:
            //   _session, _narrativeDirector, _chatBotManager, _pttController,
            //   _sceneTransitionHandler, _livestreamUI, _animationConfig
            {
                var so = new SerializedObject(livestreamController);
                SetObjectRef(so, "_session", personaSession);
                SetObjectRef(so, "_narrativeDirector", narrativeDirector);
                SetObjectRef(so, "_chatBotManager", chatBotManager);
                SetObjectRef(so, "_pttController", pttController);
                SetObjectRef(so, "_sceneTransitionHandler", sceneTransitionHandler);
                SetObjectRef(so, "_livestreamUI", livestreamUI);
                SetObjectRef(so, "_animationConfig", animConfig);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // NarrativeDirector fields:
            //   _session, _beats, _livestreamUI, _chatBotManager
            {
                var so = new SerializedObject(narrativeDirector);
                SetObjectRef(so, "_session", personaSession);
                SetObjectRef(so, "_livestreamUI", livestreamUI);
                SetObjectRef(so, "_chatBotManager", chatBotManager);
                SetObjectArray(so, "_beats", beats);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ChatBotManager fields:
            //   _livestreamUI, _session, _bots, _narrativeDirector
            {
                var so = new SerializedObject(chatBotManager);
                SetObjectRef(so, "_livestreamUI", livestreamUI);
                SetObjectRef(so, "_session", personaSession);
                SetObjectRef(so, "_narrativeDirector", narrativeDirector);
                SetObjectArray(so, "_bots", bots);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // PushToTalkController fields:
            //   _session, _narrativeDirector, _livestreamUI, _chatBotManager
            {
                var so = new SerializedObject(pttController);
                SetObjectRef(so, "_session", personaSession);
                SetObjectRef(so, "_narrativeDirector", narrativeDirector);
                SetObjectRef(so, "_livestreamUI", livestreamUI);
                SetObjectRef(so, "_chatBotManager", chatBotManager);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // SceneTransitionHandler fields:
            //   _narrativeDirector, _session
            {
                var so = new SerializedObject(sceneTransitionHandler);
                SetObjectRef(so, "_narrativeDirector", narrativeDirector);
                SetObjectRef(so, "_session", personaSession);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- Step 6: Save the scene ---

            EditorSceneManager.SaveScene(templateScene, OutputScenePath);

            // --- Step 7: Add to Build Settings (if not already present) ---

            AddSceneToBuildSettings(OutputScenePath);

            // Mark scene dirty so Unity knows it has been modified
            EditorSceneManager.MarkSceneDirty(templateScene);

            Debug.Log($"[CreateLivestreamScene] LivestreamSampleScene created at {OutputScenePath}");
            Debug.Log($"[CreateLivestreamScene] Wired: {(personaSession != null ? "PersonaSession" : "MISSING PersonaSession")}, " +
                $"{(livestreamUI != null ? "LivestreamUI" : "MISSING LivestreamUI")}, " +
                $"{beats.Length} beat(s), {bots.Length} bot(s), " +
                $"{(animConfig != null ? "AnimationConfig" : "MISSING AnimationConfig")}");
        }

        // -----------------------------------------------------------------
        // Helper: Set a single object reference on a SerializedProperty
        // -----------------------------------------------------------------

        private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[CreateLivestreamScene] Property '{propertyName}' not found on {so.targetObject.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        // -----------------------------------------------------------------
        // Helper: Set an array of object references on a SerializedProperty
        // -----------------------------------------------------------------

        private static void SetObjectArray<T>(SerializedObject so, string propertyName, T[] values) where T : Object
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[CreateLivestreamScene] Array property '{propertyName}' not found on {so.targetObject.GetType().Name}.");
                return;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // -----------------------------------------------------------------
        // Helper: Find a single asset of type T in a specific folder
        // -----------------------------------------------------------------

        private static T FindAssetOfType<T>(string folder) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            if (guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // -----------------------------------------------------------------
        // Helper: Find a single asset of type T anywhere in the project
        // -----------------------------------------------------------------

        private static T FindAssetOfTypeGlobal<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // -----------------------------------------------------------------
        // Helper: Find all assets of type T in a specific folder
        // -----------------------------------------------------------------

        private static T[] FindAllAssetsOfType<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return new T[0];
            }

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            T[] assets = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return assets;
        }

        // -----------------------------------------------------------------
        // Helper: Add scene to Build Settings if not already present
        // -----------------------------------------------------------------

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;

            // Check if already in build settings
            foreach (var scene in scenes)
            {
                if (scene.path == scenePath)
                {
                    Debug.Log($"[CreateLivestreamScene] Scene already in Build Settings: {scenePath}");
                    return;
                }
            }

            // Add to build settings
            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++)
            {
                newScenes[i] = scenes[i];
            }
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;

            Debug.Log($"[CreateLivestreamScene] Added scene to Build Settings: {scenePath}");
        }
    }
}
