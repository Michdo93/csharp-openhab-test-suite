using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Checks items and runs command tests – the C# counterpart of ItemTester.py of
    /// openhab-test-suite. A test opens the event stream first, sends the command (Contact and
    /// Location: an update), waits at most <c>timeout</c> seconds for the expected state and
    /// afterwards restores the previous state.
    /// </summary>
    public sealed class ItemTester
    {
        static readonly string[] ValidTypes = { "Color", "Contact", "DateTime", "Dimmer", "Group", "Image",
                                                "Location", "Number", "Player", "Rollershutter", "String", "Switch" };
        readonly Items _items;
        readonly ItemEvents _itemEvents;

        /// <summary>Creates the tester for a connected client.</summary>
        public ItemTester(OpenHABClient client)
        {
            _items = new Items(client);
            _itemEvents = new ItemEvents(client);
        }

        // ── value checks (Python: _isValid…Value) ────────────────────────────

        static bool OneOf(string? v, params string[] words) =>
            v != null && words.Contains(v.Trim().ToUpperInvariant());

        static bool ToNumber(string? v, out double d)
        {
            d = 0;
            return v != null && v.Trim().Length > 0 &&
                   double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }

        static bool PercentOr(string? v, params string[] words) =>
            OneOf(v, words) || (ToNumber(v, out var d) && d >= 0 && d <= 100);

        static double[]? Numbers(string? v)
        {
            if (v == null) return null;
            var parts = v.Split(',');
            var out_ = new double[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (!ToNumber(parts[i], out out_[i])) return null;
            return out_;
        }

        /// <summary>ON or OFF.</summary>
        public static bool IsValidSwitchValue(string? v) => OneOf(v, "ON", "OFF");
        /// <summary>OPEN or CLOSED.</summary>
        public static bool IsValidContactValue(string? v) => OneOf(v, "OPEN", "CLOSED");
        /// <summary>ON, OFF, INCREASE, DECREASE or 0–100.</summary>
        public static bool IsValidDimmerValue(string? v) => PercentOr(v, "ON", "OFF", "INCREASE", "DECREASE");
        /// <summary>UP, DOWN, STOP, MOVE or 0–100.</summary>
        public static bool IsValidRollershutterValue(string? v) => PercentOr(v, "UP", "DOWN", "STOP", "MOVE");

        /// <summary>ON, OFF, INCREASE, DECREASE or "H,S,B".</summary>
        public static bool IsValidColorValue(string? v)
        {
            if (OneOf(v, "ON", "OFF", "INCREASE", "DECREASE")) return true;
            var x = Numbers(v);
            return x != null && x.Length == 3 && x[0] >= 0 && x[0] <= 360 && x[1] >= 0 && x[1] <= 100 &&
                   x[2] >= 0 && x[2] <= 100;
        }

        /// <summary>PLAY, PAUSE, NEXT, PREVIOUS, REWIND or FASTFORWARD.</summary>
        public static bool IsValidPlayerValue(string? v) =>
            OneOf(v, "PLAY", "PAUSE", "NEXT", "PREVIOUS", "REWIND", "FASTFORWARD");

        /// <summary>A number, optionally with unit ("20", "20.5", "20 °C").</summary>
        public static bool IsValidNumberValue(string? v) =>
            v != null && Regex.IsMatch(v.Trim(), @"^-?\d+(\.\d+)?(\s+\S+)?$");

        /// <summary>ISO 8601, e.g. "2024-01-15T08:30:00+0000".</summary>
        public static bool IsValidDateTimeValue(string? v) =>
            v != null && Regex.IsMatch(v.Trim(), @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:?\d{2})?$");

        /// <summary>"lat,lon" or "lat,lon,alt".</summary>
        public static bool IsValidLocationValue(string? v)
        {
            var x = Numbers(v);
            return x != null && (x.Length == 2 || x.Length == 3) && x[0] >= -90 && x[0] <= 90 &&
                   x[1] >= -180 && x[1] <= 180;
        }

        /// <summary>An http/https URL or a data URI (data:image/…;base64,…).</summary>
        public static bool IsValidImageValue(string? v)
        {
            if (v == null) return false;
            var t = v.TrimStart();
            return t.StartsWith("http://", StringComparison.Ordinal) ||
                   t.StartsWith("https://", StringComparison.Ordinal) ||
                   Regex.IsMatch(t, @"^data:image/[a-zA-Z+]+;base64,");
        }

        // ── checks ───────────────────────────────────────────────────────────

        /// <summary>True if the item exists.</summary>
        public bool DoesItemExist(string itemName)
        {
            var r = _items.GetItem(itemName);
            if (!r.IsError() && r.Str("name") == itemName) return true;
            TestLog.Error($"Error: The item '{itemName}' does not exist!");
            return false;
        }

        /// <summary>itemType: "Number" (base type) or "Number:Temperature" (with dimension).</summary>
        public bool CheckItemIsType(string itemName, string itemType)
        {
            var baseType = itemType.Split(':')[0];
            if (!ValidTypes.Contains(baseType))
            {
                TestLog.Error($"Error: '{itemType}' is not a valid item type.");
                return false;
            }
            var r = _items.GetItem(itemName);
            if (r.IsError())
            {
                TestLog.Error($"Error: The item '{itemName}' could not be found.");
                return false;
            }
            var actual = r.Str("type");
            if (actual.Split(':')[0] == itemType || actual == itemType) return true;
            TestLog.Error($"Error: The item '{itemName}' is not of type '{itemType}'! Found type: '{actual}'");
            return false;
        }

        /// <summary>True if the item has exactly this state.</summary>
        public bool CheckItemHasState(string itemName, string state)
        {
            var r = _items.GetItemState(itemName);
            if (r.IsError())
            {
                TestLog.Error($"Error: Could not retrieve the state for item '{itemName}'.");
                return false;
            }
            return Py.Text(r) == state;
        }

        /// <summary>True if the item is a group.</summary>
        public bool IsGroupItem(string itemName) => CheckItemIsType(itemName, "Group");

        /// <summary>Members of a group with their states (empty on errors).</summary>
        public JsonArray GetGroupMembers(string groupName)
        {
            var r = _items.GetItem(groupName, recursive: true);
            return r.IsError() ? new JsonArray() : r.List("members");
        }

        /// <summary>True if the group contains the item.</summary>
        public bool DoesGroupContainMember(string groupName, string memberName) =>
            GetGroupMembers(groupName).Any(m => m.Str("name") == memberName);

        /// <summary>True if the member of the group has the expected state.</summary>
        public bool CheckGroupMemberState(string groupName, string memberName, string expectedState)
        {
            foreach (var m in GetGroupMembers(groupName))
                if (m.Str("name") == memberName)
                    return Py.Text(m.At("state")) == expectedState;
            return false;
        }

        // ── the test run (Python: __testItem, __resetItem, __checkFinalState) ─

        void ResetItem(string itemName, bool isUpdate, string? initial)
        {
            if (initial == null) return;
            // NULL and UNDEF are no valid commands – they are restored as state
            var asUpdate = isUpdate || initial == "NULL" || initial == "UNDEF";
            var r = asUpdate ? _items.PostUpdate(itemName, initial) : _items.SendCommand(itemName, initial);
            if (r.IsError())
                TestLog.Error($"Warning: Could not reset item '{itemName}' to '{initial}': {r.ErrorMessage()}");
        }

        bool CheckFinalState(string itemName, ExpectedState expected)
        {
            var r = _items.GetItemState(itemName);
            if (r.IsError()) TestLog.Error($"Error: Could not retrieve the state for item '{itemName}'.");
            var ok = !r.IsError() && expected.Matches(Py.Text(r));
            if (!ok)
                TestLog.Error($"Error: After fallback, state of '{itemName}' is not {(expected.IsList ? "in " : "")}{expected}.");
            return ok;
        }

        bool TestItem(string itemName, string itemType, string? command, ExpectedState? expected, int timeout)
        {
            var isUpdate = itemType == "Contact" || itemType == "Location";
            if (timeout <= 0) timeout = 10;
            string? initial = null;
            if (command != null)
            {
                var s = _items.GetItemState(itemName);
                if (s.IsError()) TestLog.Error($"Warning: Could not retrieve initial state for item '{itemName}'.");
                else initial = Py.Text(s);
            }
            var result = false;
            // open the stream before sending, so that no event is missed
            using (var stream = _itemEvents.ItemStateChangedEvent(itemName))
            {
                if (stream.IsError)
                {
                    TestLog.Error($"Error: No event stream for item '{itemName}': {stream.Error}");
                }
                else
                {
                    var rejected = false;
                    if (command != null)
                    {
                        var sent = isUpdate ? _items.PostUpdate(itemName, command) : _items.SendCommand(itemName, command);
                        rejected = sent.IsError();
                        if (rejected)
                            TestLog.Error($"Error: '{command}' was rejected for '{itemName}': {sent.ErrorMessage()}");
                    }
                    if (!rejected && expected == null)
                    {
                        result = true;
                    }
                    else if (!rejected && expected != null)
                    {
                        // if the state does not change at all, the check after the timeout decides
                        var end = DateTime.UtcNow.AddSeconds(timeout);
                        var reached = false;
                        for (;;)
                        {
                            var left = end - DateTime.UtcNow;
                            if (left <= TimeSpan.Zero) break;
                            var ev = stream.NextEvent(left);
                            if (ev == null)
                            {
                                if (!stream.IsOpen) break;
                                continue;
                            }
                            var value = ev.Str("payload.value");
                            if (ev.Str("type") == "ItemStateChangedEvent" && ev.At("payload.value") != null &&
                                expected.Matches(value))
                            {
                                TestLog.Info($"Success: '{itemName}' reached state '{value}'.");
                                reached = true;
                                break;
                            }
                        }
                        result = reached || CheckFinalState(itemName, expected);
                    }
                }
            }
            ResetItem(itemName, isUpdate, initial);
            return result;
        }

        bool TypeCheck(string itemName, string type)
        {
            if (CheckItemIsType(itemName, type)) return true;
            TestLog.Error($"Test failed: '{itemName}' is not of type '{type}'.");
            return false;
        }

        static bool Invalid(string message)
        {
            TestLog.Error(message);
            return false;
        }

        /// <summary>Sends a Color command ("240,100,100", ON, …) and waits for the expected state.</summary>
        public bool TestColor(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Color")) return false;
            if (!IsValidColorValue(command))
                return Invalid($"Test failed: '{command}' is not a valid Color command. Use ON, OFF, INCREASE, DECREASE, or 'H,S,B' (e.g. '240,100,100').");
            return TestItem(itemName, "Color", command, expectedState, timeout);
        }

        /// <summary>Sends a Contact update (OPEN/CLOSED; null: no update) and waits for the expected state.</summary>
        public bool TestContact(string itemName, string? update = null, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Contact")) return false;
            if (update != null && !IsValidContactValue(update))
                return Invalid($"Test failed: '{update}' is not a valid Contact update. Use OPEN or CLOSED.");
            return TestItem(itemName, "Contact", update, expectedState, timeout);
        }

        /// <summary>Sends a DateTime command (ISO 8601) and waits for the expected state.</summary>
        public bool TestDateTime(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "DateTime")) return false;
            if (!IsValidDateTimeValue(command))
                return Invalid($"Test failed: '{command}' is not a valid DateTime command. Use ISO-8601 format, e.g. '2024-01-15T08:30:00+0000'.");
            return TestItem(itemName, "DateTime", command, expectedState, timeout);
        }

        /// <summary>Sends a Dimmer command and waits for the expected state.</summary>
        public bool TestDimmer(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Dimmer")) return false;
            if (!IsValidDimmerValue(command))
                return Invalid($"Test failed: '{command}' is not a valid Dimmer command. Use ON, OFF, INCREASE, DECREASE, or a percentage value between 0 and 100.");
            return TestItem(itemName, "Dimmer", command, expectedState, timeout);
        }

        /// <summary>Sends an Image command (URL or data URI) and waits for the expected state.</summary>
        public bool TestImage(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Image")) return false;
            if (!IsValidImageValue(command))
                return Invalid($"Test failed: '{command}' is not a valid Image command. Use an http/https URL or a Base64 data URI (data:image/...;base64,...).");
            return TestItem(itemName, "Image", command, expectedState, timeout);
        }

        /// <summary>Sends a Location update ("lat,lon[,alt]") and waits for the expected state.</summary>
        public bool TestLocation(string itemName, string update, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Location")) return false;
            if (!IsValidLocationValue(update))
                return Invalid($"Test failed: '{update}' is not a valid Location update. Use 'lat,lon' or 'lat,lon,alt', e.g. '48.7758,9.1829' or '48.7758,9.1829,300.0'.");
            return TestItem(itemName, "Location", update, expectedState, timeout);
        }

        /// <summary>Sends a Number command (optionally with unit) and waits for the expected state.</summary>
        public bool TestNumber(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Number")) return false;
            if (!IsValidNumberValue(command))
                return Invalid($"Test failed: '{command}' is not a valid Number command. Use a numeric value, optionally with a unit (e.g. '20', '20.5', '20 °C').");
            return TestItem(itemName, "Number", command, expectedState, timeout);
        }

        /// <summary>Sends a Player command and waits for the expected state.</summary>
        public bool TestPlayer(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Player")) return false;
            if (!IsValidPlayerValue(command))
                return Invalid($"Test failed: '{command}' is not a valid Player command. Use PLAY, PAUSE, NEXT, PREVIOUS, REWIND, or FASTFORWARD.");
            return TestItem(itemName, "Player", command, expectedState, timeout);
        }

        /// <summary>Sends a Rollershutter command and waits for the expected state.</summary>
        public bool TestRollershutter(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Rollershutter")) return false;
            if (!IsValidRollershutterValue(command))
                return Invalid($"Test failed: '{command}' is not a valid Rollershutter command. Use UP, DOWN, STOP, MOVE, or a percentage value between 0 and 100.");
            return TestItem(itemName, "Rollershutter", command, expectedState, timeout);
        }

        /// <summary>Sends a String command and waits for the expected state.</summary>
        public bool TestString(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "String")) return false;
            return TestItem(itemName, "String", command, expectedState, timeout);
        }

        /// <summary>Sends ON or OFF and waits for the expected state.</summary>
        public bool TestSwitch(string itemName, string command, ExpectedState? expectedState = null, int timeout = 10)
        {
            if (!TypeCheck(itemName, "Switch")) return false;
            if (!IsValidSwitchValue(command))
                return Invalid($"Test failed: '{command}' is not a valid Switch command. Use ON or OFF.");
            return TestItem(itemName, "Switch", command, expectedState, timeout);
        }
    }
}
