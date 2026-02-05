# Codebase Concerns

**Analysis Date:** 2026-02-05

## Tech Debt

**No Project-Authored Code Exists:**
- Issue: The project contains zero custom C# scripts. All code under `Assets/` belongs to third-party SDKs (Firebase AI Logic, ExternalDependencyManager) or Unity template boilerplate (TutorialInfo). The scene `Assets/Scenes/SampleScene.unity` is a default empty scene with no custom GameObjects.
- Files: No project scripts found outside `Assets/Firebase/`, `Assets/TutorialInfo/`, `Assets/ExternalDependencyManager/`, `Assets/Plugins/`
- Impact: The project is in a bootstrapped state only. No game logic, no AI integration code, no character embodiment, no audio pipeline, no UI exists. Everything must be built from scratch.
- Fix approach: Create a proper `Assets/Scripts/` directory structure and begin implementing the Gemini Live integration, audio capture, and character animation systems.

**Unity Template Boilerplate Still Present:**
- Issue: The `Assets/TutorialInfo/` directory and `Assets/Readme.asset` are leftover Unity template files that serve no purpose.
- Files: `Assets/TutorialInfo/Scripts/Readme.cs`, `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`, `Assets/TutorialInfo/Icons/`, `Assets/Readme.asset`
- Impact: Clutters the project and can confuse developers about what is project code vs template artifacts.
- Fix approach: Delete `Assets/TutorialInfo/` and `Assets/Readme.asset` along with their `.meta` files.

**Missing .gitignore:**
- Issue: No `.gitignore` file exists in the project root. The git repository has no commits yet (branch `master` has zero history). Without a `.gitignore`, committing will include `Library/`, `Temp/`, `Logs/`, `UserSettings/`, and `obj/` directories which are build artifacts and should never be version controlled.
- Files: Project root `/home/cachy/workspaces/projects/games/AI-Embodiment/`
- Impact: First commit will be massive (Library alone is hundreds of MB) and include platform-specific build caches, making the repo unusable for collaboration. The Firebase SDK's native binaries under `Assets/Firebase/Plugins/x86_64/` (366MB total in `Assets/Firebase/`) will also bloat the repository.
- Fix approach: Add a standard Unity `.gitignore` before the first commit. Include patterns for `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, `*.csproj`, `*.sln`, `*.slnx`. Consider using Git LFS for the Firebase native binaries (`.so`, `.dll`, `.bundle` files).

**Firebase SDK TODOs in Vendor Code:**
- Issue: The Firebase AI Logic SDK (vendor code) contains 5 TODO comments indicating incomplete implementations. While these are vendor concerns (not project code), they affect what the project can rely on.
- Files:
  - `Assets/Firebase/FirebaseAI/ModelContent.cs:120` - Missing helper factories for common C#/Unity types
  - `Assets/Firebase/FirebaseAI/ModelContent.cs:122` - Same area, questioning additional factory methods
  - `Assets/Firebase/FirebaseAI/LiveSession.cs:305` - Unresolved behavior on WebSocket server close
  - `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs:133` - Incomplete field parsing for serverContent
  - `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs:146` - Unresolved decision on unknown JSON response keys
- Impact: The LiveSession's handling of server-initiated WebSocket close (`LiveSession.cs:305`) and incomplete serverContent parsing (`LiveSessionResponse.cs:133`) could cause silent failures or missed data during real-time Gemini Live sessions.
- Fix approach: These are upstream SDK issues. Monitor Firebase AI Logic SDK releases for fixes. When building the integration layer, add defensive error handling around `ReceiveAsync` to catch edge cases the SDK does not handle.

## Security Considerations

**Firebase API Key Placeholder in Committed Config:**
- Risk: Both `Assets/google-services.json` and `Assets/StreamingAssets/google-services-desktop.json` contain the placeholder `"YOUR_API_KEY_HERE"` for the API key. The project ID (`nevatars-b05fb`) and app ID (`1:344541669628:android:4e53016bf5aaa322198f79`) are real values that are committed.
- Files: `Assets/google-services.json:19`, `Assets/StreamingAssets/google-services-desktop.json:19`
- Current mitigation: The API key is a placeholder, so it cannot be used. However, the project number and app ID are exposed.
- Recommendations:
  1. Replace `YOUR_API_KEY_HERE` with the real key only at build time or via environment variable injection
  2. Add `google-services.json` and `google-services-desktop.json` to `.gitignore` and provide a template file instead (e.g., `google-services.json.template`)
  3. Enable Firebase App Check to restrict API access to legitimate app instances
  4. Restrict the API key in Google Cloud Console to only the Firebase AI Logic API

**API Key Exposed in WebSocket URL:**
- Risk: The `LiveGenerativeModel.GetURL()` method appends the Firebase API key directly as a query parameter (`?key={_firebaseApp.Options.ApiKey}`) in the WebSocket connection URL. This means the API key is visible in network logs, proxy servers, and potentially browser developer tools.
- Files: `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs:81`, `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs:87`
- Current mitigation: This is the SDK's design (vendor code). The connection uses `wss://` (TLS), so the key is encrypted in transit. However, it may appear in server-side access logs.
- Recommendations: Use Firebase App Check and Auth tokens (which the SDK supports via `FirebaseInterops.AddFirebaseTokensAsync`) to add defense in depth. Restrict the API key's allowed APIs and referrers in Google Cloud Console.

**API Key Also in HTTP Headers:**
- Risk: The HTTP-based `GenerativeModel` sends the API key in the `x-goog-api-key` header for non-WebSocket calls.
- Files: `Assets/Firebase/FirebaseAI/Internal/HttpHelpers.cs:72`
- Current mitigation: Headers are encrypted via HTTPS. This is the standard Firebase approach.
- Recommendations: Ensure App Check is enabled for production builds.

**No Authentication Required by Default:**
- Risk: Firebase Auth integration is optional and uses reflection to discover the Auth SDK at runtime (`FirebaseInterops.InitializeAuthReflection`). If Auth is not present, API calls proceed with only the API key, meaning anyone with the key can make requests.
- Files: `Assets/Firebase/FirebaseAI/Internal/FirebaseInterops.cs:274-348`
- Current mitigation: Auth reflection silently skips if the Auth SDK is not present.
- Recommendations: Add Firebase Auth SDK to the project and require user authentication before Gemini Live sessions. Configure Firebase security rules to require authenticated users.

## Performance Bottlenecks

**WebSocket Receive Buffer Size:**
- Problem: The `LiveSession.ReceiveAsync` method uses a fixed 4096-byte receive buffer for WebSocket messages. For Gemini Live audio responses (16-bit PCM at 16kHz), audio chunks can be substantially larger.
- Files: `Assets/Firebase/FirebaseAI/LiveSession.cs:296`
- Cause: The SDK uses `StringBuilder` to accumulate multi-frame messages, but each frame requires a separate async receive call with a 4KB buffer. Large audio responses require many iterations.
- Improvement path: This is vendor code. The `StringBuilder` concatenation approach (`LiveSession.cs:315`) for binary data is inefficient -- it converts bytes to UTF-8 string, appends to StringBuilder, then the response parser re-deserializes. For custom audio handling, consider processing audio chunks directly from the WebSocket rather than relying on the SDK's `ReceiveAsync` pattern.

**Audio Conversion Allocations:**
- Problem: `LiveSession.ConvertTo16BitPCM` and `LiveSessionResponse.ConvertBytesToFloat` allocate new arrays on every call. During real-time audio streaming, this creates significant GC pressure.
- Files: `Assets/Firebase/FirebaseAI/LiveSession.cs:252-268`, `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs:91-103`
- Cause: No buffer pooling or reuse. Each audio chunk allocates `short[]`, `byte[]`, and `float[]` arrays.
- Improvement path: In the project's audio pipeline wrapper, implement buffer pooling (e.g., `ArrayPool<byte>.Shared`) to reduce GC allocations. Pre-allocate fixed-size buffers for the expected audio frame size.

**HttpClient Per-Model Instance:**
- Problem: Each `GenerativeModel` instance creates its own `HttpClient` in its constructor. `HttpClient` is designed to be reused as a singleton to avoid socket exhaustion.
- Files: `Assets/Firebase/FirebaseAI/GenerativeModel.cs:79-83`
- Cause: Vendor SDK design. The timeout is set per-client based on `RequestOptions`.
- Improvement path: This is vendor code. Avoid creating many `GenerativeModel` instances. Reuse a single instance where possible. The `FirebaseAI` class uses `ConcurrentDictionary` to cache `FirebaseAI` instances, but model instances are not cached.

**Firebase SDK Binary Size:**
- Problem: The `Assets/Firebase/` directory is 366MB, largely due to native binaries (`FirebaseCppApp-13_7_0.so`, `.dll`, `.bundle`) and the m2repository for Android.
- Files: `Assets/Firebase/Plugins/x86_64/`, `Assets/Firebase/m2repository/`
- Cause: Firebase SDK ships native libraries for all supported platforms (iOS, Android/x86_64, macOS, desktop).
- Improvement path: Strip unused platform plugins before building. If targeting only desktop/PC initially, remove iOS and tvOS plugins. Use Git LFS for binary assets.

## Fragile Areas

**Firebase AI Logic SDK is in Public Preview:**
- Files: `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs:37-40`
- Why fragile: The SDK documentation explicitly states: "For Firebase AI, Live Model is in Public Preview, which means that the feature is not subject to any SLA or deprecation policy and could change in backwards-incompatible ways." The entire Gemini Live integration is built on a preview API.
- Safe modification: Wrap all Firebase AI calls behind project-owned interfaces/abstractions so that SDK API changes only require updating the wrapper layer, not all game code.
- Test coverage: No tests exist in the project.

**Reflection-Based Firebase Interop:**
- Files: `Assets/Firebase/FirebaseAI/Internal/FirebaseInterops.cs:28-58`
- Why fragile: The SDK uses runtime reflection to access `FirebaseApp.IsDataCollectionDefaultEnabled`, `Firebase.VersionInfo.SdkVersion`, `FirebaseAppCheck`, and `FirebaseAuth`. Any change to these types' APIs in future Firebase SDK versions will silently break at runtime without compile-time errors.
- Safe modification: This is vendor code. When updating the Firebase SDK, verify that reflected types and properties still exist. Debug logging is gated behind `FIREBASEAI_DEBUG_LOGGING` preprocessor define -- enable this during development to catch reflection failures.
- Test coverage: No tests exist.

**LiveGenerativeModel Setup Message Hardcodes VertexAI Path:**
- Files: `Assets/Firebase/FirebaseAI/LiveGenerativeModel.cs:154`
- Why fragile: The `ConnectAsync` method's setup message hardcodes the VertexAI-style model path (`projects/{id}/locations/{location}/publishers/google/models/{name}`) regardless of whether the GoogleAI or VertexAI backend is selected. The `GetURL()` method correctly switches between backends, but the setup payload always uses the VertexAI format.
- Safe modification: This is vendor code. If using GoogleAI backend for Live sessions, this may cause unexpected behavior. Use VertexAI backend for Live sessions until this is confirmed fixed.
- Test coverage: No tests exist.

**WebSocket State Not Guarded in Dispose:**
- Files: `Assets/Firebase/FirebaseAI/LiveSession.cs:64-67`
- Why fragile: The `Dispose(bool)` method calls `CloseAsync` without awaiting it (fire-and-forget). If the WebSocket is in a transitional state (Connecting, CloseSent), the close attempt may throw or be silently ignored. The `CloseAsync` call is also not wrapped in a try-catch.
- Safe modification: In the project's session management code, always explicitly call `CloseAsync` and await it before disposing the `LiveSession`. Do not rely on `Dispose()` for clean shutdown.
- Test coverage: No tests exist.

## Scaling Limits

**Gemini Live API Quotas:**
- Current capacity: Firebase AI Logic (Public Preview) has per-project rate limits that are not documented in the SDK.
- Limit: The default timeout is 180 seconds per request (`RequestOptions.DefaultTimeout` in `Assets/Firebase/FirebaseAI/RequestOptions.cs:35`). WebSocket sessions have no built-in keep-alive or reconnection logic.
- Scaling path: Implement session reconnection logic in the project's wrapper. Monitor Firebase Console for quota usage. Request quota increases if needed.

**Chat History Grows Unbounded:**
- Current capacity: The `Chat` class stores all conversation history in memory as `List<ModelContent>`.
- Files: `Assets/Firebase/FirebaseAI/Chat.cs:33`
- Limit: For long conversations with audio/inline data, the history list will grow without bound, consuming increasing memory and sending progressively larger payloads to the API.
- Scaling path: Implement a sliding window or summarization strategy in the project's chat wrapper. Use `LiveSession` for real-time interaction instead of `Chat` for the embodiment use case, as `LiveSession` manages context on the server side.

## Dependencies at Risk

**Firebase AI Logic SDK (Public Preview):**
- Risk: The entire SDK under `Assets/Firebase/FirebaseAI/` is marked as Public Preview. It can have breaking changes at any time without deprecation notice.
- Impact: Any SDK update could break the project's Gemini Live integration.
- Migration plan: Abstract all SDK usage behind project-owned interfaces. Pin the SDK version (currently using Firebase App Unity 13.7.0 based on `Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.7.0/`). Only upgrade after testing the new version against the project's integration layer.

**Google.MiniJSON:**
- Risk: The SDK uses `Google.MiniJSON` for all JSON serialization/deserialization. This is a lightweight JSON parser that may not handle all edge cases (large numbers, deeply nested objects, special characters).
- Files: `Assets/Firebase/Plugins/Google.MiniJson.dll`
- Impact: JSON parsing failures in API responses could cause silent data loss (the SDK returns `null` for unparseable responses in several places).
- Migration plan: This is an internal SDK dependency. For project-authored code, use `UnityEngine.JsonUtility` or `Newtonsoft.Json` (via com.unity.nuget.newtonsoft-json) for any custom serialization needs.

## Missing Critical Features

**No Audio Capture Pipeline:**
- Problem: The project aims for Gemini Live integration (real-time audio conversation with an AI character), but no microphone capture, audio processing, or playback system exists.
- Blocks: Cannot send audio to Gemini Live or play back AI-generated audio responses.

**No Character/Avatar System:**
- Problem: The project name "AI-Embodiment" implies a visual character/avatar that the AI controls or animates. No character model, animation system, or embodiment logic exists.
- Blocks: Cannot visualize AI responses as character behavior.

**No Game Architecture:**
- Problem: No MonoBehaviour scripts, ScriptableObjects, or Unity systems are implemented. The project has only the default URP sample scene.
- Blocks: Cannot run any game logic. Everything from scene setup to game loop must be created.

**No Error Handling or Retry Logic:**
- Problem: The Firebase SDK's error handling is minimal (throws `HttpRequestException` or `InvalidOperationException`). No project-level error handling, retry logic, or graceful degradation exists.
- Blocks: Network interruptions, API failures, or WebSocket disconnects will crash or hang the application.

## Test Coverage Gaps

**Zero Test Coverage:**
- What's not tested: Everything. The project has no custom code and no test files. The Unity Test Framework package (`com.unity.test-framework` 1.6.0) is installed but no test assemblies or test scripts exist.
- Files: No test files found in the project.
- Risk: All future code will be untested unless a testing strategy is established early.
- Priority: High -- establish test infrastructure (assembly definitions for tests, test runner configuration) before writing game logic. The Firebase AI SDK's async/WebSocket nature makes integration testing particularly important.

---

*Concerns audit: 2026-02-05*
