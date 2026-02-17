using UnityEngine;
using UnityEditor;
using AIEmbodiment.Samples;

namespace AIEmbodiment.Samples.Editor
{
    /// <summary>
    /// One-time editor menu script that creates NarrativeBeatConfig .asset files
    /// for the 3-beat demo narrative arc (warm-up, art process, characters).
    /// Run via: AI Embodiment > Samples > Create Demo Beat Assets
    /// </summary>
    public static class CreateBeatAssets
    {
        [MenuItem("AI Embodiment/Samples/Create Demo Beat Assets")]
        public static void Create()
        {
            string folderPath = "Assets/AyaLiveStream/Data";

            // Create directory if it does not exist
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/AyaLiveStream"))
                {
                    AssetDatabase.CreateFolder("Assets", "AyaLiveStream");
                }
                AssetDatabase.CreateFolder("Assets/AyaLiveStream", "Data");
            }

            int count = 0;

            // ---------------------------------------------------------------
            // Beat 1: Warm-Up (Getting to know Aya)
            // Urgency: Low -- casual, friendly opening
            // ---------------------------------------------------------------
            {
                var beat = ScriptableObject.CreateInstance<NarrativeBeatConfig>();
                beat.beatId = "BEAT_WARMUP";
                beat.title = "Warm-Up";
                beat.timeBudgetSeconds = 180f;
                beat.urgency = GoalUrgency.Low;
                beat.goalDescription =
                    "Aya has introduced herself and established a friendly rapport with the audience";
                beat.directorNote =
                    "[Director: You're just starting your art livestream. Greet your audience warmly, " +
                    "introduce yourself as Aya, and chat casually about your day and what you're planning " +
                    "to work on today. Be friendly and approachable.]";
                beat.slowChatDuringAya = true;
                beat.skipKeywords = new string[0];
                beat.scenes = new NarrativeSceneConfig[]
                {
                    new NarrativeSceneConfig
                    {
                        sceneId = "WARMUP_GREET",
                        type = SceneType.AyaDialogue,
                        description = "Aya greets viewers",
                        dialogueAlternatives = new[]
                        {
                            "[Director: Say hi to your viewers! You're excited to start streaming today. " +
                            "Welcome everyone and ask how they're doing.]",
                            "[Director: Welcome your audience to the stream. You're in a great mood today " +
                            "and ready to create something special.]"
                        },
                        maxResponsesToGenerate = 1,
                        isConditional = false,
                        conditionType = ConditionType.Always,
                        maxDuration = 60f,
                        requiredValue = 1
                    },
                    new NarrativeSceneConfig
                    {
                        sceneId = "WARMUP_CHAT",
                        type = SceneType.AyaChecksChat,
                        description = "Aya responds to early chat",
                        dialogueAlternatives = new string[0],
                        maxResponsesToGenerate = 2,
                        isConditional = false,
                        conditionType = ConditionType.Always,
                        maxDuration = 60f,
                        requiredValue = 1
                    },
                    new NarrativeSceneConfig
                    {
                        sceneId = "WARMUP_TRANSITION",
                        type = SceneType.AyaDialogue,
                        description = "Aya transitions to art",
                        dialogueAlternatives = new[]
                        {
                            "[Director: Mention what you're going to work on today -- you've been sketching " +
                            "some character designs and you're really excited about them.]"
                        },
                        maxResponsesToGenerate = 1,
                        isConditional = true,
                        conditionType = ConditionType.TimedOut,
                        maxDuration = 60f,
                        requiredValue = 1
                    }
                };

                AssetDatabase.CreateAsset(beat, $"{folderPath}/Beat_WarmUp.asset");
                EditorUtility.SetDirty(beat);
                count++;
            }

            // ---------------------------------------------------------------
            // Beat 2: Art Process (Creative flow and tools)
            // Urgency: Medium -- engaged, building anticipation
            // ---------------------------------------------------------------
            {
                var beat = ScriptableObject.CreateInstance<NarrativeBeatConfig>();
                beat.beatId = "BEAT_ART";
                beat.title = "Art Process";
                beat.timeBudgetSeconds = 240f;
                beat.urgency = GoalUrgency.Medium;
                beat.goalDescription =
                    "Aya has discussed her creative process and tools, building anticipation for the characters";
                beat.directorNote =
                    "[Director: You're now in your creative flow. Talk about your art process -- what tools " +
                    "and techniques you use, what inspires your style, share tips with viewers. You're working " +
                    "on character designs that are very close to your heart.]";
                beat.slowChatDuringAya = true;
                beat.skipKeywords = new[] { "movie", "film", "clip", "show us", "reveal", "watch" };
                beat.scenes = new NarrativeSceneConfig[]
                {
                    new NarrativeSceneConfig
                    {
                        sceneId = "ART_PROCESS",
                        type = SceneType.AyaDialogue,
                        description = "Aya discusses her tools and process",
                        dialogueAlternatives = new[]
                        {
                            "[Director: Talk about your drawing tools, your creative process, and what " +
                            "makes your art style unique. You're working on something special today.]"
                        },
                        maxResponsesToGenerate = 1,
                        isConditional = false,
                        conditionType = ConditionType.Always,
                        maxDuration = 60f,
                        requiredValue = 1
                    },
                    new NarrativeSceneConfig
                    {
                        sceneId = "ART_CHAT",
                        type = SceneType.AyaChecksChat,
                        description = "Aya responds to chat about art",
                        dialogueAlternatives = new string[0],
                        maxResponsesToGenerate = 2,
                        isConditional = false,
                        conditionType = ConditionType.Always,
                        maxDuration = 60f,
                        requiredValue = 1
                    },
                    new NarrativeSceneConfig
                    {
                        sceneId = "ART_TEASE",
                        type = SceneType.AyaDialogue,
                        description = "Aya teases character designs",
                        dialogueAlternatives = new[]
                        {
                            "[Director: Mention the characters you've been designing. You're particularly " +
                            "attached to one character whose story really moved you. Tease that you might " +
                            "show something special later.]"
                        },
                        maxResponsesToGenerate = 1,
                        isConditional = true,
                        conditionType = ConditionType.TimedOut,
                        maxDuration = 90f,
                        requiredValue = 1
                    }
                };

                AssetDatabase.CreateAsset(beat, $"{folderPath}/Beat_ArtProcess.asset");
                EditorUtility.SetDirty(beat);
                count++;
            }

            // ---------------------------------------------------------------
            // Beat 3: Characters (Story and emotional connection)
            // Urgency: High -- climactic, building to reveal
            // ---------------------------------------------------------------
            {
                var beat = ScriptableObject.CreateInstance<NarrativeBeatConfig>();
                beat.beatId = "BEAT_CHARACTERS";
                beat.title = "Characters";
                beat.timeBudgetSeconds = 180f;
                beat.urgency = GoalUrgency.High;
                beat.goalDescription =
                    "Aya has shared the story behind her characters, building emotional connection before the reveal";
                beat.directorNote =
                    "[Director: Now share the deeper story behind your characters. One character in particular " +
                    "has a story that's very personal to you. Talk about what inspired the character, the emotions " +
                    "behind the design, and hint that there's something you've been working on -- a short film -- " +
                    "that brings this character to life. Build up to revealing it.]";
                beat.slowChatDuringAya = true;
                beat.skipKeywords = new string[0];
                beat.scenes = new NarrativeSceneConfig[]
                {
                    new NarrativeSceneConfig
                    {
                        sceneId = "CHAR_STORY",
                        type = SceneType.AyaDialogue,
                        description = "Aya shares character backstory",
                        dialogueAlternatives = new[]
                        {
                            "[Director: Share the personal story behind your favorite character. What inspired " +
                            "you? What emotions did you put into the design? Your viewers can feel your passion.]"
                        },
                        maxResponsesToGenerate = 1,
                        isConditional = false,
                        conditionType = ConditionType.Always,
                        maxDuration = 60f,
                        requiredValue = 1
                    },
                    new NarrativeSceneConfig
                    {
                        sceneId = "CHAR_CHAT",
                        type = SceneType.AyaChecksChat,
                        description = "Aya responds to character questions",
                        dialogueAlternatives = new string[0],
                        maxResponsesToGenerate = 3,
                        isConditional = false,
                        conditionType = ConditionType.Always,
                        maxDuration = 60f,
                        requiredValue = 1
                    },
                    new NarrativeSceneConfig
                    {
                        sceneId = "CHAR_REVEAL",
                        type = SceneType.AyaDialogue,
                        description = "Aya builds to the reveal",
                        dialogueAlternatives = new[]
                        {
                            "[Director: You've been working on something special -- a short animated film " +
                            "featuring this character. You're incredibly proud of it. Build excitement and " +
                            "let your audience know you want to show them something amazing.]"
                        },
                        maxResponsesToGenerate = 1,
                        isConditional = true,
                        conditionType = ConditionType.TimedOut,
                        maxDuration = 60f,
                        requiredValue = 1
                    }
                };

                AssetDatabase.CreateAsset(beat, $"{folderPath}/Beat_Characters.asset");
                EditorUtility.SetDirty(beat);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created {count} NarrativeBeatConfig assets in {folderPath}/");
        }
    }
}
