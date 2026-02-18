using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using AIEmbodiment;
using AIEmbodiment.Samples;

namespace AIEmbodiment.Samples.Editor
{
    public static class ConversationDemoInstaller
    {
        [MenuItem("AI Embodiment/Samples/Create Conversation Demo Scene", false, 10)]
        public static void CreateScene()
        {
            // 1. Create a new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // 2. Create the main controller object
            var controllerGO = new GameObject("Conversation Controller");
            var controller = controllerGO.AddComponent<ConversationController>();
            var session = controllerGO.AddComponent<PersonaSession>();
            var audioPlayback = controllerGO.AddComponent<AudioPlayback>();
            var audioCapture = controllerGO.AddComponent<AudioCapture>(); // Added AudioCapture
            var audioSource = controllerGO.AddComponent<AudioSource>(); // Added AudioSource
            var chatUI = controllerGO.AddComponent<ChatUI>();
            
            // 3. Create UI Document
            var uiGO = new GameObject("UI Document");
            var uiDoc = uiGO.AddComponent<UIDocument>();
            uiGO.transform.SetParent(controllerGO.transform);
            
            // 4. Find and assign assets
            // Find UXML
            string[] uxmlGuids = AssetDatabase.FindAssets("ChatPanel t:VisualTreeAsset");
            if (uxmlGuids.Length > 0)
            {
                var uxmlPath = AssetDatabase.GUIDToAssetPath(uxmlGuids[0]);
                uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            else
            {
                Debug.LogError("Could not find ChatPanel.uxml. Please ensure the sample is imported.");
            }

            // Find Config
            string[] configGuids = AssetDatabase.FindAssets("SamplePersonaConfig t:ScriptableObject");
            if (configGuids.Length > 0)
            {
                var configPath = AssetDatabase.GUIDToAssetPath(configGuids[0]);
                var soSession = new SerializedObject(session);
                soSession.FindProperty("_config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PersonaConfig>(configPath);
                // Also wire up Audio components to Session here
                soSession.FindProperty("_audioCapture").objectReferenceValue = audioCapture;
                soSession.FindProperty("_audioPlayback").objectReferenceValue = audioPlayback;
                soSession.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("Could not find SamplePersonaConfig. Please assign a PersonaConfig manually.");
                // Still wire up audio even if config is missing
                var soSession = new SerializedObject(session);
                soSession.FindProperty("_audioCapture").objectReferenceValue = audioCapture;
                soSession.FindProperty("_audioPlayback").objectReferenceValue = audioPlayback;
                soSession.ApplyModifiedProperties();
            }

            // 5. Link components
            // ConversationController
            var soController = new SerializedObject(controller);
            soController.FindProperty("_session").objectReferenceValue = session;
            soController.FindProperty("_audioPlayback").objectReferenceValue = audioPlayback;
            soController.FindProperty("_ui").objectReferenceValue = chatUI;
            soController.ApplyModifiedProperties();

            // ChatUI
            var soUI = new SerializedObject(chatUI);
            soUI.FindProperty("_uiDocument").objectReferenceValue = uiDoc;
            soUI.ApplyModifiedProperties();
            
            // AudioPlayback
            var soPlayback = new SerializedObject(audioPlayback);
            soPlayback.FindProperty("_audioSource").objectReferenceValue = audioSource;
            soPlayback.ApplyModifiedProperties();
            
            // Find and assign Panel Settings
            string[] panelSettingsGuids = AssetDatabase.FindAssets("t:PanelSettings");
            if (panelSettingsGuids.Length > 0)
            {
                var panelSettingsPath = AssetDatabase.GUIDToAssetPath(panelSettingsGuids[0]);
                uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
            }
            else
            {
                Debug.LogWarning("No PanelSettings found. Please create one (Assets > Create > UI Toolkit > Panel Settings Asset) and assign it to the UI Document.");
            }
            
            Selection.activeGameObject = controllerGO;
            Debug.Log("Conversation Demo Scene Created!");
        }
    }
}
