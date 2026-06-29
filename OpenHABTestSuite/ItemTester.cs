using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Tests openHAB item behaviour: type checks, command/update validation,
    /// SSE-based state observation, and automatic state reset after each test.
    /// Mirrors the Python <c>ItemTester</c> class from openhab-test-suite.
    /// </summary>
    public class ItemTester
    {
        private readonly Items      _items;
        private readonly ItemEvents _itemEvents;

        private static readonly HashSet<string> ValidTypes = new()
        {
            "Color","Contact","DateTime","Dimmer","Group",
            "Image","Location","Number","Player",
            "Rollershutter","String","Switch"
        };

        private static readonly Regex Iso8601 = new(
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:?\d{2})?$",
            RegexOptions.Compiled);

        private static readonly Regex NumberWithUnit = new(
            @"^-?\d+(\.\d+)?(\s+\S+)?$", RegexOptions.Compiled);

        /// <summary>Creates an <see cref="ItemTester"/> backed by the given client.</summary>
        public ItemTester(OpenHABClient client)
        {
            _items      = new Items(client);
            _itemEvents = new ItemEvents(client);
        }

        // ── Static validators ─────────────────────────────────────────────────

        /// <returns><c>true</c> if <paramref name="v"/> is <c>ON</c> or <c>OFF</c>.</returns>
        public static bool IsValidSwitchValue(string? v) =>
            v != null && (v.Trim().ToUpper() is "ON" or "OFF");

        /// <returns><c>true</c> if <paramref name="v"/> is <c>OPEN</c> or <c>CLOSED</c>.</returns>
        public static bool IsValidContactValue(string? v) =>
            v != null && (v.Trim().ToUpper() is "OPEN" or "CLOSED");

        /// <returns><c>true</c> for <c>ON</c>, <c>OFF</c>, <c>INCREASE</c>,
        /// <c>DECREASE</c>, or a percentage 0–100.</returns>
        public static bool IsValidDimmerValue(string? v)
        {
            if (v == null) return false;
            var u = v.Trim().ToUpper();
            if (u is "ON" or "OFF" or "INCREASE" or "DECREASE") return true;
            return double.TryParse(u, out var d) && d >= 0 && d <= 100;
        }

        /// <returns><c>true</c> for <c>UP</c>, <c>DOWN</c>, <c>STOP</c>,
        /// <c>MOVE</c>, or a percentage 0–100.</returns>
        public static bool IsValidRollershutterValue(string? v)
        {
            if (v == null) return false;
            var u = v.Trim().ToUpper();
            if (u is "UP" or "DOWN" or "STOP" or "MOVE") return true;
            return double.TryParse(u, out var d) && d >= 0 && d <= 100;
        }

        /// <returns><c>true</c> for <c>ON</c>, <c>OFF</c>, <c>INCREASE</c>,
        /// <c>DECREASE</c>, or an HSB string <c>"H,S,B"</c>.</returns>
        public static bool IsValidColorValue(string? v)
        {
            if (v == null) return false;
            var u = v.Trim().ToUpper();
            if (u is "ON" or "OFF" or "INCREASE" or "DECREASE") return true;
            var parts = v.Trim().Split(',');
            if (parts.Length == 3 &&
                double.TryParse(parts[0], out var h) &&
                double.TryParse(parts[1], out var s) &&
                double.TryParse(parts[2], out var b))
                return h >= 0 && h <= 360 && s >= 0 && s <= 100 && b >= 0 && b <= 100;
            return false;
        }

        /// <returns><c>true</c> for <c>PLAY</c>, <c>PAUSE</c>, <c>NEXT</c>,
        /// <c>PREVIOUS</c>, <c>REWIND</c>, or <c>FASTFORWARD</c>.</returns>
        public static bool IsValidPlayerValue(string? v) =>
            v != null && v.Trim().ToUpper() is "PLAY" or "PAUSE" or "NEXT"
                                            or "PREVIOUS" or "REWIND" or "FASTFORWARD";

        /// <returns><c>true</c> for any numeric value, optionally with a unit.</returns>
        public static bool IsValidNumberValue(string? v) =>
            v != null && NumberWithUnit.IsMatch(v.Trim());

        /// <returns><c>true</c> for an ISO-8601 datetime string.</returns>
        public static bool IsValidDateTimeValue(string? v) =>
            v != null && Iso8601.IsMatch(v.Trim());

        /// <returns><c>true</c> for <c>"lat,lon"</c> or <c>"lat,lon,alt"</c>.</returns>
        public static bool IsValidLocationValue(string? v)
        {
            if (v == null) return false;
            var parts = v.Trim().Split(',');
            if (parts.Length < 2 || parts.Length > 3) return false;
            return double.TryParse(parts[0], out var lat) &&
                   double.TryParse(parts[1], out var lon) &&
                   (parts.Length == 2 || double.TryParse(parts[2], out _)) &&
                   lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
        }

        /// <returns><c>true</c> for an HTTP/HTTPS URL or a Base64 data URI.</returns>
        public static bool IsValidImageValue(string? v) =>
            v != null && (v.StartsWith("http://") || v.StartsWith("https://") ||
                          Regex.IsMatch(v, @"^data:image/[a-zA-Z+]+;base64,"));

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Checks whether an item exists in openHAB.</summary>
        public bool DoesItemExist(string itemName)
        {
            try
            {
                var raw  = _items.GetItem(itemName);
                var doc  = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("name", out var n) &&
                    n.GetString() == itemName) return true;
            }
            catch (Exception) { }
            Console.Error.WriteLine($"Error: The item '{itemName}' does not exist!");
            return false;
        }

        /// <summary>Verifies that an item is of the expected type.</summary>
        public bool CheckItemIsType(string itemName, string itemType)
        {
            if (!ValidTypes.Contains(itemType))
            {
                Console.Error.WriteLine($"Error: '{itemType}' is not a valid item type.");
                return false;
            }
            try
            {
                var raw      = _items.GetItem(itemName);
                var doc      = JsonDocument.Parse(raw);
                var actual   = doc.RootElement.GetProperty("type").GetString() ?? "";
                var baseType = actual.Contains(':') ? actual.Split(':')[0] : actual;
                if (baseType == itemType) return true;
                Console.Error.WriteLine(
                    $"Error: '{itemName}' is type '{actual}', expected '{itemType}'.");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error checking type of '{itemName}': {e.Message}");
            }
            return false;
        }

        /// <summary>Checks whether the item currently holds the given state.</summary>
        public bool CheckItemHasState(string itemName, string expected)
        {
            try { return _items.GetItemState(itemName) == expected; }
            catch { return false; }
        }

        /// <returns><c>true</c> if the item is of type <c>Group</c>.</returns>
        public bool IsGroupItem(string itemName) => CheckItemIsType(itemName, "Group");

        /// <summary>Returns the member items of a Group item as a JSON array string.</summary>
        public string GetGroupMembers(string groupName)
        {
            try
            {
                var raw = _items.GetItem(groupName, ".*", true);
                var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("members", out var m))
                    return m.GetRawText();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error reading group '{groupName}': {e.Message}");
            }
            return "[]";
        }

        /// <summary>Checks whether a group contains a specific member.</summary>
        public bool DoesGroupContainMember(string groupName, string memberName)
        {
            try
            {
                var members = JsonDocument.Parse(GetGroupMembers(groupName)).RootElement;
                foreach (var m in members.EnumerateArray())
                    if (m.TryGetProperty("name", out var n) && n.GetString() == memberName)
                        return true;
            }
            catch { }
            return false;
        }

        /// <summary>Checks whether a group member holds the expected state.</summary>
        public bool CheckGroupMemberState(string groupName, string memberName, string expected)
        {
            try
            {
                var members = JsonDocument.Parse(GetGroupMembers(groupName)).RootElement;
                foreach (var m in members.EnumerateArray())
                    if (m.TryGetProperty("name", out var n) && n.GetString() == memberName)
                        return m.TryGetProperty("state", out var s) && s.GetString() == expected;
            }
            catch { }
            return false;
        }

        // ── Per-type test methods ─────────────────────────────────────────────

        /// <summary>Tests a <c>Switch</c> item: sends <paramref name="command"/>
        /// and optionally verifies the resulting state via SSE.</summary>
        public bool TestSwitch(string itemName, string command,
                               string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Switch")) return false;
            if (!IsValidSwitchValue(command))
            { Console.Error.WriteLine($"Invalid Switch command '{command}'. Use ON or OFF."); return false; }
            return RunTest(itemName, "Switch", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>Contact</c> item (postUpdate only).</summary>
        public bool TestContact(string itemName, string? update = null,
                                string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Contact")) return false;
            if (update != null && !IsValidContactValue(update))
            { Console.Error.WriteLine($"Invalid Contact update '{update}'. Use OPEN or CLOSED."); return false; }
            return RunTest(itemName, "Contact", update, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>Color</c> item.</summary>
        public bool TestColor(string itemName, string command,
                              string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Color")) return false;
            if (!IsValidColorValue(command))
            { Console.Error.WriteLine($"Invalid Color command '{command}'."); return false; }
            return RunTest(itemName, "Color", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>Dimmer</c> item.</summary>
        public bool TestDimmer(string itemName, string command,
                               string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Dimmer")) return false;
            if (!IsValidDimmerValue(command))
            { Console.Error.WriteLine($"Invalid Dimmer command '{command}'."); return false; }
            return RunTest(itemName, "Dimmer", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>Rollershutter</c> item.</summary>
        public bool TestRollershutter(string itemName, string command,
                                      string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Rollershutter")) return false;
            if (!IsValidRollershutterValue(command))
            { Console.Error.WriteLine($"Invalid Rollershutter command '{command}'."); return false; }
            return RunTest(itemName, "Rollershutter", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>Number</c> item.</summary>
        public bool TestNumber(string itemName, string command,
                               string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Number")) return false;
            if (!IsValidNumberValue(command))
            { Console.Error.WriteLine($"Invalid Number command '{command}'."); return false; }
            return RunTest(itemName, "Number", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>Player</c> item.</summary>
        public bool TestPlayer(string itemName, string command,
                               string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Player")) return false;
            if (!IsValidPlayerValue(command))
            { Console.Error.WriteLine($"Invalid Player command '{command}'."); return false; }
            return RunTest(itemName, "Player", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>DateTime</c> item.</summary>
        public bool TestDateTime(string itemName, string command,
                                 string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "DateTime")) return false;
            if (!IsValidDateTimeValue(command))
            { Console.Error.WriteLine($"Invalid DateTime command '{command}'. Use ISO-8601."); return false; }
            return RunTest(itemName, "DateTime", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>Location</c> item (postUpdate only).</summary>
        public bool TestLocation(string itemName, string update,
                                 string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Location")) return false;
            if (!IsValidLocationValue(update))
            { Console.Error.WriteLine($"Invalid Location update '{update}'."); return false; }
            return RunTest(itemName, "Location", update, expectedState, timeoutSec);
        }

        /// <summary>Tests an <c>Image</c> item.</summary>
        public bool TestImage(string itemName, string command,
                              string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "Image")) return false;
            if (!IsValidImageValue(command))
            { Console.Error.WriteLine($"Invalid Image command '{command}'."); return false; }
            return RunTest(itemName, "Image", command, expectedState, timeoutSec);
        }

        /// <summary>Tests a <c>String</c> item.</summary>
        public bool TestString(string itemName, string command,
                               string? expectedState = null, int timeoutSec = 10)
        {
            if (!CheckItemIsType(itemName, "String")) return false;
            if (command == null)
            { Console.Error.WriteLine("Command for String item must not be null."); return false; }
            return RunTest(itemName, "String", command, expectedState, timeoutSec);
        }

        // ── Private core ──────────────────────────────────────────────────────

        private bool RunTest(string itemName, string itemType,
                             string? commandOrUpdate, string? expectedState,
                             int timeoutSec)
        {
            string? initialState = null;
            bool    result       = false;

            try
            {
                if (commandOrUpdate != null)
                    try { initialState = _items.GetItemState(itemName); }
                    catch { Console.WriteLine($"Warning: could not read initial state of '{itemName}'."); }

                using var cts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(timeoutSec));
                using var sse = _itemEvents.ItemStateChangedEvent(itemName);

                // Send the command / update
                if (commandOrUpdate != null)
                {
                    if (itemType is "Contact" or "Location")
                        _items.PostUpdate(itemName, commandOrUpdate);
                    else
                        _items.SendCommand(itemName, commandOrUpdate);
                }

                if (expectedState == null)
                {
                    result = true;
                }
                else
                {
                    foreach (var payload in sse.ReadAll(cts.Token))
                    {
                        try
                        {
                            var doc  = JsonDocument.Parse(payload);
                            var type = doc.RootElement.GetProperty("type").GetString();
                            if (type != "ItemStateChangedEvent") continue;
                            var inner = doc.RootElement.GetProperty("payload").GetString() ?? "{}";
                            var val   = JsonDocument.Parse(inner)
                                            .RootElement.GetProperty("value").GetString();
                            if (val == expectedState)
                            {
                                Console.WriteLine(
                                    $"OK: '{itemName}' reached state '{val}'.");
                                result = true;
                                break;
                            }
                        }
                        catch { /* malformed event — skip */ }
                    }

                    // Fallback: direct read after timeout
                    if (!result)
                    {
                        result = CheckItemHasState(itemName, expectedState);
                        if (!result)
                            Console.Error.WriteLine(
                                $"Error: state of '{itemName}' is not '{expectedState}' after {timeoutSec}s.");
                    }
                }
            }
            catch (OperationCanceledException) { /* timeout — handled above */ }
            catch (OpenHABException e)
            {
                Console.Error.WriteLine($"Error testing '{itemName}': {e.Message}");
            }
            finally
            {
                ResetItem(itemName, itemType, initialState);
            }
            return result;
        }

        private void ResetItem(string itemName, string itemType, string? initialState)
        {
            if (initialState == null) return;
            try
            {
                if (itemType is "Contact" or "Location")
                    _items.PostUpdate(itemName, initialState);
                else
                    _items.SendCommand(itemName, initialState);
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    $"Warning: could not reset '{itemName}' to '{initialState}': {e.Message}");
            }
        }
    }
}
