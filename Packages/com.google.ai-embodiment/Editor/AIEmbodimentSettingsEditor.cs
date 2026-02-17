using UnityEditor;
using UnityEngine;

namespace AIEmbodiment.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="AIEmbodimentSettings"/>.
    /// Displays the API key as a password field with a Show/Hide toggle
    /// for screen-sharing safety during tutorials and streams.
    /// Also provides a service account JSON file picker for bearer auth
    /// with Chirp custom voice cloning.
    /// </summary>
    [CustomEditor(typeof(AIEmbodimentSettings))]
    public class AIEmbodimentSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty _apiKey;
        SerializedProperty _serviceAccountJsonPath;
        private bool _showApiKey;
        private bool _showServiceAccountPath;

        void OnEnable()
        {
            _apiKey = serializedObject.FindProperty("_apiKey");
            _serviceAccountJsonPath = serializedObject.FindProperty("_serviceAccountJsonPath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("API Key", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (_showApiKey)
            {
                EditorGUILayout.PropertyField(_apiKey, new GUIContent("API Key"));
            }
            else
            {
                _apiKey.stringValue = EditorGUILayout.PasswordField(
                    new GUIContent("API Key"), _apiKey.stringValue);
            }
            if (GUILayout.Button(_showApiKey ? "Hide" : "Show", GUILayout.Width(50)))
            {
                _showApiKey = !_showApiKey;
            }
            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrEmpty(_apiKey.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "API key is required. Get one from Google AI Studio: https://aistudio.google.com/apikey",
                    MessageType.Warning);
            }

            // --- Service Account Section ---

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Service Account (Optional)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (_showServiceAccountPath)
            {
                EditorGUILayout.PropertyField(
                    _serviceAccountJsonPath, new GUIContent("JSON Path"));
            }
            else
            {
                string path = _serviceAccountJsonPath.stringValue;
                string masked = string.IsNullOrEmpty(path)
                    ? ""
                    : new string('\u2022', Mathf.Min(path.Length, 40));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(new GUIContent("JSON Path"), masked);
                EditorGUI.EndDisabledGroup();
            }

            if (GUILayout.Button(
                _showServiceAccountPath ? "Hide" : "Show", GUILayout.Width(50)))
            {
                _showServiceAccountPath = !_showServiceAccountPath;
            }

            if (GUILayout.Button("Browse...", GUILayout.Width(70)))
            {
                string selected = EditorUtility.OpenFilePanel(
                    "Select Service Account JSON", "", "json");
                if (!string.IsNullOrEmpty(selected))
                {
                    _serviceAccountJsonPath.stringValue = selected;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Contextual help box
            string saPath = _serviceAccountJsonPath.stringValue;
            if (string.IsNullOrEmpty(saPath))
            {
                EditorGUILayout.HelpBox(
                    "Optional. Required for Chirp custom voice cloning (bearer auth). " +
                    "Get a service account JSON key from Google Cloud Console > " +
                    "IAM & Admin > Service Accounts.",
                    MessageType.Info);
            }
            else if (!System.IO.File.Exists(saPath))
            {
                EditorGUILayout.HelpBox(
                    "File not found at the specified path.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Service account configured. Bearer auth will be used for Chirp TTS.",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
