# External Integrations

**Analysis Date:** 2026-02-05

## APIs & External Services

**Firebase AI Logic (Gemini / Vertex AI):**
- Primary AI integration for the project. Provides generative text, live audio/video streaming, image generation, and function calling.
- SDK: Source-level C# SDK at `Assets/Firebase/FirebaseAI/` (Firebase AI SDK 13.7.0)
- Auth: API key via `FirebaseApp.Options.ApiKey` (loaded from `Assets/google-services.json`)
- Backend options: GoogleAI or VertexAI (configurable via `FirebaseAI.Backend.GoogleAI()` or `FirebaseAI.Backend.VertexAI()`)
- REST base URL (HTTP): `https://firebasevertexai.googleapis.com/v1beta`
- WebSocket URL (Live): `wss://firebasevertexai.googleapis.com/ws`

**Available Model Types:**

| Model Class | Purpose | File |
|---|---|---|
| `GenerativeModel` | Text generation, streaming, token counting, chat | `Assets/Firebase/FirebaseAI/GenerativeModel.cs` |
| `LiveGenerativeModel` | Real-time bidirectional audio/video/text via WebSocket | `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs` |
| `ImagenModel` | Image generation from text prompts (Imagen 3) | `Assets/Firebase/FirebaseAI/Imagen/ImagenModel.cs` |
| `TemplateGenerativeModel` | Server-side prompt template execution | `Assets/Firebase/FirebaseAI/TemplateGenerativeModel.cs` |
| `TemplateImagenModel` | Server-side template image generation | `Assets/Firebase/FirebaseAI/Imagen/ImagenModel.cs` |
| `Chat` | Multi-turn conversation with history | `Assets/Firebase/FirebaseAI/Chat.cs` |

**API Endpoints Used:**

| Endpoint | Model | Transport |
|---|---|---|
| `:generateContent` | `GenerativeModel` | HTTP POST |
| `:streamGenerateContent?alt=sse` | `GenerativeModel` (streaming) | HTTP POST + SSE |
| `:countTokens` | `GenerativeModel` | HTTP POST |
| `:predict` | `ImagenModel` | HTTP POST |
| `/BidiGenerateContent` | `LiveGenerativeModel` | WebSocket |
| `:templateGenerateContent` | `TemplateGenerativeModel` | HTTP POST |
| `:templateStreamGenerateContent?alt=sse` | `TemplateGenerativeModel` (streaming) | HTTP POST + SSE |
| `:templatePredict` | `TemplateImagenModel` | HTTP POST |

**HTTP Headers Set on All Requests:**
- `x-goog-api-key`: Firebase API key
- `x-goog-api-client`: `gl-csharp/8.0 fire/{sdkVersion}`
- `X-Firebase-AppId`: Firebase App ID (if data collection enabled)
- `X-Firebase-AppVersion`: Unity Application version (if data collection enabled)
- `X-Firebase-AppCheck`: App Check token (if Firebase App Check is present)
- `Authorization`: `Firebase {authToken}` (if Firebase Auth is present and user is logged in)

See `Assets/Firebase/FirebaseAI/Internal/HttpHelpers.cs` for implementation.

**Google Search Grounding:**
- Tool type: `GoogleSearch` struct in `Assets/Firebase/FirebaseAI/FunctionCalling.cs`
- Allows Gemini to ground responses with live web search results

**URL Context:**
- Tool type: `UrlContext` struct in `Assets/Firebase/FirebaseAI/URLContext.cs`
- Allows providing public web URLs as additional context to the model

**Code Execution:**
- Tool type: `CodeExecution` struct in `Assets/Firebase/FirebaseAI/FunctionCalling.cs`
- Enables the model to generate and execute Python code

## Data Storage

**Databases:**
- None detected. No Firestore, Realtime Database, or local database SDKs present.

**File Storage:**
- Firebase Cloud Storage configured: `nevatars-b05fb.firebasestorage.app` (in `Assets/google-services.json`)
- `FileDataPart` in `Assets/Firebase/FirebaseAI/ModelContent.cs` supports `gs://` URIs for referencing Cloud Storage files in AI prompts
- No Cloud Storage client SDK is present -- file references are passed to the AI API only

**Caching:**
- None detected

## Authentication & Identity

**Auth Provider:**
- Firebase Auth (optional, discovered via reflection at runtime)
- Implementation: `Assets/Firebase/FirebaseAI/Internal/FirebaseInterops.cs` uses reflection to find `Firebase.Auth.FirebaseAuth` at runtime
- If `Firebase.Auth` assembly is present, auth tokens are automatically attached to API requests
- The Firebase Auth SDK itself is NOT currently included in the project (reflection init will silently skip)

**App Check:**
- Firebase App Check (optional, discovered via reflection at runtime)
- Implementation: `Assets/Firebase/FirebaseAI/Internal/FirebaseInterops.cs` uses reflection to find `Firebase.AppCheck.FirebaseAppCheck`
- If `Firebase.AppCheck` assembly is present, App Check tokens are automatically attached to API requests
- The Firebase App Check SDK itself is NOT currently included in the project (reflection init will silently skip)

## Monitoring & Observability

**Error Tracking:**
- None. Firebase Crashlytics SDK is not included (editor icons present in `Assets/Editor Default Resources/Firebase/` but SDK not installed).

**Logs:**
- `UnityEngine.Debug.Log` / `Debug.LogError` - Used behind conditional compilation flags
- `FIREBASE_LOG_REST_CALLS` - Preprocessor flag that enables logging of all HTTP request/response bodies (used in `GenerativeModel.cs`, `ImagenModel.cs`, `TemplateGenerativeModel.cs`)
- `FIREBASEAI_DEBUG_LOGGING` - Preprocessor flag that enables debug logging in `FirebaseInterops.cs` and `LiveSessionResponse.cs`
- Standard Unity player log (`usePlayerLog: 1` in `ProjectSettings/ProjectSettings.asset`)

## CI/CD & Deployment

**Hosting:**
- No deployment pipeline detected

**CI Pipeline:**
- None detected

## Environment Configuration

**Required env vars / config files:**
- `Assets/google-services.json` - Firebase project configuration (Android)
  - Must contain a valid `current_key` in `api_key` (currently placeholder `YOUR_API_KEY_HERE`)
  - Contains `project_id`: `nevatars-b05fb`
  - Contains `mobilesdk_app_id`: `1:344541669628:android:4e53016bf5aaa322198f79`
- `Assets/StreamingAssets/google-services-desktop.json` - Firebase desktop configuration (identical structure)
  - Used for editor/desktop standalone builds

**Secrets location:**
- API keys stored in `Assets/google-services.json` and `Assets/StreamingAssets/google-services-desktop.json`
- Both files are committed to git (ensure `current_key` is replaced before sharing repo)
- No `.env` files or external secret management detected

**Preprocessor Defines (optional):**
- `FIREBASE_LOG_REST_CALLS` - Enable verbose HTTP logging
- `FIREBASEAI_DEBUG_LOGGING` - Enable Firebase interop debug logging
- Define via `ProjectSettings > Player > Scripting Define Symbols` or `Assembly-CSharp.csproj`

## Webhooks & Callbacks

**Incoming:**
- None detected

**Outgoing:**
- None detected (all communication is client-initiated via HTTP/WebSocket)

## Live Session (WebSocket) Protocol Details

The `LiveGenerativeModel` establishes a WebSocket connection for real-time bidirectional AI communication.

**Connection flow:**
1. `LiveGenerativeModel.ConnectAsync()` opens a `ClientWebSocket` to the WebSocket endpoint
2. Initial setup message sent with model name, generation config, system instruction, and tools
3. Returns a `LiveSession` object for ongoing communication

**Sending data (via `LiveSession`):**
- `SendAsync()` - Send structured content with turn-complete signal
- `SendAudioRealtimeAsync()` - Stream raw audio (16-bit PCM at 16kHz)
- `SendVideoRealtimeAsync()` - Stream video frames
- `SendTextRealtimeAsync()` - Stream text input
- `SendAudioAsync()` - Convenience method accepting `float[]` (auto-converts to 16-bit PCM)

**Receiving data:**
- `ReceiveAsync()` - Returns `IAsyncEnumerable<LiveSessionResponse>`
- Response types: `LiveSessionContent` (text/audio), `LiveSessionToolCall`, `LiveSessionToolCallCancellation`
- Audio response is 16-bit PCM, convertible to `float[]` via `AudioAsFloat` property
- Transcription available via `InputTranscription` and `OutputTranscription` on `LiveSessionContent`

**Key files:**
- `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs` - Connection setup
- `Assets/Firebase/FirebaseAI/LiveSession.cs` - Send/receive operations
- `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs` - Response parsing
- `Assets/Firebase/FirebaseAI/LiveGenerationConfig.cs` - Config with `SpeechConfig`, `ResponseModality`, transcription settings

## Function Calling Protocol

The SDK supports server-side function calling where the model requests client-side function execution.

**Declaration:**
```csharp
// Assets/Firebase/FirebaseAI/FunctionCalling.cs
var func = new FunctionDeclaration("name", "description",
    new Dictionary<string, Schema> { ... },
    optionalParameters: new[] { "optParam" });
var tool = new Tool(func);
```

**Response handling:**
- `FunctionCallPart` in model responses contains `Name`, `Args`, and `Id`
- Client executes the function and returns `FunctionResponsePart` with results
- In live sessions, `LiveSessionToolCall` and `LiveSessionToolCallCancellation` handle function lifecycle

**Key files:**
- `Assets/Firebase/FirebaseAI/FunctionCalling.cs` - `FunctionDeclaration`, `Tool`, `ToolConfig`, `FunctionCallingConfig`
- `Assets/Firebase/FirebaseAI/ModelContent.cs` - `FunctionCallPart`, `FunctionResponsePart`

## Android Dependencies (External Dependency Manager)

Resolved at build time by the External Dependency Manager for Unity (EDM4U):

| Dependency | Version | Source |
|---|---|---|
| `com.google.firebase:firebase-common` | 22.0.1 | Maven |
| `com.google.firebase:firebase-analytics` | 23.0.0 | Maven |
| `com.google.android.gms:play-services-base` | 18.10.0 | Maven |
| `com.google.firebase:firebase-app-unity` | 13.7.0 | Local m2repo |

Config file: `Assets/Firebase/Editor/AppDependencies.xml`

## iOS Dependencies

| Dependency | Version | Source |
|---|---|---|
| `Firebase/Core` (CocoaPod) | 12.8.0 | CocoaPods |
| `FirebaseCore` (Swift Package) | 12.8.0 | github.com/firebase/firebase-ios-sdk.git |

Config file: `Assets/Firebase/Editor/AppDependencies.xml`

---

*Integration audit: 2026-02-05*
