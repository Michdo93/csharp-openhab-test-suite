using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Tests openHAB rule execution, enable/disable, and status checks.
    /// Mirrors the Python <c>RuleTester</c> class from openhab-test-suite.
    /// </summary>
    public class RuleTester
    {
        private readonly Rules _rules;
        private readonly Items _items;

        /// <summary>Creates a <see cref="RuleTester"/> backed by the given client.</summary>
        public RuleTester(OpenHABClient client)
        {
            _rules = new Rules(client);
            _items = new Items(client);
        }

        /// <summary>Retrieves the full status of a rule as a dictionary.</summary>
        public Dictionary<string, object> GetRuleStatus(string ruleUID)
        {
            try
            {
                var doc = JsonDocument.Parse(_rules.GetRule(ruleUID));
                if (doc.RootElement.TryGetProperty("status", out var s))
                {
                    var info = new Dictionary<string, object>
                    {
                        ["status"]       = s.TryGetProperty("status",       out var st) ? st.GetString()! : "UNKNOWN",
                        ["statusDetail"] = s.TryGetProperty("statusDetail", out var sd) ? sd.GetString()! : "UNKNOWN",
                        ["editable"]     = doc.RootElement.TryGetProperty("editable", out var ed) && ed.GetBoolean(),
                        ["name"]         = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString()! : "",
                        ["uid"]          = doc.RootElement.TryGetProperty("uid",  out var u) ? u.GetString()! : "",
                    };
                    Console.WriteLine($"Rule status: {string.Join(", ", info)}");
                    return info;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error reading status of rule '{ruleUID}': {e.Message}");
            }
            return new Dictionary<string, object>();
        }

        private string StatusOf(string ruleUID)
        {
            var info = GetRuleStatus(ruleUID);
            return info.TryGetValue("status", out var s) ? s.ToString()! : "UNKNOWN";
        }

        private string DetailOf(string ruleUID)
        {
            var info = GetRuleStatus(ruleUID);
            return info.TryGetValue("statusDetail", out var d) ? d.ToString()! : "NONE";
        }

        /// <returns><c>true</c> if the rule status is not <c>UNINITIALIZED</c>.</returns>
        public bool IsRuleActive(string ruleUID) => StatusOf(ruleUID) != "UNINITIALIZED";

        /// <returns><c>true</c> if status is <c>UNINITIALIZED</c> and detail is <c>DISABLED</c>.</returns>
        public bool IsRuleDisabled(string ruleUID) =>
            StatusOf(ruleUID) == "UNINITIALIZED" && DetailOf(ruleUID) == "DISABLED";

        /// <returns><c>true</c> if the rule is currently <c>RUNNING</c>.</returns>
        public bool IsRuleRunning(string ruleUID) => StatusOf(ruleUID) == "RUNNING";

        /// <returns><c>true</c> if the rule is <c>IDLE</c>.</returns>
        public bool IsRuleIdle(string ruleUID) => StatusOf(ruleUID) == "IDLE";

        /// <summary>Enables a rule.</summary>
        public bool EnableRule(string ruleUID)
        {
            try
            {
                _rules.Enable(ruleUID);
                Console.WriteLine($"Rule '{ruleUID}' enabled successfully.");
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error enabling rule '{ruleUID}': {e.Message}");
                return false;
            }
        }

        /// <summary>Disables a rule.</summary>
        public bool DisableRule(string ruleUID)
        {
            try
            {
                _rules.Disable(ruleUID);
                Console.WriteLine($"Rule '{ruleUID}' disabled successfully.");
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error disabling rule '{ruleUID}': {e.Message}");
                return false;
            }
        }

        /// <summary>Executes a rule immediately.</summary>
        public bool RunRule(string ruleUID, string? contextJson = null)
        {
            if (IsRuleDisabled(ruleUID))
            {
                Console.Error.WriteLine(
                    $"Error: Rule '{ruleUID}' is disabled and cannot be executed.");
                return false;
            }
            try
            {
                _rules.RunNow(ruleUID, contextJson);
                Console.WriteLine($"Rule '{ruleUID}' executed successfully.");
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error executing rule '{ruleUID}': {e.Message}");
                return false;
            }
        }

        /// <summary>Executes a rule and verifies that an item reaches the expected state.</summary>
        public bool TestRuleExecution(string ruleUID, string expectedItem, string expectedValue)
        {
            if (!RunRule(ruleUID))
            {
                Console.Error.WriteLine($"Error: Rule '{ruleUID}' could not be executed.");
                return false;
            }
            Thread.Sleep(2000);
            try
            {
                var state = _items.GetItemState(expectedItem);
                if (state == expectedValue)
                {
                    Console.WriteLine($"OK: item '{expectedItem}' = '{state}'.");
                    return true;
                }
                Console.Error.WriteLine(
                    $"Error: item '{expectedItem}' expected '{expectedValue}', found '{state}'.");
                return false;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error reading state of '{expectedItem}': {e.Message}");
                return false;
            }
        }
    }
}
