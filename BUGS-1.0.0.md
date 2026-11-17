# csharp-openhab-test-suite 1.0.0 – findings and what 2.0.0 changes

Checked on 17 November 2026 against openHAB 5.2.3 and openhab-test-suite 22.0.2 (Python, the
reference), with the NuGet packages `CSharpOpenHABTestSuite` 1.0.0 and
`CSharpOpenHABRestClient` 1.0.0.

| Call (exercise configuration) | 1.0.0 | 2.0.0 |
|---|---|---|
| `TestSwitch("Wohnzimmer_Licht", "ON", "ON", 5)` | `true` after **11.2 s** | `true` at once |
| `TestDimmer("Wohnzimmer_Dimmer", "0", "0", 5)` | `true` after 10 s | `true` at once (`new[] {"0", "0.0"}` possible) |
| `TestSwitch("Werkstatt_Luefter", "ON", "ON", 3)` – no device answers | `false` after **10 s** | `false` after 3 s |
| `CheckItemIsType("Werkstatt_Temperatur", "Number:Temperature")` | `false` | `true` (base type and full type, Python 22.0.2) |
| `TestSwitch("TS_Undef", "ON", "ON", 5)` – item was `NULL` | item stays `ON` – `NULL` was sent as command and rejected | `NULL` restored as state |
| `RuleTester.IsRuleActive("gibtsnicht")` | **`true`** | `false` |
| `PersistenceTester.HasDataInRange(…)` after `StoreItemData` | `false` – the client 1.0.0 cannot store values (HTTP 400) | `true` in the range, `false` outside |

## Found in the code

| Finding | 1.0.0 | 2.0.0 |
|---|---|---|
| Order of stream and command | `ItemStateChangedEvent()` returns an `SSEConnection` that connects only in `ReadAll()` – *after* the command; a fast device changes the state before anybody listens | the stream is opened first, then the command is sent |
| Timeout | `CancellationTokenSource` ends `ReadAll()` with an `OperationCanceledException`, which jumps over the final state check; the time is only checked when a line arrives | `NextEvent()` with the remaining time; after the timeout the final state decides, like Python |
| Several accepted states | not possible | `ExpectedState`: `"ON"`, `new[] {"30", "30.0"}`, a `List<string>` or `null` |
| Rejected commands | exceptions of the client, written to the console and swallowed | `{"error": …}` of the client is reported, the test fails |
| Reset to NULL/UNDEF | sent as command | restored with `PostUpdate` |
| Unknown rules | `IsRuleActive` compares the status `null` with `"UNINITIALIZED"` – `true` | `false`, message as in Python |
| Results | JSON strings (`GetGroupMembers`, `GetLinksForItem`), `Dictionary<string, object>` | `JsonArray`/`JsonObject` like the client 2.0.0 |
| Messages | fixed to `Console` | `TestLog.Handler` (e.g. to `ILogger` or the xUnit output); texts as in Python |
| Dependencies | client 1.0.0, System.Text.Json 8.0.0 for every target | client 2.0.0 only; `netstandard2.1`, `net8.0`, `net10.0` |

Tests: 2 unit tests (value checks, `ExpectedState`) and 7 integration tests with 38 checks
against openHAB with the exercise configuration in `tests/openhab-conf` (device that confirms
late, value 0, unit, NULL restore, exact timeout, unknown things and rules, persistence time
range).
