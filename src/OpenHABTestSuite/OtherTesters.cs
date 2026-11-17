using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>Checks the status of things – the C# counterpart of ThingTester.py.</summary>
    public sealed class ThingTester
    {
        readonly Things _things;

        /// <summary>Creates the tester for a connected client.</summary>
        public ThingTester(OpenHABClient client) => _things = new Things(client);

        /// <summary>"ONLINE", "OFFLINE", … or "UNKNOWN" (also for unknown things).</summary>
        public string GetThingStatus(string thingUID)
        {
            var r = _things.GetThing(thingUID);
            var s = r.IsError() ? "" : r.Str("statusInfo.status");
            return s.Length == 0 ? "UNKNOWN" : s;
        }

        /// <summary>True if the thing has this status.</summary>
        public bool IsThingStatus(string thingUID, string statusToCheck) => GetThingStatus(thingUID) == statusToCheck;
        /// <summary>Status ONLINE.</summary>
        public bool IsThingOnline(string thingUID) => IsThingStatus(thingUID, "ONLINE");
        /// <summary>Status OFFLINE.</summary>
        public bool IsThingOffline(string thingUID) => IsThingStatus(thingUID, "OFFLINE");
        /// <summary>Status PENDING.</summary>
        public bool IsThingPending(string thingUID) => IsThingStatus(thingUID, "PENDING");
        /// <summary>Status UNKNOWN.</summary>
        public bool IsThingUnknown(string thingUID) => IsThingStatus(thingUID, "UNKNOWN");
        /// <summary>Status UNINITIALIZED.</summary>
        public bool IsThingUninitialized(string thingUID) => IsThingStatus(thingUID, "UNINITIALIZED");
        /// <summary>Status ERROR.</summary>
        public bool IsThingError(string thingUID) => IsThingStatus(thingUID, "ERROR");

        bool SetEnabled(string uid, bool enabled)
        {
            var r = _things.SetThingStatus(uid, enabled);
            var what = enabled ? "enabl" : "disabl";
            if (!r.IsError())
            {
                TestLog.Info($"Thing {uid} was successfully {what}ed.");
                return true;
            }
            TestLog.Error($"Error {what}ing thing {uid}: {r.ErrorMessage()}");
            return false;
        }

        /// <summary>Enables the thing.</summary>
        public bool EnableThing(string thingUID) => SetEnabled(thingUID, true);
        /// <summary>Disables the thing.</summary>
        public bool DisableThing(string thingUID) => SetEnabled(thingUID, false);
    }

    /// <summary>Runs and checks rules – the C# counterpart of RuleTester.py.</summary>
    public sealed class RuleTester
    {
        readonly Rules _rules;
        readonly Items _items;

        /// <summary>Creates the tester for a connected client.</summary>
        public RuleTester(OpenHABClient client)
        {
            _rules = new Rules(client);
            _items = new Items(client);
        }

        JsonNode? Rule(string ruleUID)
        {
            var r = _rules.GetRule(ruleUID);
            if (r.IsError() || r.At("status") is not JsonObject)
            {
                TestLog.Error($"Error retrieving the status of rule {ruleUID}.");
                return null;
            }
            return r;
        }

        /// <summary>True if the rule is disabled (UNINITIALIZED/DISABLED).</summary>
        public bool IsRuleDisabled(string ruleUID)
        {
            var r = Rule(ruleUID);
            if (r == null) return false;
            string st = r.Str("status.status"), de = r.Str("status.statusDetail");
            TestLog.Info($"Rule status: {st}, Detail: {de}");
            return st == "UNINITIALIZED" && de == "DISABLED";
        }

        bool StatusIs(string ruleUID, string want, bool negate)
        {
            var r = Rule(ruleUID);
            if (r == null) return false;
            var st = r.Str("status.status");
            TestLog.Info($"Rule status: {st}");
            return (st == want) != negate;
        }

        /// <summary>True if the rule is initialized (not UNINITIALIZED).</summary>
        public bool IsRuleActive(string ruleUID) => StatusIs(ruleUID, "UNINITIALIZED", true);
        /// <summary>Status RUNNING.</summary>
        public bool IsRuleRunning(string ruleUID) => StatusIs(ruleUID, "RUNNING", false);
        /// <summary>Status IDLE.</summary>
        public bool IsRuleIdle(string ruleUID) => StatusIs(ruleUID, "IDLE", false);

        /// <summary>Runs a rule (if it is not disabled); contextData may be null.</summary>
        public bool RunRule(string ruleUID, object? contextData = null)
        {
            if (IsRuleDisabled(ruleUID))
            {
                TestLog.Error($"Error: Rule {ruleUID} could not be executed because it is disabled.");
                return false;
            }
            var r = _rules.RunNow(ruleUID, contextData);
            if (!r.IsError())
            {
                TestLog.Info($"Rule {ruleUID} executed successfully.");
                return true;
            }
            TestLog.Error($"Error executing rule {ruleUID}: {r.ErrorMessage()}");
            return false;
        }

        /// <summary>Enables the rule.</summary>
        public bool EnableRule(string ruleUID)
        {
            var r = _rules.Enable(ruleUID);
            if (!r.IsError())
            {
                TestLog.Info($"Rule {ruleUID} enabled successfully.");
                return true;
            }
            TestLog.Error($"Error enabling rule {ruleUID}: {r.ErrorMessage()}");
            return false;
        }

        /// <summary>Disables the rule.</summary>
        public bool DisableRule(string ruleUID)
        {
            var r = _rules.Disable(ruleUID);
            if (!r.IsError())
            {
                TestLog.Info($"Rule {ruleUID} disabled successfully.");
                return true;
            }
            TestLog.Error($"Error disabling rule {ruleUID}: {r.ErrorMessage()}");
            return false;
        }

        /// <summary>Runs a rule, waits <paramref name="waitSeconds"/> (2 s as in Python) and checks the item state.</summary>
        public bool TestRuleExecution(string ruleUID, string expectedItem, string expectedValue, double waitSeconds = 2.0)
        {
            if (!RunRule(ruleUID))
            {
                TestLog.Error($"Error: Rule {ruleUID} could not be executed.");
                return false;
            }
            Thread.Sleep((int)((waitSeconds > 0 ? waitSeconds : 2.0) * 1000));
            var s = _items.GetItemState(expectedItem);
            var state = s.IsError() ? s.ErrorMessage() : Py.Text(s);
            if (!s.IsError() && state == expectedValue)
            {
                TestLog.Info($"{expectedItem} state after rule execution: {state}");
                return true;
            }
            TestLog.Error($"Error: State of item {expectedItem} after rule execution does not match. Expected: {expectedValue}, Found: {state}");
            return false;
        }

        /// <summary>{status, statusDetail, editable, name, uid} or an empty object.</summary>
        public JsonObject GetRuleStatus(string ruleUID)
        {
            var r = Rule(ruleUID);
            if (r == null) return new JsonObject();
            string st = r.Str("status.status"), de = r.Str("status.statusDetail");
            return new JsonObject
            {
                ["status"] = st.Length == 0 ? "UNKNOWN" : st,
                ["statusDetail"] = de.Length == 0 ? "UNKNOWN" : de,
                ["editable"] = r.Bool("editable"),
                ["name"] = r.Str("name"),
                ["uid"] = r.Str("uid"),
            };
        }
    }

    /// <summary>Checks links between items and channels – the C# counterpart of ChannelTester.py.</summary>
    public sealed class ChannelTester
    {
        readonly Links _links;

        /// <summary>Creates the tester for a connected client.</summary>
        public ChannelTester(OpenHABClient client) => _links = new Links(client);

        /// <summary>True if the item is linked to the channel.</summary>
        public bool IsItemLinkedToChannel(string itemName, string channelUID)
        {
            var r = _links.GetLink(itemName, channelUID);
            return !r.IsError() && r.Str("itemName") == itemName;
        }

        /// <summary>All links of an item (empty on errors).</summary>
        public JsonArray GetLinksForItem(string itemName) => _links.GetLinks(itemName: itemName).List();

        /// <summary>True if there are links without item or channel.</summary>
        public bool HasOrphanedLinks() => _links.GetOrphanLinks().List().Count > 0;

        /// <summary>True if the item is linked to at least one channel.</summary>
        public bool IsItemLinkedToAnyChannel(string itemName) => GetLinksForItem(itemName).Count > 0;
    }

    /// <summary>Checks persisted data – the C# counterpart of PersistenceTester.py.</summary>
    public sealed class PersistenceTester
    {
        readonly Persistence _persistence;

        /// <summary>Creates the tester for a connected client.</summary>
        public PersistenceTester(OpenHABClient client) => _persistence = new Persistence(client);

        /// <summary>True if the service lists the item (rrd4j lists none: false).</summary>
        public bool IsItemPersisted(string serviceID, string itemName) =>
            _persistence.GetItemsFromService(serviceID).List().Any(e =>
                (e is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>() == itemName) ||
                e.Str("name") == itemName);

        /// <summary>startTime, endTime: ISO 8601, e.g. "2026-11-01T00:00:00.000Z".</summary>
        public bool HasDataInRange(string serviceID, string itemName, string startTime, string endTime)
        {
            var r = _persistence.GetItemPersistenceData(itemName, serviceID: serviceID, startTime: startTime, endTime: endTime);
            return !r.IsError() && r.List("data").Count > 0;
        }

        /// <summary>True if the last stored value has the expected state.</summary>
        public bool CheckLastPersistedState(string serviceID, string itemName, string expectedState)
        {
            var r = _persistence.GetItemPersistenceData(itemName, serviceID: serviceID);
            var data = r.IsError() ? new JsonArray() : r.List("data");
            if (data.Count == 0) return false;
            return Py.Text(data[data.Count - 1].At("state")) == expectedState;
        }
    }

    /// <summary>Checks sitemaps – the C# counterpart of SitemapTester.py.</summary>
    public sealed class SitemapTester
    {
        readonly Sitemaps _sitemaps;

        /// <summary>Creates the tester for a connected client.</summary>
        public SitemapTester(OpenHABClient client) => _sitemaps = new Sitemaps(client);

        /// <summary>True if the sitemap exists.</summary>
        public bool DoesSitemapExist(string sitemapName) =>
            _sitemaps.GetSitemaps().List().Any(e => e.Str("name") == sitemapName);

        static bool Search(JsonNode? node, string itemName)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o.Str("item.name") == itemName) return true;
                    return o.Any(p => Search(p.Value, itemName));
                case JsonArray a:
                    return a.Any(e => Search(e, itemName));
            }
            return false;
        }

        /// <summary>True if a widget of the sitemap shows the item.</summary>
        public bool DoesSitemapContainItem(string sitemapName, string itemName)
        {
            var r = _sitemaps.GetSitemap(sitemapName);
            return !r.IsError() && Search(r, itemName);
        }
    }
}
