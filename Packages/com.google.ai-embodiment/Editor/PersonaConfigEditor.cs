using UnityEditor;
using UnityEngine;

namespace AIEmbodiment.Editor
{
    /// <summary>
    /// Custom Inspector for PersonaConfig. Shows/hides Chirp-specific fields
    /// based on VoiceBackend selection, with language-filtered voice dropdown
    /// and custom voice cloning support.
    /// </summary>
    [CustomEditor(typeof(PersonaConfig))]
    public class PersonaConfigEditor : UnityEditor.Editor
    {
        // Identity
        SerializedProperty _displayName;
        SerializedProperty _archetype;
        SerializedProperty _backstory;

        // Personality
        SerializedProperty _personalityTraits;
        SerializedProperty _speechPatterns;

        // Model
        SerializedProperty _modelName;
        SerializedProperty _temperature;

        // Voice
        SerializedProperty _voiceBackend;
        SerializedProperty _geminiVoiceName;
        SerializedProperty _chirpLanguageCode;
        SerializedProperty _chirpVoiceShortName;
        SerializedProperty _chirpVoiceName;
        SerializedProperty _synthesisMode;
        SerializedProperty _customVoiceName;
        SerializedProperty _voiceCloningKey;

        void OnEnable()
        {
            _displayName = serializedObject.FindProperty("displayName");
            _archetype = serializedObject.FindProperty("archetype");
            _backstory = serializedObject.FindProperty("backstory");

            _personalityTraits = serializedObject.FindProperty("personalityTraits");
            _speechPatterns = serializedObject.FindProperty("speechPatterns");

            _modelName = serializedObject.FindProperty("modelName");
            _temperature = serializedObject.FindProperty("temperature");

            _voiceBackend = serializedObject.FindProperty("voiceBackend");
            _geminiVoiceName = serializedObject.FindProperty("geminiVoiceName");
            _chirpLanguageCode = serializedObject.FindProperty("chirpLanguageCode");
            _chirpVoiceShortName = serializedObject.FindProperty("chirpVoiceShortName");
            _chirpVoiceName = serializedObject.FindProperty("chirpVoiceName");
            _synthesisMode = serializedObject.FindProperty("synthesisMode");
            _customVoiceName = serializedObject.FindProperty("customVoiceName");
            _voiceCloningKey = serializedObject.FindProperty("voiceCloningKey");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- Identity ---
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_archetype);
            EditorGUILayout.PropertyField(_backstory);

            EditorGUILayout.Space();

            // --- Personality ---
            EditorGUILayout.LabelField("Personality", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_personalityTraits, true);
            EditorGUILayout.PropertyField(_speechPatterns);

            EditorGUILayout.Space();

            // --- Model ---
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_modelName);
            EditorGUILayout.PropertyField(_temperature);

            EditorGUILayout.Space();

            // --- Voice ---
            EditorGUILayout.LabelField("Voice", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_voiceBackend);

            EditorGUI.indentLevel++;

            var backend = (VoiceBackend)_voiceBackend.enumValueIndex;

            if (backend == VoiceBackend.GeminiNative)
            {
                EditorGUILayout.PropertyField(_geminiVoiceName,
                    new GUIContent("Voice Name", "Gemini Live API voice name (e.g. Puck, Charon)"));
            }
            else if (backend == VoiceBackend.ChirpTTS)
            {
                DrawChirpFields();
            }

            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }

        void DrawChirpFields()
        {
            // --- Language dropdown ---
            var languages = ChirpVoiceList.Languages;
            string[] languageDisplayNames = new string[languages.Length];
            int currentLanguageIndex = 0;
            string currentLanguageCode = _chirpLanguageCode.stringValue;

            for (int i = 0; i < languages.Length; i++)
            {
                languageDisplayNames[i] = languages[i].DisplayName;
                if (languages[i].Code == currentLanguageCode)
                {
                    currentLanguageIndex = i;
                }
            }

            int newLanguageIndex = EditorGUILayout.Popup(
                new GUIContent("Language", "Cloud TTS locale for Chirp 3 HD synthesis"),
                currentLanguageIndex,
                languageDisplayNames);

            bool languageChanged = newLanguageIndex != currentLanguageIndex;
            if (languageChanged)
            {
                _chirpLanguageCode.stringValue = languages[newLanguageIndex].Code;
            }

            // --- Voice dropdown ---
            string[] voiceDisplayNames = ChirpVoiceList.GetVoiceDisplayNames();
            string currentShortName = _chirpVoiceShortName.stringValue;
            int currentVoiceIndex = 0;

            for (int i = 0; i < voiceDisplayNames.Length; i++)
            {
                if (voiceDisplayNames[i] == currentShortName)
                {
                    currentVoiceIndex = i;
                    break;
                }
            }

            int newVoiceIndex = EditorGUILayout.Popup(
                new GUIContent("Voice", "Chirp 3 HD voice name"),
                currentVoiceIndex,
                voiceDisplayNames);

            bool voiceChanged = newVoiceIndex != currentVoiceIndex;
            if (voiceChanged)
            {
                _chirpVoiceShortName.stringValue = voiceDisplayNames[newVoiceIndex];
            }

            // --- Auto-sync chirpVoiceName when language or voice changes ---
            if (languageChanged || voiceChanged)
            {
                string lang = _chirpLanguageCode.stringValue;
                string voice = _chirpVoiceShortName.stringValue;
                _chirpVoiceName.stringValue = ChirpVoiceList.GetApiVoiceName(lang, voice);
            }

            // --- Custom voice fields ---
            bool isCustom = _chirpVoiceShortName.stringValue == ChirpVoiceList.CustomVoice;
            if (isCustom)
            {
                EditorGUI.indentLevel++;
                _customVoiceName.stringValue = EditorGUILayout.TextField(
                    new GUIContent("Custom Voice Name", "Name of the custom/cloned voice"),
                    _customVoiceName.stringValue);
                _voiceCloningKey.stringValue = EditorGUILayout.TextField(
                    new GUIContent("Voice Cloning Key", "Key from voices:generateVoiceCloningKey API"),
                    _voiceCloningKey.stringValue);
                EditorGUILayout.HelpBox(
                    "Custom voices use plain text (no SSML). " +
                    "Cloning key is obtained via the voices:generateVoiceCloningKey API.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // --- Synthesis mode ---
            EditorGUILayout.PropertyField(_synthesisMode,
                new GUIContent("Synthesis Mode", "How text is sent to Chirp TTS for audio generation"));

            var mode = (TTSSynthesisMode)_synthesisMode.enumValueIndex;
            if (mode == TTSSynthesisMode.SentenceBySentence)
            {
                EditorGUILayout.HelpBox(
                    "Each sentence synthesized as it arrives. ~200-500ms latency per sentence.",
                    MessageType.Info);
            }
            else if (mode == TTSSynthesisMode.FullResponse)
            {
                EditorGUILayout.HelpBox(
                    "Entire response synthesized at once. Higher latency but single request.",
                    MessageType.Info);
            }
        }
    }
}
