# AI Embodiment

A Unity UPM package that lets you add AI-powered conversational characters to your game. Create a persona config, attach a session to a GameObject, and get real-time AI conversation with synchronized voice, text, and animation events.

Built on [Firebase AI Logic](https://firebase.google.com/docs/ai-logic) for Gemini Live and Google Cloud TTS Chirp 3 HD for high-quality voices.

## Features

- **Persona Configuration** -- ScriptableObject with personality traits, archetype, backstory, speech patterns, model selection, and voice settings
- **Real-time Conversation** -- Bidirectional audio/text streaming via Gemini Live WebSocket
- **Two Voice Backends** -- Gemini native audio (5 voices) or Chirp 3 HD TTS (30+ voices), selectable per-persona
- **Push-to-Talk** -- Microphone capture at 16kHz mono PCM with start/stop listening API
- **Streaming Audio Playback** -- Ring buffer with watermark buffering through any Unity AudioSource (supports spatialization and mixing)
- **Synchronization** -- PacketAssembler correlates text, audio, and events into unified SyncPackets for subtitle/animation sync
- **Function Calling** -- Register C# delegate handlers for AI-triggered game actions (emotes, scene changes, etc.)
- **Conversational Goals** -- Steer the AI with priority-based goals that inject into the system instruction at runtime
- **Thread-Safe** -- All Firebase callbacks marshaled to Unity main thread automatically

## Requirements

- Unity 6 (6000.0+)
- [Firebase AI Logic Unity SDK](https://firebase.google.com/docs/ai-logic/get-started?platform=unity) (v13.7.0+)
- Firebase project with Gemini API enabled
- `google-services.json` in `Assets/StreamingAssets/`

## Installation

### 1. Install Firebase AI Logic SDK

Download and import the Firebase AI Unity SDK `.unitypackage` from the [Firebase Unity SDK releases](https://firebase.google.com/docs/unity/setup).

You need at minimum:
- `FirebaseApp.unitypackage`
- `FirebaseAI.unitypackage`

### 2. Install AI Embodiment Package

**Via git URL** (recommended):

Open `Window > Package Manager > + > Add package from git URL`:

```
https://github.com/Luke-42-studios/AI-Embodiment.git?path=Packages/com.google.ai-embodiment
```

**Via local path:**

Clone this repo and add via `Add package from disk`, pointing to `Packages/com.google.ai-embodiment/package.json`.

### 3. Configure Firebase

1. Create a Firebase project at [console.firebase.google.com](https://console.firebase.google.com)
2. Enable the Gemini API (Vertex AI)
3. Download `google-services.json` and place it in your Unity project's `Assets/` folder

## Quick Start

### 1. Create a Persona

Right-click in Project window > **Create > AI Embodiment > Persona Config**

Configure in the Inspector:
- **Display Name**: Your character's name
- **Archetype**: Brief role description (e.g., "friendly shopkeeper")
- **Backstory**: Character background and personality
- **Personality Traits**: Array of trait keywords
- **Voice Backend**: GeminiNative or ChirpTTS
- **Voice Name**: Select a voice (Puck, Kore, Aoede, Charon, Fenrir for Gemini; 30+ for Chirp)

### 2. Set Up the Scene

Create an empty GameObject and add:

```
MyCharacter (GameObject)
  - PersonaSession       (assign your PersonaConfig)
  - AudioCapture         (microphone input)
  - AudioPlayback        (AI voice output, requires AudioSource)
```

### 3. Connect and Talk

```csharp
using AIEmbodiment;

public class MyGame : MonoBehaviour
{
    [SerializeField] private PersonaSession session;

    void Start()
    {
        session.OnOutputTranscription += text => Debug.Log($"AI: {text}");
        session.OnInputTranscription += text => Debug.Log($"You: {text}");
        session.OnStateChanged += state => Debug.Log($"State: {state}");
        session.Connect();
    }

    void Update()
    {
        // Push-to-talk with spacebar
        if (Input.GetKeyDown(KeyCode.Space)) session.StartListening();
        if (Input.GetKeyUp(KeyCode.Space)) session.StopListening();
    }
}
```

### 4. Add Function Calling

```csharp
using Firebase.AI;

// Register before Connect()
session.RegisterFunction("emote",
    new FunctionDeclaration("emote",
        "Express an emotion visually",
        new Dictionary<string, Schema>
        {
            { "animation", Schema.Enum(new[] { "wave", "laugh", "think" }, "Animation to play") }
        }),
    ctx => {
        string anim = ctx.GetString("animation", "idle");
        myAnimator.Play(anim);
        return null; // fire-and-forget
    });
```

### 5. Add Conversational Goals

```csharp
// Inject a goal after some gameplay event
session.AddGoal(
    "quest_hint",
    "Steer the conversation toward mentioning the hidden cave to the north.",
    GoalPriority.Medium
);

// Remove when goal is achieved
session.RemoveGoal("quest_hint");
```

## Architecture

```
PersonaConfig (ScriptableObject)
    |
PersonaSession (MonoBehaviour) --- main developer API
    |
    +-- SystemInstructionBuilder --- builds prompt from config + goals
    +-- FunctionRegistry ----------- manages function declarations + handlers
    +-- GoalManager --------------- tracks active goals with priorities
    +-- PacketAssembler ----------- correlates text + audio + events
    +-- AudioCapture -------------- microphone input (16kHz PCM)
    +-- AudioPlayback ------------- ring buffer output via AudioSource
    +-- ChirpTTSClient ------------ HTTP TTS for Chirp voice backend
```

## Events

| Event | Type | Description |
|-------|------|-------------|
| `OnStateChanged` | `Action<SessionState>` | Session lifecycle (Connecting, Connected, Error, Disconnected) |
| `OnOutputTranscription` | `Action<string>` | AI speech text (streaming, for subtitles) |
| `OnInputTranscription` | `Action<string>` | User speech-to-text transcript |
| `OnTurnComplete` | `Action` | AI finished a response turn |
| `OnAISpeakingStarted` | `Action` | AI voice audio begins |
| `OnAISpeakingStopped` | `Action` | AI voice audio ends |
| `OnUserSpeakingStarted` | `Action` | User microphone active |
| `OnUserSpeakingStopped` | `Action` | User microphone stopped |
| `OnSyncPacket` | `Action<SyncPacket>` | Correlated text + audio + event packet |
| `OnError` | `Action<Exception>` | Session errors |
| `OnFunctionError` | `Action<string, Exception>` | Function handler errors (non-fatal) |

## Sample Scene

Import via **Package Manager > AI Embodiment > Samples > Aya Live Stream**.

The sample demonstrates:
- Dark-themed UI Toolkit chat panel
- Push-to-talk (spacebar + on-screen button)
- Streaming transcription display
- 3 function calls (emote with 17 animations, start_movie, start_drawing)
- Conversational goal injection after 3 exchanges
- Speaking indicator with glow animation

## Voice Backends

### Gemini Native Audio (default)
Audio generated directly by the Gemini model. Lower latency, 5 voices available.

### Chirp 3 HD TTS
High-quality text-to-speech via Google Cloud TTS API. 30+ voices across multiple languages. Requires a Cloud TTS API key configured on the `ChirpTTSClient`.

Select per-persona in the PersonaConfig Inspector. The Chirp Inspector shows voice dropdowns with language filtering.

## License

See [LICENSE.md](Packages/com.google.ai-embodiment/LICENSE.md).
