# csharp-openhab-test-suite

Tests for openHAB installations – items, things, rules, links, persistence and sitemaps – for
xUnit, NUnit, MSTest or a simple console program. It is the C# counterpart of the Python
[openhab-test-suite](https://github.com/Michdo93/openhab-test-suite) with the same classes,
methods and messages, built on
[csharp-openhab-rest-client](https://github.com/Michdo93/csharp-openhab-rest-client) 2.

```csharp
using OpenHABRestClient;
using OpenHABTestSuite;

using var client = new OpenHABClient("http://127.0.0.1:8080",
                                     token: Environment.GetEnvironmentVariable("OPENHAB_TOKEN"));

var items = new ItemTester(client);
bool ok = items.TestSwitch("Wohnzimmer_Licht", "ON", "ON", timeout: 5);
items.TestDimmer("Wohnzimmer_Dimmer", "30", new[] { "30", "30.0" });   // several accepted states
items.TestContact("Flur_Haustuer", "OPEN", "OPEN");                     // Contact: an update

new ThingTester(client).IsThingOnline("astro:sun:zuhause");
new RuleTester(client).TestRuleExecution("restbuch-1", "Wohnzimmer_Licht", "ON");
new PersistenceTester(client).HasDataInRange("inmemory", "Werkstatt_Temperatur",
                                             "2026-11-01T00:00:00.000Z", "2026-11-02T00:00:00.000Z");
```

What 2.0.0 changes: [BUGS-1.0.0.md](BUGS-1.0.0.md).

## Installation

```bash
dotnet add package CSharpOpenHABTestSuite --version 2.0.1
```

Targets `netstandard2.1`, `net8.0` and `net10.0`; the only dependency is
`CSharpOpenHABRestClient` 2.0.1.

2.0.1 only raises the dependency to `CSharpOpenHABRestClient` 2.0.1 (the client package 2.0.0 on
nuget.org was built before its last fixes); the test suite itself is unchanged.

## How a command test works

`TestSwitch`, `TestDimmer`, `TestNumber`, … (and `TestContact`/`TestLocation` with an update):

1. check the item type (`Number:Temperature` counts as `Number`) and the value
2. remember the current state
3. open the event stream of the item, *then* send the command – no event is missed
4. wait at most `timeout` seconds (default 10) for an `ItemStateChangedEvent` with an accepted
   state; without event the state after the timeout decides
5. restore the previous state (`NULL`/`UNDEF` as state, not as command)

`expectedState` is a string, an array/list of accepted states or `null` (then the test only
checks that openHAB accepts the command). A rejected command fails the test.

## Classes

| Class | Methods |
|---|---|
| `ItemTester` | `DoesItemExist`, `CheckItemIsType`, `CheckItemHasState`, `IsGroupItem`, `GetGroupMembers`, `DoesGroupContainMember`, `CheckGroupMemberState`, `TestColor`, `TestContact`, `TestDateTime`, `TestDimmer`, `TestImage`, `TestLocation`, `TestNumber`, `TestPlayer`, `TestRollershutter`, `TestString`, `TestSwitch`, static `IsValid…Value` |
| `ThingTester` | `GetThingStatus` (`"UNKNOWN"` for unknown things), `IsThingStatus`, `IsThingOnline`, `IsThingOffline`, `IsThingPending`, `IsThingUnknown`, `IsThingUninitialized`, `IsThingError`, `EnableThing`, `DisableThing` |
| `RuleTester` | `RunRule`, `EnableRule`, `DisableRule`, `TestRuleExecution` (waits 2 s), `IsRuleActive`, `IsRuleDisabled`, `IsRuleRunning`, `IsRuleIdle`, `GetRuleStatus` |
| `ChannelTester` | `IsItemLinkedToChannel`, `GetLinksForItem`, `HasOrphanedLinks`, `IsItemLinkedToAnyChannel` |
| `PersistenceTester` | `IsItemPersisted`, `HasDataInRange`, `CheckLastPersistedState` |
| `SitemapTester` | `DoesSitemapExist`, `DoesSitemapContainItem` |

## Messages

The testers write what they do like the Python library (`Success: …`, `Error: …`). By default
info goes to standard output and errors to standard error; redirect them, e.g. to the xUnit
output:

```csharp
public class LichtTests(ITestOutputHelper output)
{
    [Fact]
    public void LichtSchaltet()
    {
        TestLog.Handler = (level, text) => output.WriteLine(text);
        Assert.True(new ItemTester(client).TestSwitch("Wohnzimmer_Licht", "ON", "ON"));
    }
}
```

## Development

```bash
dotnet test --project tests/OpenHABTestSuite.Tests                # unit tests
OPENHAB_URL=http://127.0.0.1:8080 OPENHAB_TOKEN=oh.xxx \
  dotnet test --project tests/OpenHABTestSuite.Tests              # plus integration tests
dotnet pack src/OpenHABTestSuite -c Release -o dist
```

The integration tests expect the exercise configuration in `tests/openhab-conf` (Werkstatt_Luefter
without autoupdate as a device that confirms late). GitHub Actions starts a fresh openHAB 5.

## License

MIT – Michael Christian Dörflinger
