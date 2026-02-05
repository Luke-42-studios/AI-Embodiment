namespace AIEmbodiment
{
    /// <summary>
    /// Static inventory of Chirp 3 HD voice names, supported language locales,
    /// and helper methods for building Cloud TTS API voice identifiers.
    /// Used by both the custom Inspector dropdown (PersonaConfigEditor) and
    /// runtime voice name resolution in <see cref="ChirpTTSClient"/>.
    /// </summary>
    public static class ChirpVoiceList
    {
        /// <summary>
        /// Sentinel value for the "Custom" voice option in the Inspector dropdown.
        /// When selected, the developer provides a <c>voiceCloningKey</c> instead
        /// of a standard voice name.
        /// </summary>
        public const string CustomVoice = "Custom";

        /// <summary>
        /// All 30 Chirp 3 HD voice short names, sorted alphabetically.
        /// These are the friendly display names used in the Inspector dropdown.
        /// </summary>
        public static readonly string[] Voices = new[]
        {
            "Achernar",
            "Achird",
            "Algenib",
            "Algieba",
            "Alnilam",
            "Aoede",
            "Autonoe",
            "Callirrhoe",
            "Charon",
            "Despina",
            "Enceladus",
            "Erinome",
            "Fenrir",
            "Gacrux",
            "Iapetus",
            "Kore",
            "Laomedeia",
            "Leda",
            "Orus",
            "Puck",
            "Pulcherrima",
            "Rasalgethi",
            "Sadachbia",
            "Sadaltager",
            "Schedar",
            "Sulafat",
            "Umbriel",
            "Vindemiatrix",
            "Zephyr",
            "Zubenelgenubi"
        };

        /// <summary>
        /// Language locale code and human-readable display name pair.
        /// </summary>
        public readonly struct ChirpLanguage
        {
            /// <summary>BCP-47 language code (e.g., "en-US").</summary>
            public readonly string Code;

            /// <summary>Human-readable name for Inspector display (e.g., "English (US)").</summary>
            public readonly string DisplayName;

            public ChirpLanguage(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }
        }

        /// <summary>
        /// All supported Chirp 3 HD language locales (47 GA + 2 Preview), sorted by display name.
        /// </summary>
        public static readonly ChirpLanguage[] Languages = new[]
        {
            new ChirpLanguage("ar-XA", "Arabic"),
            new ChirpLanguage("bn-IN", "Bengali (India)"),
            new ChirpLanguage("bg-BG", "Bulgarian"),
            new ChirpLanguage("yue-HK", "Chinese (Hong Kong) [Preview]"),
            new ChirpLanguage("cmn-CN", "Chinese (Mandarin)"),
            new ChirpLanguage("hr-HR", "Croatian"),
            new ChirpLanguage("cs-CZ", "Czech"),
            new ChirpLanguage("da-DK", "Danish"),
            new ChirpLanguage("nl-BE", "Dutch (Belgium)"),
            new ChirpLanguage("nl-NL", "Dutch (Netherlands)"),
            new ChirpLanguage("en-AU", "English (Australia)"),
            new ChirpLanguage("en-GB", "English (UK)"),
            new ChirpLanguage("en-IN", "English (India)"),
            new ChirpLanguage("en-US", "English (US)"),
            new ChirpLanguage("et-EE", "Estonian"),
            new ChirpLanguage("fi-FI", "Finnish"),
            new ChirpLanguage("fr-CA", "French (Canada)"),
            new ChirpLanguage("fr-FR", "French (France)"),
            new ChirpLanguage("de-DE", "German"),
            new ChirpLanguage("el-GR", "Greek"),
            new ChirpLanguage("gu-IN", "Gujarati (India)"),
            new ChirpLanguage("he-IL", "Hebrew"),
            new ChirpLanguage("hi-IN", "Hindi (India)"),
            new ChirpLanguage("hu-HU", "Hungarian"),
            new ChirpLanguage("id-ID", "Indonesian"),
            new ChirpLanguage("it-IT", "Italian"),
            new ChirpLanguage("ja-JP", "Japanese"),
            new ChirpLanguage("kn-IN", "Kannada (India)"),
            new ChirpLanguage("ko-KR", "Korean"),
            new ChirpLanguage("lv-LV", "Latvian"),
            new ChirpLanguage("lt-LT", "Lithuanian"),
            new ChirpLanguage("ml-IN", "Malayalam (India)"),
            new ChirpLanguage("mr-IN", "Marathi (India)"),
            new ChirpLanguage("pl-PL", "Polish"),
            new ChirpLanguage("pt-BR", "Portuguese (Brazil)"),
            new ChirpLanguage("pa-IN", "Punjabi (India) [Preview]"),
            new ChirpLanguage("ro-RO", "Romanian"),
            new ChirpLanguage("ru-RU", "Russian"),
            new ChirpLanguage("sr-RS", "Serbian"),
            new ChirpLanguage("sk-SK", "Slovak"),
            new ChirpLanguage("sl-SI", "Slovenian"),
            new ChirpLanguage("es-ES", "Spanish (Spain)"),
            new ChirpLanguage("es-US", "Spanish (US)"),
            new ChirpLanguage("sw-KE", "Swahili (Kenya)"),
            new ChirpLanguage("ta-IN", "Tamil (India)"),
            new ChirpLanguage("te-IN", "Telugu (India)"),
            new ChirpLanguage("th-TH", "Thai"),
            new ChirpLanguage("tr-TR", "Turkish"),
            new ChirpLanguage("vi-VN", "Vietnamese")
        };

        /// <summary>
        /// Builds the full Cloud TTS API voice name from a language code and voice short name.
        /// Example: <c>GetApiVoiceName("en-US", "Achernar")</c> returns <c>"en-US-Chirp3-HD-Achernar"</c>.
        /// </summary>
        /// <param name="languageCode">BCP-47 language code (e.g., "en-US").</param>
        /// <param name="voiceShortName">Chirp 3 HD voice short name (e.g., "Achernar").</param>
        /// <returns>Full API voice name for the Cloud TTS synthesize request.</returns>
        public static string GetApiVoiceName(string languageCode, string voiceShortName)
        {
            return $"{languageCode}-Chirp3-HD-{voiceShortName}";
        }

        /// <summary>
        /// Returns all voice short names plus "Custom" as the last entry.
        /// Intended for Inspector dropdown population.
        /// </summary>
        /// <returns>String array with 30 voice names followed by "Custom".</returns>
        public static string[] GetVoiceDisplayNames()
        {
            string[] names = new string[Voices.Length + 1];
            Voices.CopyTo(names, 0);
            names[Voices.Length] = CustomVoice;
            return names;
        }
    }
}
