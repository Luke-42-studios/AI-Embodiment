# AI Embodiment - Developer Guide

Build interactive AI characters in Unity with real-time voice, text, animation, and narrative direction powered by Gemini.

---

## Quick Setup

### 1. Configure API Key

1. Get a Google AI API key from [aistudio.google.com](https://aistudio.google.com)
2. In Unity: select `Assets/Resources/AIEmbodimentSettings` in the Project window
3. Paste your API key in the **Api Key** field

> **Custom Voice Cloning (Chirp 3):** Also requires a service account JSON key. Set the **Service Account Json Path** to the absolute path of your key file (keep it outside the project). Get it from Google Cloud Console > IAM > Service Accounts > Keys > Create new key (JSON).

### 2. Create a Persona

1. Assets > Create > **AI Embodiment > Persona Config**
2. Configure in Inspector:

| Field | Purpose | Default |
|-------|---------|---------|
| Display Name | Character name | "New Persona" |
| Backstory | Character background (sent to Gemini as system instruction) | - |
| Personality Traits | Array of trait keywords | - |
| Model Name | Gemini model ID | `gemini-2.5-flash-native-audio-preview-12-2025` |
| Temperature | Creativity (0 = deterministic, 2 = random) | 0.7 |
| Voice Backend | `GeminiNative`, `ChirpTTS`, or `Custom` | GeminiNative |
| Gemini Voice Name | Native voice (Puck, Charon, Kore, etc.) | Puck |

An example config exists at `Assets/AyaLiveStream/AyaPersonaConfig.asset`.

### 3. Set Up a Scene

**Option A: Livestream sample (one-click)**

```
1. AI Embodiment > Samples > Create Demo Beat Assets
2. AI Embodiment > Samples > Migrate Chat Bot Configs
3. Open your scene
4. AI Embodiment > Samples > Create Livestream Scene
5. Hit Play
```

This creates all GameObjects, wires all references, and adds the scene to Build Settings automatically.

**Option B: Minimal custom scene**

Create a GameObject with these components:

| Component | Purpose |
|-----------|---------|
| `PersonaSession` | Core session manager (assign your PersonaConfig) |
| `AudioCapture` | Microphone input (16kHz mono) |
| `AudioPlayback` | Speaker output (24kHz, resampled to system rate) |
| `AudioSource` | Required by AudioPlayback (set Play On Awake = false) |

Wire in Inspector:
- `PersonaSession._config` -> your PersonaConfig asset
- `PersonaSession._audioCapture` -> the AudioCapture component
- `PersonaSession._audioPlayback` -> the AudioPlayback component
- `AudioPlayback._audioSource` -> the AudioSource component

---

## Core API

### PersonaSession

The main entry point. All interaction flows through this MonoBehaviour.

**Connection:**

```csharp
PersonaSession session = GetComponent<PersonaSession>();

// Connect (call after registering functions)
session.Connect();

// Send text to the AI
session.SendText("Hello!");

// Push-to-talk
session.StartListening();  // Begin microphone capture
session.StopListening();   // End capture, triggers AI response

// Disconnect
session.Disconnect();
```

**Events:**

```csharp
// Connection state
session.OnStateChanged += (SessionState state) => { };

// AI speech (streaming text chunks)
session.OnOutputTranscription += (string text) => { };

// User speech (transcribed by Gemini)
session.OnInputTranscription += (string text) => { };

// AI finished responding
session.OnTurnComplete += () => { };

// AI started/stopped speaking
session.OnAISpeakingStarted += () => { };
session.OnAISpeakingStopped += () => { };

// Synchronized text + audio packets
session.OnSyncPacket += (SyncPacket packet) => { };

// Errors
session.OnError += (Exception ex) => { };
session.OnFunctionError += (string name, Exception ex) => { };
```

**Connection States:** `Disconnected` -> `Connecting` -> `Connected` -> `Disconnecting` -> `Disconnected` (or `Error`)

### Function Calling

Register functions **before** calling `Connect()`. The AI can then call them during conversation.

```csharp
// Define a function with typed parameters
var decl = new FunctionDeclaration("play_animation",
        "Play a character animation or gesture")
    .AddEnum("animation_name", "Animation to play",
        new[] { "wave", "nod", "laugh", "think", "point" });

// Register with a handler
session.RegisterFunction("play_animation", decl, (FunctionCallContext ctx) =>
{
    string anim = ctx.GetString("animation_name", "wave");
    animator.SetTrigger(anim);
    return null; // null = fire-and-forget (no response sent back to AI)
});

session.Connect();
```

**FunctionCallContext typed accessors:**

```csharp
ctx.GetString("key", "default");
ctx.GetInt("key", 0);
ctx.GetFloat("key", 0f);
ctx.GetBool("key", false);
```

**Function calling modes:**

```csharp
// Native Gemini tool calling (recommended)
PersonaSession.UseNativeFunctionCalling = true;

// Prompt-based fallback (parses [CALL: name {...}] from transcript)
PersonaSession.UseNativeFunctionCalling = false;
```

### Conversational Goals

Steer the AI's behavior at runtime:

```csharp
session.AddGoal("reveal", "Build toward showing the movie clip", GoalPriority.High);
session.ReprioritizeGoal("reveal", GoalPriority.Medium);
session.RemoveGoal("reveal");
```

Priority levels: `Low` (mention if natural), `Medium` (work toward), `High` (actively steer).

> Goals are applied to the system instruction at `Connect()`. Mid-session goal changes are stored locally and take effect at next connection.

### SyncPacket (Synchronized Output)

For coordinated subtitle display and audio playback:

```csharp
session.OnSyncPacket += (SyncPacket packet) =>
{
    if (packet.Type == SyncPacketType.TextAudio)
    {
        subtitleLabel.text += packet.Text;
        if (packet.Audio != null)
            audioPlayback.EnqueueAudio(packet.Audio);
        if (packet.IsTurnEnd)
            subtitleLabel.text = "";
    }
    else if (packet.Type == SyncPacketType.FunctionCall)
    {
        Debug.Log($"Function called: {packet.FunctionName}");
    }
};
```

---

## Voice Backends

### Gemini Native (default)

Audio generated directly by Gemini Live. Lowest latency, limited voice selection.

Set `PersonaConfig.voiceBackend = VoiceBackend.GeminiNative`.

### Chirp TTS (Google Cloud Text-to-Speech)

Higher quality HD voices with more options. Requires API key (standard voices) or service account (custom/cloned voices).

Set `PersonaConfig.voiceBackend = VoiceBackend.ChirpTTS` and configure:
- `chirpVoiceShortName` - Voice name (e.g., "Achernar")
- `chirpLanguageCode` - Language (e.g., "en-US")
- `synthesisMode` - `SentenceBySentence` (low latency) or `FullResponse` (higher quality)

**Custom voice cloning** additionally requires:
- `voiceCloningKey` set on PersonaConfig
- Service account JSON path set in AIEmbodimentSettings

### Custom TTS

Implement `ITTSProvider` on a MonoBehaviour and assign it to `PersonaConfig._customTTSProvider`:

```csharp
public class MyTTS : MonoBehaviour, ITTSProvider
{
    public async Awaitable<TTSResult> SynthesizeAsync(
        string text, string voiceName, string languageCode,
        Action<TTSResult> onAudioChunk = null)
    {
        float[] samples = await MyEngine.Synthesize(text);
        return new TTSResult(samples, 24000, 1); // 24kHz mono
    }

    public void Dispose() { }
}
```

---

## Livestream Sample

The full sample in `Assets/AyaLiveStream/` demonstrates a 10-minute interactive livestream experience.

### Architecture

```
LivestreamController (orchestrator)
    |
    +-- PersonaSession        (Gemini connection)
    +-- NarrativeDirector     (beat progression, director notes)
    +-- ChatBotManager        (6 bot personas, scripted + dynamic)
    +-- PushToTalkController  (finish-first PTT state machine)
    +-- SceneTransitionHandler (movie clip transition)
    +-- LivestreamUI          (UIElements chat feed + transcript)
    +-- FactTracker           (cross-system coherence)
    +-- AyaTranscriptBuffer   (Aya speech history for bot prompts)
```

### Experience Flow

1. **Loading** - PersonaSession connects to Gemini (15s timeout)
2. **Going Live** - UI transition, bots start chatting, narrative begins
3. **Beat 1: Warm-Up** (3 min) - Aya greets viewers, casual chat
4. **Beat 2: Art Process** (4 min) - Aya discusses creative process, teases characters
5. **Beat 3: Characters** (3 min) - Aya shares character story, builds to reveal
6. **Reveal** - Narrative completes, clean disconnect, movie clip scene loads

### Key Concepts

**Narrative Beats** (`NarrativeBeatConfig`): Time-budgeted segments with director notes sent to Gemini via `SendText`. Each beat has scenes (AyaDialogue, AyaChecksChat) that execute sequentially within the time budget.

**Catalyst Messages**: 25% of bot burst messages are narrative nudges (e.g., "omg are you gonna show us the thing??") that steer Aya toward the current beat's goal.

**Topic Keywords**: When the user mentions keywords matching a future beat (e.g., "movie", "reveal"), the narrative skips ahead to that beat.

**Finish-First PTT**: If the user presses Space while Aya is speaking, a visual acknowledgment appears but recording defers until Aya finishes. This prevents audio interruption.

**Cross-System Coherence**: `FactTracker` records narrative facts. `AyaTranscriptBuffer` stores Aya's recent speech. Both are injected into bot prompts so bots react to what Aya actually said and never contradict established facts.

### Customization

| What | Where |
|------|-------|
| Narrative content | Edit beat `.asset` files in `Assets/AyaLiveStream/Data/` |
| Bot personalities | Edit bot `.asset` files in `Assets/AyaLiveStream/ChatBotConfigs/` |
| Available animations | Edit `AnimationConfig.asset` in `Assets/AyaLiveStream/Data/` |
| Chat timing | `ChatBotManager` Inspector: burst interval, message delay, max bots |
| Dead air threshold | `LivestreamController` Inspector: `_deadAirThreshold` (default 10s) |
| Movie scene name | `SceneTransitionHandler` Inspector: `_movieSceneName` |
| UI layout | `Assets/AyaLiveStream/UI/LivestreamPanel.uxml` and `.uss` |

### Editor Tools

| Menu Path | What It Does |
|-----------|-------------|
| AI Embodiment > Samples > **Create Demo Beat Assets** | Creates 3 narrative beat configs (warm-up, art, characters) |
| AI Embodiment > Samples > **Migrate Chat Bot Configs** | Creates 6 bot personality configs |
| AI Embodiment > Samples > **Migrate Response Patterns** | Fills bot message pools from nevatars data |
| AI Embodiment > Samples > **Create Livestream Scene** | One-click scene setup with all components wired |

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "No API key configured" | Select `Assets/Resources/AIEmbodimentSettings`, paste your API key |
| No audio output | Ensure `AudioPlayback` has an `AudioSource` assigned, Play On Awake = false |
| No microphone input | Ensure `AudioCapture` is on the PersonaSession GameObject |
| Functions not working | Register functions **before** calling `Connect()` |
| Custom voice fails | Set service account JSON path in AIEmbodimentSettings + voiceCloningKey on PersonaConfig |
| Bots not chatting | Run "Migrate Chat Bot Configs" editor tool, assign bot configs to ChatBotManager |
| No narrative progression | Run "Create Demo Beat Assets", assign beat configs to NarrativeDirector |
| Scene won't transition | Add movie scene to File > Build Settings > Scenes In Build |
| Audio feedback loop | PersonaSession auto-suppresses mic while AI speaks (built-in) |
| PTT not recording | Check PushToTalkController has `_session` reference wired |

---

## Project Structure

```
Packages/com.google.ai-embodiment/Runtime/    # Package runtime (do not modify)
    PersonaSession.cs                         # Core session manager
    PersonaConfig.cs                          # Persona configuration
    AIEmbodimentSettings.cs                   # API key storage
    AudioCapture.cs / AudioPlayback.cs        # Audio I/O
    GeminiLiveClient.cs                       # WebSocket client
    ChirpTTSClient.cs                         # Google Cloud TTS
    FunctionDeclaration.cs                    # Function schema builder
    PacketAssembler.cs                        # Text/audio sync
    GoogleServiceAccountAuth.cs               # OAuth2 for custom voices

Assets/AyaLiveStream/                         # Sample application
    LivestreamController.cs                   # Orchestrator
    NarrativeDirector.cs                      # Beat progression
    ChatBotManager.cs                         # Bot system
    PushToTalkController.cs                   # PTT state machine
    SceneTransitionHandler.cs                 # Scene loading
    LivestreamUI.cs                           # UI controller
    FactTracker.cs                            # Shared facts
    AyaTranscriptBuffer.cs                    # Aya speech history
    GeminiTextClient.cs                       # REST API for bots
    UI/LivestreamPanel.uxml                   # UI layout
    UI/LivestreamPanel.uss                    # UI styles
    Editor/                                   # Editor tools
    Data/                                     # Beat + animation configs
    ChatBotConfigs/                            # Bot personality configs

Assets/Resources/
    AIEmbodimentSettings.asset                # API key (singleton)
```
