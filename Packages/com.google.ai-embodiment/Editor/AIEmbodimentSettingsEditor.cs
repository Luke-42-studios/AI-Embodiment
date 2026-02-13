using UnityEditor;
using UnityEngine;

namespace AIEmbodiment.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="AIEmbodimentSettings"/>.
    /// Displays the API key as a password field with a Show/Hide toggle
    /// for screen-sharing safety during tutorials and streams.
    /// </summary>
    [CustomEditor(typeof(AIEmbodimentSettings))]
    public class AIEmbodimentSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty _apiKey;
        private bool _showApiKey;

        void OnEnable()
        {
            _apiKey = serializedObject.FindProperty("_apiKey");
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

            serializedObject.ApplyModifiedProperties();
        }
    }
}
