# Architecture

**Analysis Date:** 2026-02-05

## Pattern Overview

**Overall:** Early-stage Unity project with Firebase AI Logic SDK integration (vendor-provided SDK library pattern)

**Key Characteristics:**
- This is a fresh Unity 6 project (6000.3.7f1) created from the Universal 3D template
- The only meaningful code is the Firebase AI Logic SDK (`Firebase.AI` namespace), which is a vendored third-party library imported via the Firebase Unity SDK package
- No custom game scripts, MonoBehaviours, or gameplay logic exist yet -- the project is a shell awaiting application code
- The Firebase AI SDK provides three model interaction paradigms: batch generation (`GenerativeModel`), real-time streaming (`LiveGenerativeModel`/`LiveSession`), and image generation (`ImagenModel`)

## Layers

**Firebase AI SDK Layer (Vendor Code):**
- Purpose: Provides C# API for interacting with Google's Gemini and Imagen models via Firebase
- Location: `Assets/Firebase/FirebaseAI/`
- Contains: Model clients, configuration structs, request/response DTOs, JSON serialization, WebSocket/HTTP transport
- Depends on: `Firebase.App` (native C++ plugin at `Assets/Firebase/Plugins/`), `Google.MiniJSON`, `System.Net.Http`, `System.Net.WebSockets`
- Used by: Future application code (none exists yet)

**Firebase Native Plugins Layer:**
- Purpose: Platform-specific C++ Firebase App runtime libraries
- Location: `Assets/Firebase/Plugins/x86_64/`, `Assets/Plugins/iOS/Firebase/`, `Assets/Plugins/tvOS/Firebase/`
- Contains: `FirebaseCppApp-13_7_0.dll` (Windows), `.so` (Linux), `.bundle` (macOS), `.a` (iOS/tvOS)
- Depends on: Nothing (native binaries)
- Used by: Firebase AI SDK via `FirebaseApp` class

**Firebase Editor Tooling:**
- Purpose: Editor-time dependency resolution, service config generation
- Location: `Assets/Firebase/Editor/`, `Assets/ExternalDependencyManager/`
- Contains: `Firebase.Editor.dll`, `generate_xml_from_google_services_json.py/.exe`, `AppDependencies.xml`
- Depends on: Unity Editor
- Used by: Build pipeline only

**Unity Rendering Layer:**
- Purpose: Universal Render Pipeline (URP) configuration
- Location: `Assets/Settings/`
- Contains: PC and Mobile renderer/pipeline assets, volume profiles
- Depends on: `com.unity.render-pipelines.universal` package
- Used by: Unity scene rendering

**Scene Layer:**
- Purpose: Unity scene containing the application
- Location: `Assets/Scenes/`
- Contains: `SampleScene.unity` (default template scene)
- Depends on: Settings, future game objects
- Used by: Unity runtime

## Data Flow

**Standard Text Generation (HTTP):**

1. User code calls `FirebaseAI.GetInstance()` to obtain a `FirebaseAI` singleton
2. User calls `firebaseAI.GetGenerativeModel("gemini-2.0-flash")` to create a `GenerativeModel`
3. User calls `model.GenerateContentAsync("prompt")` or `GenerateContentStreamAsync("prompt")`
4. `GenerativeModel` constructs JSON payload via `MakeGenerateContentRequest()` with contents, config, tools, system instructions
5. `HttpHelpers.SetRequestHeaders()` adds API key, Firebase tokens (AppCheck + Auth via reflection), SDK version headers
6. HTTP POST to `https://firebasevertexai.googleapis.com/v1beta/projects/{projectId}/.../:generateContent`
7. Response JSON is deserialized via `GenerateContentResponse.FromJson()` into `Candidate` -> `ModelContent` -> `Part` hierarchy
8. User accesses `response.Text`, `response.FunctionCalls`, etc.

**Live/Real-time Streaming (WebSocket):**

1. User code calls `firebaseAI.GetLiveModel("gemini-2.0-flash")` to create a `LiveGenerativeModel`
2. User calls `model.ConnectAsync()` which opens a WebSocket to `wss://firebasevertexai.googleapis.com/ws/...`
3. Connection sends a setup message containing model name, generation config, system instructions, and tools
4. Returns a `LiveSession` wrapping the `ClientWebSocket`
5. User sends content via `session.SendAsync()` (text/function responses) or `session.SendAudioRealtimeAsync()` / `session.SendVideoRealtimeAsync()` / `session.SendTextRealtimeAsync()` (realtime input)
6. User receives responses via `session.ReceiveAsync()` which yields `LiveSessionResponse` structs
7. Responses contain `LiveSessionContent` (text/audio), `LiveSessionToolCall` (function calls), or `LiveSessionToolCallCancellation`
8. Audio data is sent as 16-bit PCM at 16kHz, converted between `float[]` and `byte[]` by helper methods

**Image Generation (HTTP):**

1. User calls `firebaseAI.GetImagenModel("imagen-3.0-generate-002")` to create an `ImagenModel`
2. User calls `model.GenerateImagesAsync("prompt")`
3. HTTP POST to `.../:predict` endpoint with prompt and parameters
4. Returns `ImagenGenerationResponse<ImagenInlineImage>`

**Chat with History:**

1. User calls `model.StartChat()` to create a `Chat` object
2. Each `SendMessageAsync()` prepends the full chat history to the request
3. On success, both user message and model response are appended to internal history list

## Key Abstractions

**FirebaseAI (Entry Point):**
- Purpose: Singleton factory for all AI model instances
- File: `Assets/Firebase/FirebaseAI/FirebaseAI.cs`
- Pattern: Thread-safe singleton keyed by `{appName}::{backend}`, uses `ConcurrentDictionary`
- Factory methods: `GetGenerativeModel()`, `GetLiveModel()`, `GetImagenModel()`, `GetTemplateGenerativeModel()`

**Backend (Configuration):**
- Purpose: Determines which Google AI backend to use (GoogleAI vs VertexAI)
- File: `Assets/Firebase/FirebaseAI/FirebaseAI.cs` (nested struct)
- Pattern: Static factory methods `Backend.GoogleAI()` and `Backend.VertexAI(location)`
- Affects URL construction, request format, and some response parsing

**ModelContent (Data Transfer):**
- Purpose: Polymorphic content container for multi-modal AI interactions
- File: `Assets/Firebase/FirebaseAI/ModelContent.cs`
- Pattern: Readonly struct with `Part` interface implementing multiple content types
- Part types: `TextPart`, `InlineDataPart`, `FileDataPart`, `FunctionCallPart`, `FunctionResponsePart`, `ExecutableCodePart`, `CodeExecutionResultPart`
- All Parts support `IsThought` flag for Gemini thinking features

**LiveSession (WebSocket Manager):**
- Purpose: Manages bidirectional WebSocket communication for real-time AI interaction
- File: `Assets/Firebase/FirebaseAI/LiveSession.cs`
- Pattern: `IDisposable` with thread-safe send via `SemaphoreSlim`, `IAsyncEnumerable` for receiving
- Handles PCM audio conversion between `float[]` (Unity) and `byte[]` (wire format)

**Tool / FunctionDeclaration (Function Calling):**
- Purpose: Defines callable functions the AI model can invoke
- File: `Assets/Firebase/FirebaseAI/FunctionCalling.cs`
- Pattern: Builder-style structs with JSON serialization; supports GoogleSearch, CodeExecution, UrlContext tools

**Schema (Type System):**
- Purpose: OpenAPI 3.0-subset schema definition for structured output
- File: `Assets/Firebase/FirebaseAI/Schema.cs`
- Pattern: Static factory methods (`Schema.String()`, `Schema.Object()`, `Schema.Array()`, etc.)

## Entry Points

**FirebaseAI.GetInstance() / FirebaseAI.DefaultInstance:**
- Location: `Assets/Firebase/FirebaseAI/FirebaseAI.cs`
- Triggers: Application code initialization
- Responsibilities: Creates/caches `FirebaseAI` instances keyed by app + backend

**LiveGenerativeModel.ConnectAsync():**
- Location: `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs`
- Triggers: Application code requesting real-time AI session
- Responsibilities: Opens WebSocket, sends setup message, returns `LiveSession`

**GenerativeModel.GenerateContentAsync():**
- Location: `Assets/Firebase/FirebaseAI/GenerativeModel.cs`
- Triggers: Application code requesting AI text/content generation
- Responsibilities: Sends HTTP request, deserializes response

**SampleScene.unity:**
- Location: `Assets/Scenes/SampleScene.unity`
- Triggers: Unity play mode / build
- Responsibilities: Default scene (currently empty template)

## Error Handling

**Strategy:** Exception-based with HTTP validation

**Patterns:**
- `HttpHelpers.ValidateHttpResponse()` reads error body content and throws `HttpRequestException` with status code, reason phrase, and error body (in `Assets/Firebase/FirebaseAI/Internal/HttpHelpers.cs`)
- WebSocket errors throw `WebSocketException` or `InvalidOperationException` for connection state issues
- JSON parsing uses `JsonParseOptions` flags: `ThrowMissingKey`, `ThrowInvalidCast`, `ThrowEverything` for required fields (in `Assets/Firebase/FirebaseAI/Internal/InternalHelpers.cs`)
- `LiveSession` uses `lock` + `_disposed` flag for thread-safe disposal, gracefully closes WebSocket
- Unknown JSON response keys are silently ignored (with optional `FIREBASE_LOG_REST_CALLS` / `FIREBASEAI_DEBUG_LOGGING` compile flags)

## Cross-Cutting Concerns

**Logging:** Conditional compilation via `FIREBASE_LOG_REST_CALLS` and `FIREBASEAI_DEBUG_LOGGING` scripting define symbols. Uses `UnityEngine.Debug.Log()` / `UnityEngine.Debug.LogError()` / `UnityEngine.Debug.LogWarning()`. Off by default.

**Authentication:** Handled via reflection in `Assets/Firebase/FirebaseAI/Internal/FirebaseInterops.cs`. Automatically discovers Firebase Auth and AppCheck at runtime via `System.Reflection`, caches Type/MethodInfo/PropertyInfo. Adds `X-Firebase-AppCheck` and `Authorization: Firebase {token}` headers when available. Falls back gracefully if Auth/AppCheck assemblies are not present.

**Serialization:** All JSON serialization uses `Google.MiniJSON` (`Json.Serialize` / `Json.Deserialize`). Every SDK type has internal `ToJson()` and `FromJson()` methods. Extension methods in `Assets/Firebase/FirebaseAI/Internal/InternalHelpers.cs` provide type-safe JSON parsing (`ParseValue<T>`, `ParseObject<T>`, `ParseObjectList<T>`, `ParseEnum<T>`, etc.).

**Configuration:** Firebase project config loaded from `Assets/StreamingAssets/google-services-desktop.json`. API key is currently placeholder (`YOUR_API_KEY_HERE`). The `FirebaseApp` class (from native plugin) handles configuration loading.

---

*Architecture analysis: 2026-02-05*
