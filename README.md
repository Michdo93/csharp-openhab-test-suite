# csharp-openhab-test-suite

A C# testing library for validating openHAB installations. Mirrors the Python
[openhab-test-suite](https://github.com/Michdo93/openhab-test-suite) with identical
class and method names, powered by
[CSharpOpenHABRestClient](https://www.nuget.org/packages/CSharpOpenHABRestClient).

## Classes

| Class | Description |
|---|---|
| `ItemTester` | Validates item types, sends commands/updates, verifies state via SSE, auto-resets |
| `ThingTester` | Checks Thing status (ONLINE/OFFLINE/…), enables and disables Things |
| `RuleTester` | Runs rules, enables/disables, checks status |
| `ChannelTester` | Verifies item-channel links and orphaned links |
| `PersistenceTester` | Checks item registration in persistence services and historical data |
| `SitemapTester` | Verifies sitemap existence and item references |

## Installation

```bash
dotnet add package CSharpOpenHABTestSuite
```

## Usage

```csharp
using OpenHABRestClient;
using OpenHABTestSuite;

using var client = new OpenHABClient("http://127.0.0.1:8080", "openhab", "habopen");

var items = new ItemTester(client);
items.TestSwitch("MySwitch", "ON", "ON", 10);

var things = new ThingTester(client);
things.IsThingOnline("astro:sun:local");

var rules = new RuleTester(client);
rules.EnableRule("my-rule");
rules.RunRule("my-rule");
```

## License

MIT License
