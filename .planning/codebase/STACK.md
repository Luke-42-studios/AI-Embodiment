# Technology Stack

**Analysis Date:** 2026-02-05

## Languages

**Primary:**
- C# 9.0 - All gameplay logic, Firebase AI SDK, editor scripts (LangVersion 9.0 per `Assembly-CSharp.csproj`)

**Secondary:**
- Python - Firebase editor tooling (`Assets/Firebase/Editor/generate_xml_from_google_services_json.py`, `Assets/Firebase/Editor/network_request.py`)
- YAML - Unity serialized assets and project settings (`ProjectSettings/ProjectSettings.asset`)
- XML - Android/iOS dependency declarations (`Assets/Firebase/Editor/AppDependencies.xml`)

## Runtime

**Environment:**
- Unity 6000.3.7f1 (Unity 6)
- .NET Standard 2.1 (target framework: `netstandard2.1`)
- Mono scripting backend (desktop), IL2CPP (Android, `scriptingBackend.Android: 1`)
- C# Language Version: 9.0

**Package Manager:**
- Unity Package Manager (UPM)
- Lockfile: present (`Packages/packages-lock.json`)

## Frameworks

**Core:**
- Unity 6000.3.7f1 - Game engine, rendering, audio, physics
- Universal Render Pipeline (URP) 17.3.0 - Rendering pipeline (`com.unity.render-pipelines.universal`)
- Firebase SDK for Unity 13.7.0 - Firebase App foundation (`Firebase.App.dll`, `FirebaseCppApp-13_7_0.so`)
- Firebase AI Logic SDK (source-level) - Gemini / Vertex AI integration (`Assets/Firebase/FirebaseAI/`)

**Testing:**
- Unity Test Framework 1.6.0 - Unit and play mode testing (`com.unity.test-framework`)
- NUnit 2.0.5 - Assertion library (via `com.unity.ext.nunit`)

**Build/Dev:**
- Unity IDE Integration for Rider 3.0.39 (`com.unity.ide.rider`)
- Unity IDE Integration for Visual Studio 2.0.26 (`com.unity.ide.visualstudio`)
- External Dependency Manager 1.2.187 - Android/iOS dependency resolution (`Assets/ExternalDependencyManager/`)

## Key Dependencies

**Critical:**
- `Firebase.App.dll` 13.7.0 - Core Firebase initialization, `FirebaseApp` singleton, API key management
- `Firebase.AI` (source) - Full Firebase AI Logic SDK providing Gemini Live, GenerativeModel, ImagenModel, Chat, function calling
- `Google.MiniJson.dll` - JSON serialization/deserialization used by the Firebase AI SDK
- `Firebase.Platform.dll` - Firebase platform abstraction layer
- `Firebase.TaskExtension.dll` - Task-based async extensions for Firebase

**Infrastructure:**
- `com.unity.inputsystem` 1.18.0 - New Input System for player input handling
- `com.unity.ai.navigation` 2.0.9 - AI NavMesh navigation for agent pathfinding
- `com.unity.ugui` 2.0.0 - Unity UI system
- `com.unity.timeline` 1.8.10 - Animation timeline sequencing
- `com.unity.visualscripting` 1.9.9 - Visual scripting (Bolt)
- `com.unity.multiplayer.center` 1.0.1 - Multiplayer discovery/setup

**Transitive (resolved by UPM):**
- `com.unity.burst` 1.8.27 - Burst compiler for high-performance code
- `com.unity.collections` 2.6.2 - Native collections
- `com.unity.mathematics` 1.3.3 - SIMD math library
- `com.unity.shadergraph` 17.3.0 - Shader Graph for URP

## Configuration

**Environment:**
- Firebase project: `nevatars-b05fb` (project ID from `Assets/google-services.json`)
- Firebase API key: configured via `Assets/google-services.json` (placeholder `YOUR_API_KEY_HERE` -- must be replaced)
- Desktop Firebase config: `Assets/StreamingAssets/google-services-desktop.json` (mirrors mobile config)
- Android package name: `com.google.nevatars`
- Firebase storage bucket: `nevatars-b05fb.firebasestorage.app`

**Build:**
- Solution file: `AI-Embodiment.slnx` (SDK-style, references `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj`)
- Default build target: Standalone Linux 64-bit (per `UnityBuildTarget: StandaloneLinux64:24`)
- Android scripting backend: IL2CPP
- Android min SDK: 25 (Android 7.1)
- Android graphics APIs: Vulkan + OpenGL ES (non-auto)
- Color space: Linear (`m_ActiveColorSpace: 1`)
- Active input handler: New Input System only (`activeInputHandler: 1`)
- Template origin: `com.unity.template.urp-blank@17.0.14`
- Bundle version: `0.1.0`

**VSCode:**
- Default solution: `AI-Embodiment.slnx`
- File exclusions configured for Unity Library, Temp, binary assets (`/.vscode/settings.json`)
- File associations: `.asset`, `.meta`, `.prefab`, `.unity` mapped to YAML

## Platform Requirements

**Development:**
- Unity Hub with Unity 6000.3.7f1 installed
- Linux x86_64 (current editor platform) or Windows/macOS
- .NET SDK (for IDE support / Rider / VS Code)
- Firebase project with google-services.json configured with a valid API key

**Production:**
- Standalone Linux/Windows/macOS desktop builds (current target)
- Android builds (configured with IL2CPP, min SDK 25)
- iOS builds (configured with min target 15.0)
- Firebase AI requires network access to `firebasevertexai.googleapis.com`

## Native Plugins

**Desktop (x86_64):**
- `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_7_0.so` (Linux)
- `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_7_0.dll` (Windows)
- `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_7_0.bundle` (macOS)

**iOS:**
- `Assets/Plugins/iOS/Firebase/libFirebaseCppApp.a` (static lib)
- `Assets/Firebase/Plugins/iOS/Firebase.App.dll` (managed)

**tvOS:**
- `Assets/Plugins/tvOS/Firebase/libFirebaseCppApp.a` (static lib)

**Android:**
- `Assets/Firebase/m2repository/` - Local Maven repo for `firebase-app-unity:13.7.0`
- Dependencies resolved via External Dependency Manager (`AppDependencies.xml`)
- `com.google.firebase:firebase-common:22.0.1`
- `com.google.firebase:firebase-analytics:23.0.0`
- `com.google.android.gms:play-services-base:18.10.0`

---

*Stack analysis: 2026-02-05*
