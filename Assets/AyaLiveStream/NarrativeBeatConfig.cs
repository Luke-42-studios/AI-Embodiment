using System;
using UnityEngine;

namespace AIEmbodiment.Samples
{
    /// <summary>
    /// Scene types that define what happens during a narrative scene.
    /// Simplified from nevatars (5 types vs 6): dropped Login scene type.
    /// </summary>
    public enum SceneType
    {
        AyaDialogue,
        ChatBurst,
        AyaChecksChat,
        AyaAction,
        UserChoice
    }

    /// <summary>
    /// Conditions that determine when a scene transitions to the next.
    /// Simplified from nevatars (3 types vs 8): kept only the conditions
    /// relevant to a time-driven 3-beat demo.
    /// </summary>
    public enum ConditionType
    {
        TimedOut,
        QuestionsAnswered,
        Always
    }

    /// <summary>
    /// Urgency level for a narrative beat. Escalates across the narrative arc
    /// (Low -> Medium -> High) and influences director note phrasing and beat pacing.
    /// </summary>
    public enum GoalUrgency
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Defines a single scene within a narrative beat.
    /// Scenes are executed in order within their parent beat.
    /// Each scene has a type that determines its behavior and optional
    /// conditional transition settings.
    /// </summary>
    [Serializable]
    public class NarrativeSceneConfig
    {
        [Tooltip("Unique identifier for this scene (e.g. WARMUP_GREET).")]
        public string sceneId = "SCENE_ID";

        [Tooltip("What type of scene this is -- determines execution behavior.")]
        public SceneType type = SceneType.AyaDialogue;

        [TextArea(1, 2)]
        [Tooltip("Editor-only description of what this scene does.")]
        public string description;

        [Header("AyaDialogue")]
        [TextArea(2, 4)]
        [Tooltip("Director notes sent to Gemini via SendText. One is chosen at random.")]
        public string[] dialogueAlternatives;

        [Header("AyaChecksChat")]
        [Tooltip("Maximum number of chat responses Aya generates in this scene.")]
        public int maxResponsesToGenerate = 1;

        [Header("AyaAction (Phase 15)")]
        [Tooltip("Placeholder for Phase 15 action description.")]
        public string actionDescription;

        [Header("Conditional Transition")]
        [Tooltip("If true, this scene waits for a condition before advancing. If false, advances when done.")]
        public bool isConditional = false;

        [Tooltip("The condition type that triggers transition to the next scene.")]
        public ConditionType conditionType = ConditionType.TimedOut;

        [Tooltip("Maximum duration in seconds before this scene times out (for TimedOut condition).")]
        public float maxDuration = 60f;

        [Tooltip("Required value for QuestionsAnswered condition.")]
        public int requiredValue = 1;
    }

    /// <summary>
    /// Defines a narrative beat in the livestream demo.
    /// A beat represents a segment of the narrative arc with a time budget,
    /// goal, director note for Gemini steering, and an ordered list of scenes.
    /// Beats are linear (1->2->3) with escalating urgency.
    ///
    /// Create via Assets > Create > AI Embodiment > Samples > Narrative Beat.
    /// </summary>
    [CreateAssetMenu(fileName = "Beat_", menuName = "AI Embodiment/Samples/Narrative Beat")]
    public class NarrativeBeatConfig : ScriptableObject
    {
        [Header("Beat Identity")]
        [Tooltip("Unique identifier for this beat (e.g. BEAT_WARMUP).")]
        public string beatId = "BEAT_1";

        [Tooltip("Display title for this beat.")]
        public string title = "Beat Title";

        [Header("Timing & Urgency")]
        [Tooltip("Time budget in seconds. Beat advances when goal is met or time expires.")]
        public float timeBudgetSeconds = 180f;

        [Tooltip("Urgency level for this beat. Escalates across the narrative arc (Low -> Medium -> High). Influences director note phrasing and beat pacing.")]
        public GoalUrgency urgency = GoalUrgency.Low;

        [Header("Goals & Steering")]
        [TextArea(2, 4)]
        [Tooltip("What Aya should accomplish during this beat.")]
        public string goalDescription;

        [TextArea(3, 6)]
        [Tooltip("Text sent to Gemini via SendText at beat start to steer Aya's dialogue.")]
        public string directorNote;

        [Header("Scenes")]
        [Tooltip("Ordered list of scenes that execute during this beat.")]
        public NarrativeSceneConfig[] scenes;

        [Header("Chat Pacing")]
        [Tooltip("Slow down chat bursts while Aya is speaking during this beat.")]
        public bool slowChatDuringAya = true;

        [Header("Skip Detection")]
        [Tooltip("Keywords from the user that trigger a skip to the final beat (e.g. 'movie', 'reveal').")]
        public string[] skipKeywords;

        [Header("Catalyst")]
        [TextArea(1, 3)]
        [Tooltip("What are bots trying to nudge Aya toward in this beat?")]
        public string catalystGoal;

        [TextArea(1, 3)]
        [Tooltip("Catalyst messages sprinkled in chat to push narrative forward.")]
        public string[] catalystMessages;

        [Tooltip("Keywords in user speech that indicate interest in this beat's topic. Used for PTT skip-ahead to specific beats.")]
        public string[] topicKeywords;
    }
}
