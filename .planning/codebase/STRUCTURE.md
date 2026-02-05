# Codebase Structure

**Analysis Date:** 2026-02-05

## Directory Layout

```
AI-Embodiment/
├── Assets/
│   ├── Editor Default Resources/
│   │   └── Firebase/              # Firebase editor resources
│   ├── ExternalDependencyManager/
│   │   └── Editor/
│   │       └── 1.2.187/           # Google EDM4U plugin (dependency resolver)
│   ├── Firebase/
│   │   ├── Editor/                # Firebase build tools (DLLs, scripts)
│   │   ├── FirebaseAI/            # Firebase AI Logic SDK (C# source)
│   │   │   ├── Imagen/            # Imagen image generation models
│   │   │   └── Internal/          # SDK internal helpers (HTTP, JSON, reflection)
│   │   ├── m2repository/          # Android Maven repository artifacts
│   │   └── Plugins/               # Firebase native libraries (per-platform)
│   │       ├── iOS/
│   │       └── x86_64/            # Desktop native binaries (.dll/.so/.bundle)
│   ├── Plugins/
│   │   ├── iOS/
│   │   │   └── Firebase/          # iOS Firebase C++ static library
│   │   └── tvOS/
│   │       └── Firebase/          # tvOS Firebase C++ static library
│   ├── Scenes/                    # Unity scenes
│   ├── Settings/                  # URP render pipeline configuration
│   ├── StreamingAssets/            # Runtime config (Firebase google-services)
│   └── TutorialInfo/              # Unity template boilerplate (Readme asset)
│       ├── Icons/
│       └── Scripts/
│           └── Editor/
├── Packages/                      # Unity Package Manager manifest
├── ProjectSettings/               # Unity project settings (YAML assets)
├── .planning/
│   └── codebase/                  # GSD planning documents
├── .vscode/                       # VS Code workspace settings
├── AI-Embodiment.slnx             # Solution file
├── Assembly-CSharp.csproj         # Main C# project (auto-generated)
└── Assembly-CSharp-Editor.csproj  # Editor C# project (auto-generated)
```

## Directory Purposes

**`Assets/Firebase/FirebaseAI/`:**
- Purpose: Firebase AI Logic SDK -- the primary C# library for Gemini/Imagen AI interaction
- Contains: 21 C# source files implementing the full Firebase AI SDK
- Key files:
  - `Assets/Firebase/FirebaseAI/FirebaseAI.cs`: SDK entry point, singleton factory
  - `Assets/Firebase/FirebaseAI/GenerativeModel.cs`: Standard (HTTP) text generation
  - `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs`: Real-time WebSocket model connection
  - `Assets/Firebase/FirebaseAI/LiveSession.cs`: WebSocket session management, audio/video/text send/receive
  - `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs`: Response types (content, tool calls, transcription)
  - `Assets/Firebase/FirebaseAI/LiveGenerationConfig.cs`: Real-time generation config (speech, modalities)
  - `Assets/Firebase/FirebaseAI/ModelContent.cs`: Multi-modal content container with Part types
  - `Assets/Firebase/FirebaseAI/Chat.cs`: Stateful chat with history tracking
  - `Assets/Firebase/FirebaseAI/FunctionCalling.cs`: Function declarations, tools, tool config
  - `Assets/Firebase/FirebaseAI/GenerationConfig.cs`: Generation parameters (temperature, tokens, thinking)
  - `Assets/Firebase/FirebaseAI/Schema.cs`: OpenAPI-style schema definitions for structured output
  - `Assets/Firebase/FirebaseAI/Safety.cs`: Safety settings, ratings, harm categories
  - `Assets/Firebase/FirebaseAI/GenerateContentResponse.cs`: Response DTOs, usage metadata, grounding
  - `Assets/Firebase/FirebaseAI/Candidate.cs`: Response candidate with citations and grounding
  - `Assets/Firebase/FirebaseAI/RequestOptions.cs`: Timeout configuration
  - `Assets/Firebase/FirebaseAI/ResponseModality.cs`: Text/Image/Audio output modes
  - `Assets/Firebase/FirebaseAI/Citation.cs`: Citation metadata for source attribution
  - `Assets/Firebase/FirebaseAI/URLContext.cs`: URL context tool and metadata
  - `Assets/Firebase/FirebaseAI/ModalityTokenCount.cs`: Per-modality token counting

**`Assets/Firebase/FirebaseAI/Imagen/`:**
- Purpose: Imagen-specific model and response types
- Contains: Image generation model, config, safety settings, response types
- Key files:
  - `Assets/Firebase/FirebaseAI/Imagen/ImagenModel.cs`: Imagen image generation + TemplateImagenModel
  - `Assets/Firebase/FirebaseAI/Imagen/ImagenConfig.cs`: Image generation configuration
  - `Assets/Firebase/FirebaseAI/Imagen/ImagenSafety.cs`: Image-specific safety settings
  - `Assets/Firebase/FirebaseAI/Imagen/ImagenResponse.cs`: Image generation response types

**`Assets/Firebase/FirebaseAI/Internal/`:**
- Purpose: SDK-internal utilities not part of the public API
- Contains: HTTP helpers, Firebase interop via reflection, JSON parsing extensions, enum converters
- Key files:
  - `Assets/Firebase/FirebaseAI/Internal/HttpHelpers.cs`: URL construction, header setting, HTTP validation
  - `Assets/Firebase/FirebaseAI/Internal/FirebaseInterops.cs`: Reflection-based Auth/AppCheck token retrieval
  - `Assets/Firebase/FirebaseAI/Internal/InternalHelpers.cs`: JSON parsing extension methods (`ParseValue<T>`, `ParseObject<T>`, etc.)
  - `Assets/Firebase/FirebaseAI/Internal/EnumConverters.cs`: ResponseModality to string conversion

**`Assets/Firebase/Editor/`:**
- Purpose: Firebase Unity Editor integration and build tooling
- Contains: Compiled editor DLL, dependency XML, service config generator
- Key files:
  - `Assets/Firebase/Editor/Firebase.Editor.dll`: Editor extension (compiled)
  - `Assets/Firebase/Editor/AppDependencies.xml`: Android/iOS dependency declarations
  - `Assets/Firebase/Editor/FirebaseAI_version-13.7.0_manifest.txt`: SDK version manifest

**`Assets/Firebase/Plugins/x86_64/`:**
- Purpose: Desktop platform native Firebase C++ runtime
- Contains: `FirebaseCppApp-13_7_0.dll` (Windows), `.so` (Linux), `.bundle` (macOS)

**`Assets/Scenes/`:**
- Purpose: Unity scene files
- Contains: `SampleScene.unity` (default template scene, no custom objects)

**`Assets/Settings/`:**
- Purpose: Universal Render Pipeline (URP) configuration assets
- Contains: PC and Mobile renderer configs, pipeline assets, volume profiles
- Key files:
  - `Assets/Settings/PC_RPAsset.asset`: PC render pipeline asset
  - `Assets/Settings/PC_Renderer.asset`: PC renderer settings
  - `Assets/Settings/Mobile_RPAsset.asset`: Mobile render pipeline asset
  - `Assets/Settings/Mobile_Renderer.asset`: Mobile renderer settings
  - `Assets/Settings/DefaultVolumeProfile.asset`: Default post-processing volume
  - `Assets/Settings/SampleSceneProfile.asset`: Scene-specific volume profile

**`Assets/StreamingAssets/`:**
- Purpose: Runtime configuration files bundled with builds
- Key files:
  - `Assets/StreamingAssets/google-services-desktop.json`: Firebase project config (project ID: `nevatars-b05fb`, API key is placeholder)

**`Assets/TutorialInfo/`:**
- Purpose: Unity template boilerplate (Readme asset and editor script)
- Contains: Template-generated readme display script
- Key files:
  - `Assets/TutorialInfo/Scripts/Readme.cs`: ScriptableObject for Readme asset
  - `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`: Custom editor for Readme display

**`Packages/`:**
- Purpose: Unity Package Manager configuration
- Key files:
  - `Packages/manifest.json`: Package dependencies (URP 17.3.0, Input System 1.18.0, etc.)

**`ProjectSettings/`:**
- Purpose: Unity project-wide configuration (YAML-serialized assets)
- Key files:
  - `ProjectSettings/ProjectVersion.txt`: Unity 6000.3.7f1
  - `ProjectSettings/ProjectSettings.asset`: Player settings (product name: "AI-Embodiment")
  - `ProjectSettings/QualitySettings.asset`: Quality levels
  - `ProjectSettings/GraphicsSettings.asset`: Graphics pipeline settings

## Key File Locations

**Entry Points:**
- `Assets/Scenes/SampleScene.unity`: Default scene (launch point)
- `Assets/Firebase/FirebaseAI/FirebaseAI.cs`: SDK entry point for AI features

**Configuration:**
- `Assets/StreamingAssets/google-services-desktop.json`: Firebase project config
- `Packages/manifest.json`: Unity package dependencies
- `ProjectSettings/ProjectSettings.asset`: Unity player/build settings
- `Assets/Settings/PC_RPAsset.asset`: URP render pipeline config

**Core Logic (SDK):**
- `Assets/Firebase/FirebaseAI/GenerativeModel.cs`: HTTP-based content generation
- `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs`: WebSocket real-time model
- `Assets/Firebase/FirebaseAI/LiveSession.cs`: WebSocket session management
- `Assets/Firebase/FirebaseAI/ModelContent.cs`: Content/Part type system

**Testing:**
- No test files exist in the project

## Naming Conventions

**Files:**
- C# source files: PascalCase matching the primary type name (e.g., `GenerativeModel.cs`, `LiveSession.cs`)
- Unity assets: PascalCase with underscores for qualifiers (e.g., `PC_Renderer.asset`, `Mobile_RPAsset.asset`)
- Config files: lowercase-with-hyphens (e.g., `google-services-desktop.json`)

**Directories:**
- PascalCase for Unity asset folders (e.g., `FirebaseAI/`, `Scenes/`, `Settings/`)
- lowercase for project-level dirs (e.g., `.planning/`, `.vscode/`)

**C# Types (Firebase AI SDK):**
- Namespace: `Firebase.AI` (public), `Firebase.AI.Internal` (internal)
- Classes: PascalCase (e.g., `GenerativeModel`, `LiveSession`, `FirebaseAI`)
- Structs: PascalCase, mostly `readonly struct` (e.g., `ModelContent`, `LiveGenerationConfig`, `SafetySetting`)
- Enums: PascalCase with PascalCase members (e.g., `ResponseModality.Audio`, `FinishReason.MaxTokens`)
- Interfaces: `I` prefix (e.g., `ILiveSessionMessage`)
- Private fields: `_camelCase` prefix (e.g., `_firebaseApp`, `_clientWebSocket`)
- Internal methods: PascalCase with `Internal` prefix/suffix (e.g., `InternalCreateChat`, `InternalSendBytesAsync`)

## Where to Add New Code

**New Game Feature / MonoBehaviour Script:**
- Create: `Assets/Scripts/` (does not exist yet -- create this directory)
- Naming: PascalCase `.cs` files matching class name
- Follow Unity convention: one MonoBehaviour per file

**New AI Integration Script:**
- Create: `Assets/Scripts/AI/` for scripts that use Firebase AI SDK
- Example usage pattern:
  ```csharp
  using Firebase.AI;

  var ai = FirebaseAI.GetInstance(FirebaseAI.Backend.GoogleAI());
  var model = ai.GetGenerativeModel("gemini-2.0-flash");
  var response = await model.GenerateContentAsync("Hello");
  ```

**New Live/Real-time AI Script:**
- Create: `Assets/Scripts/AI/` for live session management
- Example usage pattern:
  ```csharp
  var liveModel = ai.GetLiveModel("gemini-2.0-flash",
      new LiveGenerationConfig(
          responseModalities: new[] { ResponseModality.Audio },
          speechConfig: SpeechConfig.UsePrebuiltVoice("Aoede")));
  var session = await liveModel.ConnectAsync();
  await session.SendAsync(ModelContent.Text("Hello"), turnComplete: true);
  await foreach (var response in session.ReceiveAsync()) { ... }
  ```

**New Scene:**
- Create: `Assets/Scenes/YourScene.unity`
- Add to build settings via `ProjectSettings/EditorBuildSettings.asset`

**New URP Rendering Config:**
- Create: `Assets/Settings/` following existing naming pattern (e.g., `VR_Renderer.asset`)

**New Prefab:**
- Create: `Assets/Prefabs/` (does not exist yet -- create this directory)

**New Materials/Shaders:**
- Create: `Assets/Materials/` or `Assets/Shaders/` (do not exist yet)

## Special Directories

**`Library/`:**
- Purpose: Unity-generated cache (compiled scripts, imported assets, package cache)
- Generated: Yes
- Committed: No (in .gitignore)

**`Temp/`:**
- Purpose: Unity editor temporary files
- Generated: Yes
- Committed: No

**`Logs/`:**
- Purpose: Unity editor log files
- Generated: Yes
- Committed: No

**`Assets/Firebase/`:**
- Purpose: Vendored Firebase AI SDK (Google-provided, Apache 2.0 licensed)
- Generated: No (imported via Firebase Unity SDK package)
- Committed: Yes
- Note: Do NOT modify these files. They are vendor code from Firebase SDK v13.7.0.

**`Assets/ExternalDependencyManager/`:**
- Purpose: Google External Dependency Manager for Unity (EDM4U) v1.2.187
- Generated: No (imported with Firebase SDK)
- Committed: Yes
- Note: Handles Android/iOS native dependency resolution at build time

**`.planning/`:**
- Purpose: GSD project planning and codebase analysis documents
- Generated: By GSD tooling
- Committed: Yes

---

*Structure analysis: 2026-02-05*
