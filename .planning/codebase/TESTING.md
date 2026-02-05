# Testing Patterns

**Analysis Date:** 2026-02-05

## Test Framework

**Runner:**
- Unity Test Framework 1.6.0 (NUnit-based)
- Package: `com.unity.test-framework` in `Packages/manifest.json`
- No test assembly definitions (`.asmdef`) exist in the project `Assets/` directory

**Assertion Library:**
- NUnit (bundled with Unity Test Framework)

**Run Commands:**
```bash
# Via Unity Editor: Window > General > Test Runner
# Via command line (requires Unity CLI):
Unity -runTests -testPlatform EditMode -projectPath .
Unity -runTests -testPlatform PlayMode -projectPath .
```

## Test File Organization

**Location:**
- No test files exist in the project

**Current State:**
There are zero test files in this project. The Unity Test Framework package is installed as a dependency, but no test assemblies, test scripts, or test fixtures have been created.

**Naming (recommended based on Unity conventions):**
- `Assets/Tests/EditMode/` for edit-mode tests
- `Assets/Tests/PlayMode/` for play-mode tests
- Files: `{ClassName}Tests.cs`

**Structure (recommended):**
```
Assets/
  Tests/
    EditMode/
      EditMode.asmdef          # Assembly definition referencing test framework
      FirebaseAITests.cs       # Unit tests for Firebase AI SDK integration
    PlayMode/
      PlayMode.asmdef          # Assembly definition referencing test framework
      LiveSessionTests.cs      # Integration tests requiring runtime
```

## Test Structure

**Recommended Suite Organization (Unity/NUnit pattern):**
```csharp
using NUnit.Framework;

[TestFixture]
public class GenerativeModelTests
{
    private GenerativeModel _model;

    [SetUp]
    public void SetUp()
    {
        // Initialize test fixtures
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up
    }

    [Test]
    public void GenerateContentAsync_WithValidText_ReturnsResponse()
    {
        // Arrange
        // Act
        // Assert
    }

    [Test]
    public void GenerateContentAsync_WithNullContent_ThrowsException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ...);
    }
}
```

**Patterns:**
- Use `[SetUp]` / `[TearDown]` for per-test initialization
- Use `[OneTimeSetUp]` / `[OneTimeTearDown]` for shared fixtures
- Use `[UnityTest]` for coroutine-based play mode tests

## Mocking

**Framework:** Not configured

**Recommended Approach:**
Since this project heavily relies on HTTP calls (`HttpClient`) and WebSocket connections (`ClientWebSocket`), testing should use:

1. **Interface-based mocking** -- Extract interfaces for `HttpClient` and `ClientWebSocket` wrappers
2. **Custom test doubles** -- The Firebase SDK uses `internal` constructors, so test helpers would need `[assembly: InternalsVisibleTo("Tests")]`

**What to Mock:**
- `HttpClient` responses (for `GenerativeModel`, `ImagenModel`, `TemplateGenerativeModel`)
- `ClientWebSocket` connections (for `LiveSession`, `LiveGenerativeModel`)
- `FirebaseApp` initialization and configuration
- `FirebaseInterops` reflection-based token retrieval

**What NOT to Mock:**
- JSON serialization/deserialization (`ToJson`/`FromJson` methods) -- test these with real data
- `FirebaseAIExtensions` helper methods -- test directly with dictionary inputs
- Value type constructors (`GenerationConfig`, `SafetySetting`, etc.)

## Fixtures and Factories

**Test Data (recommended pattern):**
```csharp
public static class TestData
{
    public static Dictionary<string, object> ValidGenerateContentResponse => new()
    {
        ["candidates"] = new List<object>
        {
            new Dictionary<string, object>
            {
                ["content"] = new Dictionary<string, object>
                {
                    ["role"] = "model",
                    ["parts"] = new List<object>
                    {
                        new Dictionary<string, object> { ["text"] = "Hello!" }
                    }
                },
                ["finishReason"] = "STOP"
            }
        }
    };
}
```

**Location (recommended):**
- `Assets/Tests/EditMode/TestData/` for JSON fixtures
- `Assets/Tests/EditMode/Helpers/` for test utility classes

## Coverage

**Requirements:** None enforced

**Current Coverage:** 0% -- no tests exist

**Priority Areas for Testing:**

1. **JSON deserialization** (`FromJson` methods) -- highest value, pure functions, easy to test
   - `Assets/Firebase/FirebaseAI/GenerateContentResponse.cs` -- `GenerateContentResponse.FromJson`
   - `Assets/Firebase/FirebaseAI/ModelContent.cs` -- `ModelContent.FromJson`, `PartFromJson`
   - `Assets/Firebase/FirebaseAI/Candidate.cs` -- `Candidate.FromJson`
   - `Assets/Firebase/FirebaseAI/LiveSessionResponse.cs` -- `LiveSessionResponse.FromJson`

2. **JSON serialization** (`ToJson` methods) -- verify request formatting
   - `Assets/Firebase/FirebaseAI/GenerationConfig.cs` -- `GenerationConfig.ToJson`
   - `Assets/Firebase/FirebaseAI/Safety.cs` -- `SafetySetting.ToJson`
   - `Assets/Firebase/FirebaseAI/FunctionCalling.cs` -- `Tool.ToJson`, `FunctionDeclaration.ToJson`
   - `Assets/Firebase/FirebaseAI/Schema.cs` -- `Schema.ToJson`

3. **Extension methods** -- `InternalHelpers.cs` parsing helpers
   - `Assets/Firebase/FirebaseAI/Internal/InternalHelpers.cs` -- `ParseValue`, `TryParseValue`, `ParseEnum`, `ParseObjectList`, etc.

4. **URL generation** -- verify backend-specific URLs
   - `Assets/Firebase/FirebaseAI/Internal/HttpHelpers.cs` -- `GetURL`, `GetTemplateURL`

5. **Enum conversion** -- verify string-to-enum and enum-to-string mappings
   - `Assets/Firebase/FirebaseAI/Internal/EnumConverters.cs`
   - Parse functions in `Candidate.cs`, `Safety.cs`, `GenerateContentResponse.cs`

## Test Types

**Unit Tests (EditMode):**
- Scope: JSON serialization/deserialization, enum parsing, URL generation, helper extensions
- No Unity lifecycle required
- Assembly definition needs reference to `Firebase.AI` namespace (requires `InternalsVisibleTo` for testing `internal` methods)

**Integration Tests (PlayMode):**
- Scope: Full request/response cycle with mocked HTTP, WebSocket session management
- Requires Unity runtime for `MonoBehaviour`-dependent features
- Would need mock Firebase project configuration (`google-services.json`)

**E2E Tests:**
- Not practical for automated testing due to Firebase project + API key requirements
- Use manual testing with real Firebase project and Gemini API

## Common Patterns

**Async Testing (NUnit with Unity):**
```csharp
[Test]
public async Task GenerateContentAsync_ReturnsValidResponse()
{
    // Arrange
    var response = GenerateContentResponse.FromJson(TestData.ValidResponse,
        FirebaseAI.Backend.InternalProvider.GoogleAI);

    // Assert
    Assert.IsNotNull(response.Candidates);
    Assert.AreEqual(1, response.Candidates.Count);
    Assert.AreEqual("Hello!", response.Text);
}
```

**Error Testing:**
```csharp
[Test]
public void FromJson_WithMissingRequiredField_ThrowsKeyNotFoundException()
{
    var invalidJson = new Dictionary<string, object>();

    Assert.Throws<KeyNotFoundException>(() =>
    {
        // ParseValue with ThrowEverything should throw on missing keys
        invalidJson.ParseValue<string>("required", JsonParseOptions.ThrowEverything);
    });
}
```

**Enum Parsing Testing:**
```csharp
[TestCase("STOP", FinishReason.Stop)]
[TestCase("MAX_TOKENS", FinishReason.MaxTokens)]
[TestCase("UNKNOWN_VALUE", FinishReason.Unknown)]
public void ParseFinishReason_ReturnsExpectedValue(string input, FinishReason expected)
{
    // Test via Candidate.FromJson with crafted dictionary
}
```

## Setup Requirements

**To add tests to this project:**

1. Create test assembly definitions:
   ```
   Assets/Tests/EditMode/EditMode.asmdef
   Assets/Tests/PlayMode/PlayMode.asmdef
   ```

2. The EditMode `.asmdef` should reference:
   ```json
   {
     "name": "EditMode",
     "references": ["Assembly-CSharp"],
     "includePlatforms": ["Editor"],
     "defineConstraints": ["UNITY_INCLUDE_TESTS"],
     "optionalUnityReferences": ["TestAssemblies"]
   }
   ```

3. Add `[assembly: InternalsVisibleTo("EditMode")]` to the Firebase AI SDK assembly if testing `internal` methods directly, or create public test helper wrappers.

4. Since Firebase AI SDK files are in `Assets/Firebase/` without their own `.asmdef`, they compile into `Assembly-CSharp` by default and are accessible from test assemblies that reference it.

---

*Testing analysis: 2026-02-05*
