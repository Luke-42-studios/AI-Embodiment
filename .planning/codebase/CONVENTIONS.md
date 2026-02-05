# Coding Conventions

**Analysis Date:** 2026-02-05

## Project Context

This project contains two distinct code layers with different convention origins:

1. **Firebase AI Logic SDK** (Google, third-party) -- `Assets/Firebase/` -- follows Google C# style
2. **Unity Tutorial/Editor scripts** (Unity template) -- `Assets/TutorialInfo/` -- follows Unity conventions

All game-specific scripts should follow the Firebase AI SDK conventions since that is the dominant codebase and the primary integration surface.

## Naming Patterns

**Files:**
- Use PascalCase matching the primary class name: `GenerativeModel.cs`, `LiveSession.cs`
- One primary public type per file
- Internal helper classes can be in the same file as their related public type (see `ModelContent.cs` containing `ModelContentJsonParsers` in `Internal` namespace)

**Classes:**
- PascalCase: `GenerativeModel`, `LiveSession`, `FirebaseAI`
- Prefix `I` for interfaces: `ILiveSessionMessage`
- `readonly struct` for immutable data types: `GenerateContentResponse`, `Candidate`, `SafetySetting`
- `class` for stateful objects with lifecycle: `GenerativeModel`, `Chat`, `LiveSession`

**Methods:**
- PascalCase: `GenerateContentAsync`, `SendMessageAsync`, `StartChat`
- Suffix `Async` on all async methods
- Internal methods prefixed with `Internal`: `GenerateContentAsyncInternal`, `InternalCreateChat`
- Factory methods use `From` prefix for deserialization: `FromJson`
- Static factory methods for creation: `Backend.GoogleAI()`, `Schema.Object()`

**Properties:**
- PascalCase: `Text`, `Candidates`, `PromptFeedback`
- Read-only backing fields use underscore prefix: `_candidates`, `_firebaseApp`
- Expose `IReadOnlyList<T>` for collection properties, never raw `List<T>`

**Fields:**
- Private fields use underscore prefix with camelCase: `_httpClient`, `_modelName`, `_backend`
- Constants use `k_` prefix for Unity code (`k_Space`) or PascalCase for Firebase code (`StreamPrefix`)
- Static fields use `s_` prefix in Unity code (`s_ShowedReadmeSessionStateName`)
- Private constants use camelCase with descriptive names: `appCheckHeader`, `authHeader`

**Enums:**
- PascalCase for enum types and values: `FinishReason.MaxTokens`, `HarmCategory.Harassment`
- Use `Unknown = 0` as the default/fallback value for all enums parsed from JSON

**Namespaces:**
- Root namespace: `Firebase.AI`
- Internal namespace: `Firebase.AI.Internal`
- Do NOT use `UnityEngine` namespace for game scripts; keep AI logic namespace-isolated

## Code Style

**Formatting:**
- No `.editorconfig` or dedicated formatting tool detected
- 2-space indentation for Firebase AI SDK files
- 4-space indentation for Unity template scripts (`Assets/TutorialInfo/`)
- Use 2-space indentation for new code to match the dominant Firebase AI SDK style

**Braces:**
- Allman style (opening brace on new line) for class/method declarations
- Allman style for control flow blocks
- Exception: single-line `if` with `HasValue` check can omit braces:
  ```csharp
  if (_temperature.HasValue) jsonDict["temperature"] = _temperature.Value;
  ```

**Linting:**
- No linting tools configured
- Rely on C# compiler warnings and Unity's built-in analysis

## Import Organization

**Order:**
1. `System.*` namespaces (alphabetical)
2. Third-party namespaces (`Google.MiniJSON`)
3. Project namespaces (`Firebase.AI.Internal`)

**Example from `GenerativeModel.cs`:**
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.MiniJSON;
using Firebase.AI.Internal;
```

**Path Aliases:**
- Type alias used for Dictionary shorthand: `using JsonDict = Dictionary<string, object>;` (in `InternalHelpers.cs`)

## Error Handling

**Patterns:**

1. **Argument validation -- throw immediately:**
   ```csharp
   if (app == null)
   {
     throw new ArgumentNullException(nameof(app));
   }
   ```

2. **HTTP errors -- custom validation with detail extraction:**
   ```csharp
   // In HttpHelpers.ValidateHttpResponse
   var ex = new HttpRequestException(
     $"HTTP request failed with status code: {(int)response.StatusCode} ({response.ReasonPhrase}).\n" +
     $"Error Content: {errorContent}"
   );
   throw ex;
   ```

3. **Unsupported backend -- NotSupportedException:**
   ```csharp
   throw new NotSupportedException($"Missing support for backend: {_backend.Provider}");
   ```

4. **JSON parsing -- controlled exception behavior via `JsonParseOptions` flags:**
   ```csharp
   jsonDict.ParseValue<string>("name", JsonParseOptions.ThrowEverything)  // Required
   jsonDict.ParseValue<string>("title")                                    // Optional, returns default
   ```

5. **Reflection failures -- log and degrade gracefully:**
   ```csharp
   // In FirebaseInterops, when Firebase Auth/AppCheck aren't available
   _appCheckReflectionInitialized = false;
   return null;  // Caller checks for null
   ```

6. **WebSocket state checks -- InvalidOperationException:**
   ```csharp
   if (_clientWebSocket.State != WebSocketState.Open)
   {
     throw new InvalidOperationException("WebSocket is not open, cannot send message.");
   }
   ```

**Convention:** Let HTTP/async exceptions propagate to the caller. Document throws with `<exception cref="">` XML doc tags. Never swallow exceptions silently except in reflection-based optional integrations.

## Logging

**Framework:** Unity `Debug.Log` behind conditional compilation

**Patterns:**
- Use `FIREBASE_LOG_REST_CALLS` for HTTP request/response logging:
  ```csharp
  #if FIREBASE_LOG_REST_CALLS
  UnityEngine.Debug.Log("Request:\n" + bodyJson);
  #endif
  ```
- Use `FIREBASEAI_DEBUG_LOGGING` for internal debug messages:
  ```csharp
  #if FIREBASEAI_DEBUG_LOGGING
  UnityEngine.Debug.LogWarning($"Received unknown part, with keys: {string.Join(',', jsonDict.Keys)}");
  #endif
  ```
- Never log unconditionally in library code; always gate behind `#if`

## Comments

**When to Comment:**
- Comment the "why" not the "what": `// MiniJson puts all ints as longs, so special case it`
- Mark internal-only APIs: `// Hidden constructor, users don't need to make this.`
- Use `// Note:` for important behavioral context: `// Note: No public constructor, get one through GenerativeModel.StartChat`

**XML Documentation:**
- All public types and members have `<summary>` XML doc comments
- Use `<param>` tags on all public method parameters
- Use `<returns>` for non-void methods
- Use `<exception cref="">` for documented throws
- Mark internal APIs with `/// Intended for internal use only.`
- Use `<remarks>` for deprecation notes
- Use `[Obsolete("...")]` attribute for deprecated members

**TODOs:**
- Format: `// TODO: Description` -- 5 active TODOs in the Firebase AI SDK

## Function Design

**Size:** Single responsibility. Most methods are 5-30 lines. Internal async methods handle the actual logic while public methods are thin wrappers providing overloads.

**Overload Pattern:** Public API methods provide multiple overloads for convenience:
```csharp
// String overload
public Task<GenerateContentResponse> GenerateContentAsync(string text, ...)
// Single content overload
public Task<GenerateContentResponse> GenerateContentAsync(ModelContent content, ...)
// Collection overload (actual implementation)
public Task<GenerateContentResponse> GenerateContentAsync(IEnumerable<ModelContent> content, ...)
```

**Parameters:**
- Use nullable value types for optional config: `float? temperature = null`
- Use `CancellationToken cancellationToken = default` as the last parameter on all async methods
- Use `params` for variadic convenience: `public Chat StartChat(params ModelContent[] history)`

**Return Values:**
- Async methods return `Task<T>` or `IAsyncEnumerable<T>`
- Use `readonly struct` for response types (value semantics, no allocation)
- Collection properties return `IReadOnlyList<T>`, never `List<T>`
- Nullable struct properties for optional data: `FinishReason?`, `PromptFeedback?`

## Module Design

**Exports:**
- Public classes use `internal` constructors; creation via factory methods or parent classes
- `internal` access modifier for all serialization/deserialization methods (`ToJson`, `FromJson`)
- `#region` directives used to organize logical sections: `#region Public API`, `#region Parts`, `#region Helper Factories`

**Barrel Files:**
- No barrel files used; Unity's compilation model handles assembly resolution
- No `.asmdef` files for project scripts (all compile into `Assembly-CSharp`)

## Serialization Pattern

**JSON Serialization:**
- Use `Google.MiniJSON` (Json.Serialize/Json.Deserialize)
- All types implement `internal Dictionary<string, object> ToJson()` for serialization
- All response types implement `internal static T FromJson(Dictionary<string, object>)` for deserialization
- Use extension methods from `FirebaseAIExtensions` for safe parsing: `ParseValue`, `ParseEnum`, `ParseObjectList`, `ParseNullableObject`

**Example pattern:**
```csharp
// Serialization
internal Dictionary<string, object> ToJson()
{
  Dictionary<string, object> jsonDict = new();
  if (_temperature.HasValue) jsonDict["temperature"] = _temperature.Value;
  // ... conditionally add fields
  return jsonDict;
}

// Deserialization
internal static MyType FromJson(Dictionary<string, object> jsonDict)
{
  return new MyType(
    jsonDict.ParseValue<int>("requiredField", JsonParseOptions.ThrowEverything),
    jsonDict.ParseValue<string>("optionalField")
  );
}
```

## Type Design Patterns

**Immutable Value Types:**
- Use `readonly struct` for all data transfer objects and configuration types
- Private backing fields for collections, with null-coalescing getters:
  ```csharp
  private readonly IReadOnlyList<Candidate> _candidates;
  public IReadOnlyList<Candidate> Candidates
  {
    get { return _candidates ?? new List<Candidate>(); }
  }
  ```

**Singleton/Instance Pattern:**
- `FirebaseAI` uses `ConcurrentDictionary` for thread-safe instance caching
- Keyed by combination of app name and backend config

**Dispose Pattern:**
- `LiveSession` implements `IDisposable` with the full dispose pattern:
  ```csharp
  protected virtual void Dispose(bool disposing) { ... }
  public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
  ~LiveSession() { Dispose(false); }
  ```

**Concurrency:**
- Use `SemaphoreSlim` for WebSocket send locking
- Use `CancellationToken` throughout all async APIs
- Use `CancellationTokenSource.CreateLinkedTokenSource` for timeouts

---

*Convention analysis: 2026-02-05*
