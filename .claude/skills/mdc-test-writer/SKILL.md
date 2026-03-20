---
name: mdc-test-writer
description: >
  Test generation skill for the Meridian project. Use this skill whenever an agent
  needs to write new xUnit tests, expand coverage for existing components, or validate that
  test quality meets the project's standards. Triggers on: "write tests for", "add unit tests",
  "increase test coverage", "write a test for this class", "how do I test X", "the tests are
  missing for", or when reviewing code that lacks corresponding test coverage. Also triggers
  when a code review (mdc-code-review) has identified test gaps. This skill produces
  idiomatic xUnit + FluentAssertions tests with correct async patterns, isolation, naming
  conventions, and mock setup for all major MDC component types: providers, storage sinks,
  pipeline components, WPF services, and F# interop boundaries.
---

# Meridian — Test Writer Skill

Generate high-quality, idiomatic xUnit tests for any Meridian component. Every test
produced by this skill must pass the `mdc-code-review` Lens 4 (Test Code Quality) checks without
warnings.

> **Shared project context:** [`../_shared/project-context.md`](../_shared/project-context.md)
> **Test patterns reference:** [`references/test-patterns.md`](references/test-patterns.md)
> **Code review skill:** [`../mdc-code-review/SKILL.md`](../mdc-code-review/SKILL.md)

---

## Integration Pattern

Every test-writing task follows this 4-step workflow:

### 1 — GATHER CONTEXT (MCP)
- Read the source file(s) under test to understand the component's contract and dependencies
- If available, read the issue/PR/review note that identified the gap
- Check the target test project's `.csproj` to confirm which mock library and test packages are in use

### 2 — ANALYZE & PLAN (Agents)
- Detect the component type using the Step 0 decision tree
- Select the correct pattern (A–H) and target test project
- Plan the minimum required test cases: happy path, error path, cancellation path, boundary, disposal

### 3 — EXECUTE (Skills + Manual)
- Apply all 7 universal quality rules and the selected pattern
- Write the complete, compilable test file with `CreateSut()`, proper `await using`, and timeout tokens
- Run through the Lens 4 validation checklist before finalizing

### 4 — COMPLETE (MCP)
- Run the most relevant `dotnet test` command(s) for the touched project
- Commit the test file to the appropriate test project subdirectory
- Summarize which scenarios are covered, which are intentionally deferred, and any environment-specific test limits

---

## Test Framework Stack

| Tool | Purpose |
|------|---------|
| **xUnit** | Test runner — all test projects |
| **FluentAssertions** | Assertion library — preferred over `Assert.*` |
| **Moq** | Common mocking library — used in `Meridian.Tests`, `Meridian.McpServer.Tests`, `Meridian.Ui.Tests`, and `Meridian.Wpf.Tests` |
| **NSubstitute** | Also available in `Meridian.Tests` and `Meridian.Wpf.Tests`; use only when the target project or nearby tests already favor it |
| **coverlet** | Code coverage — collected via `dotnet test --collect:"XPlat Code Coverage"` |

Always check the target test project's `.csproj` and nearby tests before writing mocks; this repo now has 6 test projects with slightly different conventions.

---

## Step 0: Component Type Detection

Before writing any code, identify the component type using the decision tree in
`references/test-patterns.md`. The component type determines:

1. Which test project to target
2. Which subdirectory to use
3. Which pattern (A–H) to follow
4. Whether the project convention is Moq, NSubstitute, no mocks, or F#-native helpers
5. Whether `IDisposable` / `IAsyncDisposable` cleanup is needed

---

## Step 1: Apply Universal Quality Rules

These 7 rules apply to **every** test, regardless of component type:

1. **Never `async void`** — always `async Task`
2. **CancellationToken with timeout** — `new CancellationTokenSource(TimeSpan.FromSeconds(5))`
3. **`await using` for `IAsyncDisposable`** — never `using` for async-disposable types
4. **No `Task.Delay` for synchronization** — use `TaskCompletionSource` or `SemaphoreSlim`
5. **Naming: `MethodUnderTest_Scenario_ExpectedBehavior`**
6. **No shared static mutable state** — each test method creates its own SUT
7. **File isolation for storage tests** — temp directory, `Dispose()` cleans it up

---

## Step 2: Select the Right Pattern

| Component | Pattern | Key Concerns |
|-----------|---------|-------------|
| `IHistoricalDataProvider` impl | A | HTTP errors, rate limit, cancellation, empty |
| `IMarketDataClient` impl | B | Connect/disconnect, reconnect, dispose |
| `IStorageSink` / WAL | C | Temp dir, FlushAsync, DisposeAsync, line count |
| `EventPipeline` | D | FlushAsync before assert, DisposeAsync flushes |
| Application service (pure) | E | `[Theory]` for multiple inputs, `[InlineData]` |
| WPF / Ui.Services | F | API mock (Moq or NSubstitute based on local convention), null on error |
| F# modules | G | F# test module style, `Result` type assertions |
| Endpoint / MCP integration | H | `WebApplicationFactory`, JSON contract snapshots, or MCP tool harnesses |

Full scaffolding for each pattern is in `references/test-patterns.md`.

---

## Step 3: Write the Test File

### Minimum Test Coverage Requirements

For any non-trivial component, a test file must cover at minimum:

- **Happy path** — valid input returns expected output
- **Error path** — invalid input or downstream failure throws correct exception type
- **Cancellation path** — `OperationCanceledException` propagates when token is cancelled
- **Boundary conditions** — null/empty/whitespace input where relevant
- **Disposal/cleanup** — `DisposeAsync` or `Dispose` completes without hanging

For storage sinks additionally:
- **Flush semantics** — data written without explicit flush is still persisted after `DisposeAsync`

For streaming providers additionally:
- **Reconnection** — a disconnect triggers reconnect, not silent data loss

### Test Output Format

Produce a complete, compilable test file with:

1. Namespace matching the project convention (`Meridian.Tests.{Category}`)
2. `using` directives (xUnit, FluentAssertions, Moq or NSubstitute, and types under test)
3. A `CreateSut()` factory method (not scattered construction in each test method)
4. `IDisposable` or `IAsyncDisposable` implementation when temp resources are needed
5. All test methods returning `Task` (never `void`)
6. CancellationToken with 5-second timeout on every async test

---

## Step 4: Validate Before Submitting

Run through the `mdc-code-review` Lens 4 checklist mentally before finalizing:

- [ ] No `async void` test methods
- [ ] No shared static mutable state
- [ ] No `Task.Delay` for timing
- [ ] All names follow `MethodUnderTest_Scenario_ExpectedBehavior`
- [ ] Every `IAsyncDisposable` test subject uses `await using`
- [ ] Every async test has a `CancellationToken` with a timeout
- [ ] Storage tests clean up temp directories in `Dispose()`
- [ ] At least one test for the cancellation path
- [ ] At least one test for the error/exception path

---

## Quick Reference: Common Assertions

```csharp
// Value equality
result.Should().Be(expected);
result.Should().BeEquivalentTo(expectedObject);

// Collection
items.Should().HaveCount(3);
items.Should().ContainSingle(x => x.Symbol == "AAPL");
items.Should().BeEmpty();
items.Should().NotBeEmpty();
items.Should().NotContain(x => x.Price < 0);

// Strings
str.Should().Be("expected");
str.Should().Contain("substring");
str.Should().StartWith("prefix");
str.Should().NotBeNullOrWhiteSpace();

// Exceptions
var act = () => sut.Method(input);
await act.Should().ThrowAsync<DataProviderException>();
await act.Should().ThrowAsync<DataProviderException>().WithMessage("*symbol*");
await act.Should().NotThrowAsync();

// Null
result.Should().NotBeNull();
result.Should().BeNull();

// Boolean
condition.Should().BeTrue("because ...");
condition.Should().BeFalse();

// Verify mock was called
mock.Verify(m => m.Method(It.IsAny<string>()), Times.Once);
mock.Verify(m => m.Method(It.IsAny<string>()), Times.Never);
```

---

## Quick Reference: Common Mock Setups (Moq)

```csharp
// Setup return value
mock.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedValue);

// Setup exception
mock.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
    .ThrowsAsync(new HttpRequestException("error"));

// Setup callback
mock.Setup(m => m.WriteAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
    .Callback<MarketEvent, CancellationToken>((evt, _) => captured.Add(evt))
    .Returns(ValueTask.CompletedTask);

// Setup property
mock.Setup(m => m.CurrentValue).Returns(new MyOptions { ... });

// Verify
mock.Verify(m => m.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);
mock.VerifyNoOtherCalls();  // ensure no unexpected calls
```

---

## Quick Reference: Common Mock Setups (NSubstitute)

```csharp
// Setup return value
sub.GetAsync(Arg.Any<CancellationToken>()).Returns(expectedValue);

// Setup exception
sub.GetAsync(Arg.Any<CancellationToken>()).Throws(new HttpRequestException("error"));

// Verify
sub.Received(1).FlushAsync(Arg.Any<CancellationToken>());
sub.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
```

---

## Common Testing Anti-Patterns to Avoid

| Anti-Pattern | Symptom | Fix |
|-------------|---------|-----|
| `async void` test | Exceptions silently swallowed; test passes on failure | Change to `async Task` |
| `Task.Delay(200)` for synchronization | Flaky tests; CI timing sensitivity | Use `TaskCompletionSource` |
| No CancellationToken timeout | Test hangs indefinitely if SUT blocks | Add `new CancellationTokenSource(TimeSpan.FromSeconds(5))` |
| `Assert.True(result != null)` | Meaningless assertion, no context on failure | `result.Should().NotBeNull()` |
| Shared static `_sut` field | Test isolation violated; state leaks | Create SUT in `CreateSut()` per-test |
| `using var sink = new JsonlStorageSink(...)` | File handles leaked | `await using var sink = ...` |
| No temp dir cleanup | CI disk fills up; cross-test pollution | Implement `IDisposable` with `Directory.Delete` |
| Copy-paste test names | `Test1`, `Test2` — unintelligible | Follow `Method_Scenario_Expected` convention |
