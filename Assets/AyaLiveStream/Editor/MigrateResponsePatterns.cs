using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AIEmbodiment.Samples;

namespace AIEmbodiment.Samples.Editor
{
    /// <summary>
    /// One-time editor migration script that extracts chatBurstMessages from the
    /// nevatars ResponsePattern .asset files and distributes them across the 6
    /// AI-Embodiment ChatBotConfig assets as messageAlternatives, curated by
    /// personality fit.
    /// Run via: AI Embodiment > Samples > Migrate Response Patterns
    /// </summary>
    public static class MigrateResponsePatterns
    {
        /// <summary>
        /// Absolute path to the nevatars Patterns directory.
        /// This is a sibling project, not inside AI-Embodiment.
        /// </summary>
        private const string NevatarsPatternPath =
            "/home/cachy/workspaces/projects/games/nevatars/Assets/_Project/" +
            "DialogueAI/StreamingBotsDemo/Data/Patterns/";

        private const string ChatBotConfigFolder = "Assets/AyaLiveStream/ChatBotConfigs";

        // ────────────────────────────────────────────────────────────
        // Personality keyword sets (case-insensitive matching)
        // ────────────────────────────────────────────────────────────

        private static readonly string[] TeenFanKeywords =
        {
            "omg", "so cool", "love", "amazing", "yes", "lets go", "let's go",
            "can't wait", "exciting", "wow", "cute", "aww", "yay", "obsessed",
            "queen", "manifesting", "goals", "vibes", "gang", "rise up",
            "multi-talented", "multitalented", "subscribe", "fan", "we love",
            "so wholesome", "wholesome", "fam", "support", "inspiring"
        };

        private static readonly string[] DadJohnKeywords =
        {
            "great", "proud", "keep", "wonderful", "nice", "good", "awesome",
            "well done", "support", "family", "care", "health", "sweet",
            "you're doing", "encouraging", "wholesome", "beautiful",
            "passion", "dedication", "matters", "important", "take care",
            "we support", "respect", "love her", "she's gonna",
            "you belong", "no need to be nervous"
        };

        private static readonly string[] ArtStudentKeywords =
        {
            "art", "technique", "style", "color", "design", "composition",
            "skill", "creative", "process", "reference", "anatomy", "perspective",
            "brush", "layer", "palette", "digital", "traditional", "workflow",
            "texture", "illustration", "tablet", "settings", "tool", "program",
            "practice", "progress", "philosophical", "theory", "refs",
            "character design", "learning", "study"
        };

        private static readonly string[] RegularTechBroKeywords =
        {
            "been here", "day one", "community", "content", "stream",
            "remember when", "told you", "milestone", "journey", "origin",
            "channel", "youtube", "check", "setup", "gear", "tech",
            "cloud", "backup", "editing", "software", "file",
            "bookmarking", "taking notes", "list", "recommendations",
            "gonna check"
        };

        private static readonly string[] TrollKeywords =
        {
            "is it though", "actually", "seen better", "overrated", "meh",
            "mid", "not really", "sure about that", "controversial",
            "spicy", "incoming", "oh no", "here we go", "but",
            "hard to imagine", "doesn't", "obviously", "silly",
            "lol why"
        };

        private static readonly string[] LurkerKeywords =
        {
            // Lurker matches are handled by message length (1-3 words)
            // rather than keyword content. These are backup matches.
        };

        [MenuItem("AI Embodiment/Samples/Migrate Response Patterns")]
        public static void Migrate()
        {
            // ── Step 1: Validate nevatars path ──────────────────────

            if (!Directory.Exists(NevatarsPatternPath))
            {
                Debug.LogError(
                    $"[MigrateResponsePatterns] Nevatars Patterns directory not found at:\n" +
                    $"{NevatarsPatternPath}\n" +
                    "Ensure the nevatars project exists as a sibling project.");
                return;
            }

            // ── Step 2: Extract chatBurstMessages ───────────────────

            var allMessages = ExtractChatBurstMessages();

            if (allMessages.Count == 0)
            {
                Debug.LogError("[MigrateResponsePatterns] No chatBurstMessages found in any Pattern files.");
                return;
            }

            Debug.Log($"[MigrateResponsePatterns] Extracted {allMessages.Count} unique messages " +
                      $"from nevatars Pattern files.");

            // ── Step 3: Categorize by personality ───────────────────

            var categorized = CategorizeMessages(allMessages);

            // ── Step 4: Apply per-bot transforms ────────────────────

            ApplyTransforms(categorized);

            // ── Step 5: Write to ChatBotConfig assets ───────────────

            WriteToConfigs(categorized);
        }

        // ────────────────────────────────────────────────────────────
        // Step 1: Extract
        // ────────────────────────────────────────────────────────────

        private static HashSet<string> ExtractChatBurstMessages()
        {
            var messages = new HashSet<string>();

            string[] files = Directory.GetFiles(NevatarsPatternPath, "Pattern_*.asset");
            int fileCount = 0;

            foreach (string filePath in files)
            {
                string content = File.ReadAllText(filePath);

                // Find the chatBurstMessages: section
                int sectionIndex = content.IndexOf("chatBurstMessages:");
                if (sectionIndex < 0) continue;

                // Extract lines starting with "  - " after chatBurstMessages:
                string afterSection = content.Substring(
                    sectionIndex + "chatBurstMessages:".Length);

                string[] lines = afterSection.Split('\n');

                foreach (string rawLine in lines)
                {
                    string line = rawLine.TrimEnd('\r');

                    // chatBurstMessages list items are indented with "  - "
                    if (line.StartsWith("  - "))
                    {
                        string msg = line.Substring(4).Trim();
                        if (!string.IsNullOrWhiteSpace(msg))
                        {
                            messages.Add(msg);
                        }
                    }
                    else if (line.Length > 0 && !string.IsNullOrWhiteSpace(line)
                             && !line.StartsWith("  - "))
                    {
                        // We've left the chatBurstMessages list -- stop parsing
                        break;
                    }
                }

                fileCount++;
            }

            Debug.Log($"[MigrateResponsePatterns] Scanned {fileCount} Pattern files.");
            return messages;
        }

        // ────────────────────────────────────────────────────────────
        // Step 2: Categorize
        // ────────────────────────────────────────────────────────────

        private static Dictionary<string, List<string>> CategorizeMessages(HashSet<string> allMessages)
        {
            var result = new Dictionary<string, List<string>>
            {
                { "TeenFan_Miko", new List<string>() },
                { "Dad_John", new List<string>() },
                { "ArtStudent_Priya", new List<string>() },
                { "Regular_TechBro42", new List<string>() },
                { "Troll_xXShadowXx", new List<string>() },
                { "Lurker_Ghost404", new List<string>() }
            };

            var generalPool = new List<string>();

            foreach (string msg in allMessages)
            {
                string lower = msg.ToLowerInvariant();
                int wordCount = msg.Split(new[] { ' ' },
                    System.StringSplitOptions.RemoveEmptyEntries).Length;

                // Count keyword hits per bot
                int teenHits = CountKeywordHits(lower, TeenFanKeywords);
                int dadHits = CountKeywordHits(lower, DadJohnKeywords);
                int artHits = CountKeywordHits(lower, ArtStudentKeywords);
                int techHits = CountKeywordHits(lower, RegularTechBroKeywords);
                int trollHits = CountKeywordHits(lower, TrollKeywords);

                // Lurker: very short messages (1-3 words) with no strong keyword match
                bool isLurkerCandidate = wordCount <= 3;

                // Find best match
                int maxHits = Mathf.Max(teenHits,
                    Mathf.Max(dadHits,
                        Mathf.Max(artHits,
                            Mathf.Max(techHits, trollHits))));

                if (maxHits == 0)
                {
                    // No keyword match -- check lurker candidacy first
                    if (isLurkerCandidate && wordCount >= 1)
                    {
                        result["Lurker_Ghost404"].Add(msg);
                    }
                    else
                    {
                        generalPool.Add(msg);
                    }
                    continue;
                }

                // Assign to the best-matching bot (most keyword hits)
                // Tie-breaking order: Teen > Dad > Art > Tech > Troll
                if (teenHits == maxHits)
                    result["TeenFan_Miko"].Add(msg);
                else if (dadHits == maxHits)
                    result["Dad_John"].Add(msg);
                else if (artHits == maxHits)
                    result["ArtStudent_Priya"].Add(msg);
                else if (techHits == maxHits)
                    result["Regular_TechBro42"].Add(msg);
                else if (trollHits == maxHits)
                    result["Troll_xXShadowXx"].Add(msg);
            }

            // Distribute general pool evenly across 4 main bots
            // (skip Troll and Lurker for generic messages)
            string[] generalRecipients =
                { "TeenFan_Miko", "Dad_John", "ArtStudent_Priya", "Regular_TechBro42" };
            for (int i = 0; i < generalPool.Count; i++)
            {
                string recipient = generalRecipients[i % generalRecipients.Length];
                result[recipient].Add(generalPool[i]);
            }

            Debug.Log($"[MigrateResponsePatterns] General pool: {generalPool.Count} messages " +
                      "distributed across TeenFan, Dad, ArtStudent, TechBro.");

            return result;
        }

        private static int CountKeywordHits(string lowerMessage, string[] keywords)
        {
            int hits = 0;
            foreach (string keyword in keywords)
            {
                if (lowerMessage.Contains(keyword))
                    hits++;
            }
            return hits;
        }

        // ────────────────────────────────────────────────────────────
        // Step 3: Per-bot transforms
        // ────────────────────────────────────────────────────────────

        private static void ApplyTransforms(Dictionary<string, List<string>> categorized)
        {
            // TeenFan_Miko: ensure enthusiasm (add "!!" if no exclamation)
            TransformList(categorized["TeenFan_Miko"], msg =>
            {
                if (!msg.Contains("!"))
                    msg += "!!";
                return msg;
            });

            // Troll_xXShadowXx: strip exclamations, lowercase
            TransformList(categorized["Troll_xXShadowXx"], msg =>
            {
                msg = msg.Replace("!", "");
                msg = msg.ToLowerInvariant();
                return msg;
            });

            // Lurker_Ghost404: truncate to max 4 words, all lowercase, strip trailing punctuation
            TransformList(categorized["Lurker_Ghost404"], msg =>
            {
                msg = msg.ToLowerInvariant();
                string[] words = msg.Split(new[] { ' ' },
                    System.StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 4)
                {
                    msg = string.Join(" ", words.Take(4));
                }
                // Strip trailing punctuation except periods
                msg = msg.TrimEnd('!', '?', ',', ';', ':');
                return msg;
            });

            // Dad_John, ArtStudent_Priya, Regular_TechBro42: keep as-is
        }

        private static void TransformList(List<string> messages,
            System.Func<string, string> transform)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                messages[i] = transform(messages[i]);
            }
        }

        // ────────────────────────────────────────────────────────────
        // Step 4: Write to ChatBotConfig assets
        // ────────────────────────────────────────────────────────────

        private static void WriteToConfigs(Dictionary<string, List<string>> categorized)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:ChatBotConfig", new[] { ChatBotConfigFolder });

            if (guids.Length == 0)
            {
                Debug.LogError(
                    $"[MigrateResponsePatterns] No ChatBotConfig assets found in {ChatBotConfigFolder}.");
                return;
            }

            int totalWritten = 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<ChatBotConfig>(assetPath);
                if (config == null) continue;

                string botName = config.botName;
                if (!categorized.ContainsKey(botName))
                {
                    Debug.LogWarning(
                        $"[MigrateResponsePatterns] No categorized messages for bot: {botName}");
                    continue;
                }

                // Deduplicate within this bot's list
                List<string> messages = categorized[botName].Distinct().ToList();

                // Write via SerializedObject for proper asset serialization
                var serializedObject = new SerializedObject(config);
                SerializedProperty prop = serializedObject.FindProperty("messageAlternatives");

                prop.arraySize = messages.Count;
                for (int i = 0; i < messages.Count; i++)
                {
                    prop.GetArrayElementAtIndex(i).stringValue = messages[i];
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);

                Debug.Log($"[MigrateResponsePatterns] {botName}: {messages.Count} messages assigned.");
                totalWritten += messages.Count;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[MigrateResponsePatterns] Migration complete. " +
                      $"Total messages distributed: {totalWritten} across {guids.Length} bots.");
        }
    }
}
