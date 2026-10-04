# Structured output mutation audit

This is a focused audit of handwritten `StructuredOutputs/**`, `Models/CodexAskForApproval.cs`, `Exec/EventStreamOptions.cs`, and `Exec/SessionFilter.cs`. It is not a whole-SDK mutation result. The generated protocol files are not in this scope. No mutant exclusions, suppression attributes, or reflection-based tests were added.

The behavioral tests cover valid and malformed output, retry exhaustion, custom retry context, cancellation, live-session completion and reader disposal, historical EOF, resume offsets, prompt-argument mode, serialization options, inherited interfaces, nullable schema annotations, and composed inline schemas. The tests exposed real bugs in granular-policy parsing, constructor validation, prompt-mode preservation, and nested schema normalization. A follow-up review also corrected an intermediate-state validation regression in record updates.

## Measurement

The successive focused scores were 53.91%, 68.35%, 83.76%, and **84.71%**. The final report is `/tmp/stryker-outputs-round3/reports/mutation-report.json`, measured at commit `186bd0d`: 390 killed, 9 timeout, 55 survived, 17 uncovered, 71 compiler-rejected, and 127 automatically block-filtered mutants. The whole local Release suite passed 1,123 tests with 15 external-runtime tests skipped. The final measurement below uses Stryker.NET 5.0.0, Release/net10.0, `perTest` coverage analysis, two workers, the unit-test filter, and 3000ms additional timeout. The scoped `mutate` list is the four paths above. Use the checked-in .NET tool manifest, run from `tests/JKToolKit.CodexSDK.Tests`, and pass these settings in a `stryker-config` JSON object. The build and test project were not modified during measurement.

A score below 100% is retained. Compile errors and Stryker's automatic block-already-covered filtering are reported separately; they are not test kills. The all-assembly compiler rollback count also includes files outside this focused mutate list and must not be presented as this scope's count.

## Interpretation

`ConfigureAwait(false)` to `true` mutants remain environment-sensitive. Tests do not assert the internal await flag merely to improve the score. These mutations can affect a caller with a synchronization context, so they are not claimed universally equivalent.

The extractor has several behaviorally equivalent mutants under its public contract: it parses an already-validated candidate twice; failed scan out-values are discarded; removing some `continue` statements falls through only mutually exclusive delimiter checks; index searches cannot return zero because their starting offsets are positive. The failed scanner's result still has to pass JSON parsing. Assertions about private iteration counts would add no user-visible protection.

Retry end-of-loop fallbacks duplicate the final-attempt throw. Removing that throw or changing `>=` to `>` reaches an exception with the same public result/error data, although the throw location in the stack trace changes. At `MaxAttempts == Int32.MaxValue`, allowing the final increment can also overflow the loop counter, so these are bounded-run equivalences, not universal equivalences for every integer input. Nullable context fallbacks are unreachable after a normal parse failure has populated the context. Public retry preparation requires a positive attempt count.

The app-server terminal-state mutants rely on the real `CodexTurnHandle` contract: completion closes event channels and rejects later observations. Tests do not manufacture impossible post-completion channel data. File-length fallback mutants return the same value with stable filesystem state: a missing file either fails the existence check or throws when reading `Length`, and both paths return zero. Concurrent file creation can distinguish those paths, so this is a conditional equivalence rather than a claim about every possible race.

The approval union's private invalid-constructor states cannot be produced by its public factories; its failed-boolean out-value is discarded. The schema generator receives `typeof(T)` and the non-null generated schema from its private caller chain. Reflection to bypass those invariants is not meaningful behavioral coverage.

One explicit remaining extension-injection gap is the retry runner's missing-session guard: a custom `ICodexClient` can throw `CodexStructuredOutputParseException` during `StartSessionAsync`, before returning a handle. Ordinary SDK startup does not throw that high-level parsing exception. The branch remains reported as uncovered, rather than being called unreachable.

The schema normalizer is not dead code. Public NJsonSchema annotations and schema processors exercise legacy nullable markers, multi-type unions, inherited interface properties, property-only schemas, and nullable composition. In particular, the composed-schema regression failed because an early return left inline child `nullable` markers untouched; traversing the wrapper fixes that output.

## Compiler limitations

The JSON report records `CompileError` without a specific diagnostic for each mutant. Representative invalid mutant shapes include removing required object initializers, negating pattern-variable declarations and then using the undeclared/unassigned binding, replacing `StringBuilder.Append` with a nonexistent `Prepend`, subtracting strings, and creating contradictory character patterns. These are tool-generated failures, not failing source builds. The console log additionally records Stryker safe-mode method rollback for `CS0165` unassigned pattern variables in unrelated application-server parser methods; those rollbacks belong to the whole instrumented assembly, not this focused scope.

After adding the open-ended range follow-up, the complete local Release suite passed 1,124 tests with the same 15 skips. The exact remaining IDs and compiler-rejected IDs are recorded below so results can be checked against the report without treating every survivor as an equivalent mutant.

## Surviving mutant ledger

IDs are from the final report above; do not match them to a report from a different source revision.

| IDs | Assessment |
| --- | --- |
| 11605 | Real missing case: an open-ended date range must remain valid. A follow-up test covers both half-open constructors and listing consumption. Applying this exact `&&` to `||` mutant manually makes that test fail with `InvalidOperationException`; the restored source passes. The historical score above is not recomputed to count that follow-up as a Stryker kill. |
| 16339 | Equivalent: a failed boolean parse out-value is discarded by callers. |
| 16478, 16484 | Private-call invariants: `typeof(T)` and generated schema cannot be null through the public entry point. |
| 16500 | Generator-shape invariant: standard serialization emits a non-null `type` or omits the key; explicit `type: null` is not valid JSON Schema. No private-node injection test added. |
| 16607 | Equivalent: candidate was already parsed successfully before the second validation. |
| 16610, 16656, 16675, 16676 | Equivalent: initial out-values are replaced on success or discarded on failure. |
| 16621, 16630, 16632 | Equivalent: search offsets and newline delimiters make the changed boundary indistinguishable. |
| 16664, 16667, 16695, 16703, 16708, 16718 | Equivalent: fallthrough cannot produce a different valid JSON candidate; final JSON validation rejects failed/empty scans. |
| 16839, 16842, 17031, 17034 | Duplicate final-failure path; same public error data for bounded runs, subject to the stack-location and Int32 overflow caveats above. |
| 16856 | Equivalent: truncating a string at its exact length preserves its content. |
| 16879, 16882 | Equivalent under the turn-completion contract; the event channel closes after the terminal notification. |
| 16972, 16974 | Equivalent for stable filesystem state; preserve the concurrent-creation caveat above. |
| 16730, 16731, 16745, 16747, 16749, 16763, 16764, 16770, 16774, 16786, 16797, 16825, 16826, 16834, 16835 | `CodexStructuredOutputExtensions.cs`: await-context mutations, retained as environment-sensitive. |
| 16869 | `StructuredOutputAppServerCapture.cs`: await-context mutations, retained as environment-sensitive. |
| 16911, 16915, 16926, 16928, 16930, 16932 | `StructuredOutputExecCapture.cs`: await-context mutations, retained as environment-sensitive. |
| 17000, 17002, 17022, 17023, 17026 | `StructuredOutputRetryRunner.cs`: await-context mutations, retained as environment-sensitive. |

## Uncovered mutant ledger

| IDs | Boundary |
| --- | --- |
| 16287, 16288 | Private approval constructor rejection for invalid both/neither union states; public factories enforce the union. |
| 16485 | Generated-schema JSON parser returning a null root; schema serialization produces an object. |
| 16831, 16833, 17016, 17018 | Retry-context defaults after a preceding failure; the preceding catch populates the parse exception. |
| 16843, 16844, 16845, 17036, 17037, 17038, 17039 | Final defensive fallback after a positive bounded retry loop; normal final failure throws inside the loop. |
| 16854 | Null exception fallback in internally constructed retry context; the context always receives an exception. |
| 17006, 17007 | Remaining custom-client startup parse-exception injection gap described above. |

## Compiler-rejected IDs

| File | Count | IDs |
| --- | ---: | --- |
| `CodexAskForApproval.cs` | 23 | 16279, 16293, 16295, 16296, 16297, 16299, 16303, 16304, 16308, 16309, 16310, 16315, 16316, 16317, 16318, 16319, 16321, 16322, 16323, 16324, 16326, 16341, 16342 |
| `CodexJsonSchemaGenerator.cs` | 24 | 16489, 16491, 16493, 16494, 16496, 16515, 16516, 16517, 16520, 16524, 16525, 16526, 16533, 16541, 16547, 16548, 16550, 16561, 16562, 16563, 16566, 16569, 16570, 16571 |
| `CodexStructuredJsonExtractor.cs` | 1 | 16645 |
| `CodexStructuredOutputExtensions.cs` | 7 | 16732, 16746, 16750, 16765, 16827, 16828, 16836 |
| `CodexStructuredRetryOptions.cs` | 1 | 16863 |
| `StructuredOutputAppServerCapture.cs` | 4 | 16872, 16874, 16875, 16876 |
| `StructuredOutputExecCapture.cs` | 8 | 16933, 16935, 16936, 16937, 16939, 16940, 16941, 16942 |
| `StructuredOutputRetryRunner.cs` | 3 | 17003, 17013, 17027 |
