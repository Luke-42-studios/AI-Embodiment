using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using AIEmbodiment;
using AIEmbodiment.Samples;

namespace AIEmbodiment.Samples.Editor
{
    /// <summary>
    /// One-click editor script that sets up a complete livestream scene from scratch.
    /// Creates all required GameObjects and components if missing:
    /// - PersonaSession with AudioCapture, AudioPlayback, AudioSource, and PersonaConfig
    /// - LivestreamUI with UIDocument wired to LivestreamPanel.uxml
    /// - LivestreamController with NarrativeDirector, ChatBotManager, PushToTalkController,
    ///   SceneTransitionHandler, and AnimationConfig
    ///
    /// Run via: AI Embodiment > Samples > Create Livestream Scene
    /// </summary>
    public static class CreateLivestreamScene
    {
        private const string BeatDataFolder = "Assets/AyaLiveStream/Data";
        private const string BotConfigFolder = "Assets/AyaLiveStream/ChatBotConfigs";
        private const string AnimConfigFolder = "Assets/AyaLiveStream/Data";
        private const string UxmlPath = "Assets/AyaLiveStream/UI/LivestreamPanel.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
        private const string PersonaConfigPath = "Assets/AyaLiveStream/AyaPersonaConfig.asset";

        [MenuItem("AI Embodiment/Samples/Create Livestream Scene")]
        public static void Create()
        {
            // --- Step 1: Work with the currently open scene ---

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("[CreateLivestreamScene] No active scene open.");
                return;
            }

            // Check if LivestreamController already exists (re-run safety)
            var existingController = Object.FindFirstObjectByType<LivestreamController>();
            if (existingController != null)
            {
                Debug.LogError("[CreateLivestreamScene] LivestreamController already exists in scene. " +
                    "Delete it first if you want to re-run setup.");
                return;
            }

            // --- Step 2: PersonaSession (find or create) ---

            var personaSession = Object.FindFirstObjectByType<PersonaSession>();
            if (personaSession == null)
            {
                var sessionGO = new GameObject("PersonaSession");

                // Add AudioSource first (AudioPlayback needs it)
                var audioSource = sessionGO.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D audio

                // Add audio components
                var audioCapture = sessionGO.AddComponent<AudioCapture>();
                var audioPlayback = sessionGO.AddComponent<AudioPlayback>();

                // Wire AudioPlayback._audioSource
                var playbackSO = new SerializedObject(audioPlayback);
                SetObjectRef(playbackSO, "_audioSource", audioSource);
                playbackSO.ApplyModifiedPropertiesWithoutUndo();

                // Add PersonaSession
                personaSession = sessionGO.AddComponent<PersonaSession>();

                // Wire PersonaSession fields
                var sessionSO = new SerializedObject(personaSession);
                SetObjectRef(sessionSO, "_audioCapture", audioCapture);
                SetObjectRef(sessionSO, "_audioPlayback", audioPlayback);

                // Wire PersonaConfig if the asset exists
                var personaConfig = AssetDatabase.LoadAssetAtPath<PersonaConfig>(PersonaConfigPath);
                if (personaConfig == null)
                {
                    personaConfig = FindAssetOfTypeGlobal<PersonaConfig>();
                }
                if (personaConfig != null)
                {
                    SetObjectRef(sessionSO, "_config", personaConfig);
                    Debug.Log($"[CreateLivestreamScene] Wired PersonaConfig: {personaConfig.name}");
                }
                else
                {
                    Debug.LogWarning("[CreateLivestreamScene] No PersonaConfig asset found. " +
                        "Create one via Assets > Create > AI Embodiment > Persona Config.");
                }

                sessionSO.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[CreateLivestreamScene] Created PersonaSession with AudioCapture + AudioPlayback.");
            }
            else
            {
                // PersonaSession exists -- ensure audio components are present
                var audioCapture = personaSession.GetComponent<AudioCapture>();
                var audioPlayback = personaSession.GetComponent<AudioPlayback>();

                if (audioCapture == null)
                {
                    audioCapture = personaSession.gameObject.AddComponent<AudioCapture>();
                    var sessionSO = new SerializedObject(personaSession);
                    SetObjectRef(sessionSO, "_audioCapture", audioCapture);
                    sessionSO.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[CreateLivestreamScene] Added missing AudioCapture to PersonaSession.");
                }

                if (audioPlayback == null)
                {
                    var audioSource = personaSession.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = personaSession.gameObject.AddComponent<AudioSource>();
                        audioSource.playOnAwake = false;
                        audioSource.spatialBlend = 0f;
                    }

                    audioPlayback = personaSession.gameObject.AddComponent<AudioPlayback>();
                    var playbackSO = new SerializedObject(audioPlayback);
                    SetObjectRef(playbackSO, "_audioSource", audioSource);
                    playbackSO.ApplyModifiedPropertiesWithoutUndo();

                    var sessionSO = new SerializedObject(personaSession);
                    SetObjectRef(sessionSO, "_audioPlayback", audioPlayback);
                    sessionSO.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[CreateLivestreamScene] Added missing AudioPlayback to PersonaSession.");
                }
            }

            // --- Step 3: LivestreamUI (find or create) ---

            var livestreamUI = Object.FindFirstObjectByType<LivestreamUI>();
            if (livestreamUI == null)
            {
                var uiGO = new GameObject("LivestreamUI");

                // Add UIDocument
                var uiDoc = uiGO.AddComponent<UIDocument>();

                // Wire PanelSettings
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
                if (panelSettings == null)
                {
                    panelSettings = FindAssetOfTypeGlobal<PanelSettings>();
                }
                if (panelSettings != null)
                {
                    uiDoc.panelSettings = panelSettings;
                }
                else
                {
                    Debug.LogWarning("[CreateLivestreamScene] No PanelSettings asset found.");
                }

                // Wire UXML source asset
                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
                if (uxml != null)
                {
                    uiDoc.visualTreeAsset = uxml;
                }
                else
                {
                    Debug.LogWarning($"[CreateLivestreamScene] UXML not found at {UxmlPath}.");
                }

                // Add LivestreamUI and wire its _uiDocument
                livestreamUI = uiGO.AddComponent<LivestreamUI>();
                var uiSO = new SerializedObject(livestreamUI);
                SetObjectRef(uiSO, "_uiDocument", uiDoc);
                uiSO.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log("[CreateLivestreamScene] Created LivestreamUI with UIDocument.");
            }

            // --- Step 4: AnimationConfig (find or create) ---

            AnimationConfig animConfig = FindAssetOfType<AnimationConfig>(AnimConfigFolder);
            if (animConfig == null)
            {
                animConfig = FindAssetOfType<AnimationConfig>("Assets/AyaLiveStream");
            }
            if (animConfig == null)
            {
                animConfig = FindAssetOfTypeGlobal<AnimationConfig>();
            }
            if (animConfig == null)
            {
                // Create a default AnimationConfig with common animations
                animConfig = ScriptableObject.CreateInstance<AnimationConfig>();
                animConfig.animations = new AnimationConfig.AnimationEntry[]
                {
                    new AnimationConfig.AnimationEntry { name = "wave", description = "Friendly wave greeting" },
                    new AnimationConfig.AnimationEntry { name = "nod", description = "Nodding in agreement" },
                    new AnimationConfig.AnimationEntry { name = "think", description = "Thinking pose, hand on chin" },
                    new AnimationConfig.AnimationEntry { name = "laugh", description = "Laughing at something funny" },
                    new AnimationConfig.AnimationEntry { name = "point", description = "Pointing at something to draw attention" },
                };

                string configFolder = "Assets/AyaLiveStream/Data";
                if (!AssetDatabase.IsValidFolder(configFolder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/AyaLiveStream"))
                        AssetDatabase.CreateFolder("Assets", "AyaLiveStream");
                    AssetDatabase.CreateFolder("Assets/AyaLiveStream", "Data");
                }
                AssetDatabase.CreateAsset(animConfig, $"{configFolder}/AnimationConfig.asset");
                Debug.Log("[CreateLivestreamScene] Created default AnimationConfig with 5 animations.");
            }

            // --- Step 5: Load beat and bot ScriptableObjects ---

            NarrativeBeatConfig[] beats = FindAllAssetsOfType<NarrativeBeatConfig>(BeatDataFolder);
            if (beats.Length == 0)
            {
                Debug.LogWarning($"[CreateLivestreamScene] No NarrativeBeatConfig assets in {BeatDataFolder}. " +
                    "Run 'AI Embodiment > Samples > Create Demo Beat Assets' first.");
            }

            ChatBotConfig[] bots = FindAllAssetsOfType<ChatBotConfig>(BotConfigFolder);
            if (bots.Length == 0)
            {
                Debug.LogWarning($"[CreateLivestreamScene] No ChatBotConfig assets in {BotConfigFolder}. " +
                    "Run 'AI Embodiment > Samples > Migrate Chat Bot Configs' first.");
            }

            // --- Step 6: LivestreamController + subsystems ---

            // Try to swap AyaSampleController, otherwise create new GO
            GameObject controllerGO;
            var ayaController = Object.FindFirstObjectByType<AyaSampleController>();
            if (ayaController != null)
            {
                controllerGO = ayaController.gameObject;
                Object.DestroyImmediate(ayaController);
                Debug.Log("[CreateLivestreamScene] Replaced AyaSampleController.");
            }
            else
            {
                controllerGO = new GameObject("LivestreamController");
            }

            var livestreamController = controllerGO.AddComponent<LivestreamController>();
            var narrativeDirector = controllerGO.AddComponent<NarrativeDirector>();
            var chatBotManager = controllerGO.AddComponent<ChatBotManager>();
            var pttController = controllerGO.AddComponent<PushToTalkController>();
            var sceneTransitionHandler = controllerGO.AddComponent<SceneTransitionHandler>();

            // --- Step 7: Wire all SerializeField references ---

            // LivestreamController
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

            // NarrativeDirector
            {
                var so = new SerializedObject(narrativeDirector);
                SetObjectRef(so, "_session", personaSession);
                SetObjectRef(so, "_livestreamUI", livestreamUI);
                SetObjectRef(so, "_chatBotManager", chatBotManager);
                SetObjectArray(so, "_beats", beats);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ChatBotManager
            {
                var so = new SerializedObject(chatBotManager);
                SetObjectRef(so, "_livestreamUI", livestreamUI);
                SetObjectRef(so, "_session", personaSession);
                SetObjectRef(so, "_narrativeDirector", narrativeDirector);
                SetObjectArray(so, "_bots", bots);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // PushToTalkController
            {
                var so = new SerializedObject(pttController);
                SetObjectRef(so, "_session", personaSession);
                SetObjectRef(so, "_narrativeDirector", narrativeDirector);
                SetObjectRef(so, "_livestreamUI", livestreamUI);
                SetObjectRef(so, "_chatBotManager", chatBotManager);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // SceneTransitionHandler
            {
                var so = new SerializedObject(sceneTransitionHandler);
                SetObjectRef(so, "_narrativeDirector", narrativeDirector);
                SetObjectRef(so, "_session", personaSession);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- Step 8: Save ---

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            if (!string.IsNullOrEmpty(activeScene.path))
            {
                AddSceneToBuildSettings(activeScene.path);
            }

            Debug.Log($"[CreateLivestreamScene] Complete! Scene: {activeScene.name}");
            Debug.Log($"[CreateLivestreamScene] PersonaSession: OK, " +
                $"LivestreamUI: {(livestreamUI != null ? "OK" : "MISSING")}, " +
                $"AnimationConfig: {animConfig.animations.Length} anims, " +
                $"{beats.Length} beat(s), {bots.Length} bot(s)");
        }

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

        private static T FindAssetOfType<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static T FindAssetOfTypeGlobal<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static T[] FindAllAssetsOfType<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new T[0];
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            T[] assets = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }
            return assets;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var scene in scenes)
            {
                if (scene.path == scenePath) return;
            }
            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++) newScenes[i] = scenes[i];
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
            Debug.Log($"[CreateLivestreamScene] Added to Build Settings: {scenePath}");
        }
    }
}
