using OpenHABRestClient;
using OpenHABTestSuite;
using Xunit;

namespace OpenHABTestSuite.Tests;

/// <summary>
/// Against openHAB with the exercise configuration in tests/openhab-conf
/// (OPENHAB_URL, OPENHAB_TOKEN). Without OPENHAB_URL the tests are skipped.
/// The tests change the same items, so they run one after another.
/// </summary>
[Collection("openHAB")]
public class IntegrationTests
{
    static OpenHABClient Client()
    {
        var url = Environment.GetEnvironmentVariable("OPENHAB_URL");
        Assert.SkipWhen(string.IsNullOrEmpty(url), "OPENHAB_URL is not set");
        return new OpenHABClient(url!, token: Environment.GetEnvironmentVariable("OPENHAB_TOKEN"));
    }

    [Fact]
    public void Items()
    {
        using var c = Client();
        var items = new ItemTester(c);
        Assert.True(items.DoesItemExist("Wohnzimmer_Licht"));
        Assert.False(items.DoesItemExist("Gibts_nicht"));
        Assert.True(items.CheckItemIsType("Werkstatt_Temperatur", "Number"));
        Assert.True(items.CheckItemIsType("Werkstatt_Temperatur", "Number:Temperature"));
        Assert.False(items.CheckItemIsType("Wohnzimmer_Licht", "Dimmer"));
        Assert.False(items.CheckItemIsType("Wohnzimmer_Licht", "Lampe"));
        Assert.True(items.IsGroupItem("gLichter"));
        Assert.True(items.DoesGroupContainMember("gLichter", "Wohnzimmer_Licht"));
        Assert.False(items.DoesGroupContainMember("gLichter", "Flur_Haustuer"));
    }

    [Fact]
    public void CommandTests()
    {
        using var c = Client();
        var items = new ItemTester(c);
        Assert.True(items.TestSwitch("Wohnzimmer_Licht", "ON", "ON", 5));
        Assert.False(items.TestSwitch("Wohnzimmer_Licht", "HALB", "ON", 2));
        Assert.True(items.TestDimmer("Wohnzimmer_Dimmer", "0", new[] { "0", "0.0" }, 5));
        Assert.True(items.TestDimmer("Wohnzimmer_Dimmer", "30", new[] { "30", "30.0" }, 5));
        Assert.True(items.TestRollershutter("Kueche_Rollladen", "DOWN", "100", 5));
        Assert.True(items.TestContact("Flur_Haustuer", "OPEN", "OPEN", 5));
        Assert.True(items.TestNumber("Werkstatt_Temperatur", "23.5 °C", "23.5 °C", 5));
        Assert.True(items.TestSwitch("Wohnzimmer_Licht", "OFF", null, 2));
    }

    [Fact]
    public void LateConfirmingDevice()
    {
        using var c = Client();
        // autoupdate is off for Werkstatt_Luefter: the state changes only when the "device" answers
        var device = Task.Run(() =>
        {
            using var s = new ItemEvents(c).ItemCommandEvent("Werkstatt_Luefter");
            var ev = s.NextEvent(TimeSpan.FromSeconds(8));
            if (ev == null) return;
            Thread.Sleep(1500);
            new Items(c).PostUpdate("Werkstatt_Luefter", ev.Str("payload.value"));
        });
        Thread.Sleep(300);
        Assert.True(new ItemTester(c).TestSwitch("Werkstatt_Luefter", "ON", "ON", 6));
        device.Wait();
        new Items(c).PostUpdate("Werkstatt_Luefter", "OFF");
    }

    [Fact]
    public void NullIsRestoredAsState()
    {
        using var c = Client();
        var raw = new Items(c);
        raw.AddOrUpdateItem("TS_Undef", new { type = "Switch", name = "TS_Undef", label = "x" });
        var items = new ItemTester(c);
        Assert.True(items.TestSwitch("TS_Undef", "ON", "ON", 5));
        Thread.Sleep(300);
        Assert.True(items.CheckItemHasState("TS_Undef", "NULL"));
        raw.DeleteItem("TS_Undef");
    }

    [Fact]
    public void TimeoutHolds()
    {
        using var c = Client();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // autoupdate off and no device: the state never arrives
        Assert.False(new ItemTester(c).TestSwitch("Werkstatt_Luefter", "ON", "ON", 3));
        Assert.InRange(sw.Elapsed.TotalSeconds, 2.5, 6);
        new Items(c).PostUpdate("Werkstatt_Luefter", "OFF");
    }

    [Fact]
    public void ThingsRulesLinksSitemaps()
    {
        using var c = Client();
        var things = new ThingTester(c);
        Assert.True(things.IsThingOnline("astro:sun:zuhause"));
        Assert.False(things.IsThingOnline("astro:sun:gibtsnicht"));
        Assert.True(things.IsThingUnknown("astro:sun:gibtsnicht"));
        var rules = new RuleTester(c);
        Assert.True(rules.IsRuleActive("restbuch-1"));
        Assert.False(rules.IsRuleActive("gibtsnicht"));
        Assert.False(rules.IsRuleDisabled("gibtsnicht"));
        Assert.Equal("restbuch-1", rules.GetRuleStatus("restbuch-1").Str("uid"));
        Assert.Empty(rules.GetRuleStatus("gibtsnicht"));
        var sitemaps = new SitemapTester(c);
        Assert.True(sitemaps.DoesSitemapExist("restbuch"));
        Assert.True(sitemaps.DoesSitemapContainItem("restbuch", "Wohnzimmer_Licht"));
        Assert.False(sitemaps.DoesSitemapContainItem("restbuch", "Gibts_nicht"));
        var channels = new ChannelTester(c);
        Assert.False(channels.IsItemLinkedToChannel("Wohnzimmer_Licht", "x:y:z#a"));
        Assert.Empty(channels.GetLinksForItem("Gibts_nicht"));
    }

    [Fact]
    public void PersistenceRange()
    {
        using var c = Client();
        var p = new Persistence(c);
        p.StoreItemData("Werkstatt_Temperatur", "2026-03-01T10:00:00.000Z", "18.5", "inmemory");
        var t = new PersistenceTester(c);
        Assert.True(t.IsItemPersisted("inmemory", "Werkstatt_Temperatur"));
        Assert.True(t.HasDataInRange("inmemory", "Werkstatt_Temperatur", "2026-03-01T09:00:00.000Z", "2026-03-01T11:00:00.000Z"));
        Assert.False(t.HasDataInRange("inmemory", "Werkstatt_Temperatur", "2001-01-01T00:00:00.000Z", "2001-01-02T00:00:00.000Z"));
        p.DeleteItemData("Werkstatt_Temperatur", "2026-03-01T09:00:00.000Z", "2026-03-01T11:00:00.000Z", "inmemory");
    }
}

[CollectionDefinition("openHAB", DisableParallelization = true)]
public class OpenHABCollection { }
