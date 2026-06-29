using System;
using System.Text.Json;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Tests openHAB Thing status and enable/disable operations.
    /// Mirrors the Python <c>ThingTester</c> class from openhab-test-suite.
    /// </summary>
    public class ThingTester
    {
        private readonly Things _things;

        /// <summary>Creates a <see cref="ThingTester"/> backed by the given client.</summary>
        public ThingTester(OpenHABClient client) => _things = new Things(client);

        /// <summary>Returns the status string of a Thing
        /// (e.g. <c>"ONLINE"</c>, <c>"OFFLINE"</c>).</summary>
        public string GetThingStatus(string thingUID)
        {
            try
            {
                var doc = JsonDocument.Parse(_things.GetThing(thingUID));
                if (doc.RootElement.TryGetProperty("statusInfo", out var si) &&
                    si.TryGetProperty("status", out var s))
                    return s.GetString() ?? "UNKNOWN";
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error reading status of '{thingUID}': {e.Message}");
            }
            return "UNKNOWN";
        }

        /// <summary>Checks whether a Thing has the specified status.</summary>
        public bool IsThingStatus(string thingUID, string status) =>
            GetThingStatus(thingUID) == status;

        /// <returns><c>true</c> if the Thing is <c>ONLINE</c>.</returns>
        public bool IsThingOnline(string uid)         => IsThingStatus(uid, "ONLINE");
        /// <returns><c>true</c> if the Thing is <c>OFFLINE</c>.</returns>
        public bool IsThingOffline(string uid)        => IsThingStatus(uid, "OFFLINE");
        /// <returns><c>true</c> if the Thing is <c>PENDING</c>.</returns>
        public bool IsThingPending(string uid)        => IsThingStatus(uid, "PENDING");
        /// <returns><c>true</c> if the Thing is <c>UNKNOWN</c>.</returns>
        public bool IsThingUnknown(string uid)        => IsThingStatus(uid, "UNKNOWN");
        /// <returns><c>true</c> if the Thing is <c>UNINITIALIZED</c>.</returns>
        public bool IsThingUninitialized(string uid)  => IsThingStatus(uid, "UNINITIALIZED");
        /// <returns><c>true</c> if the Thing is in <c>ERROR</c> state.</returns>
        public bool IsThingError(string uid)          => IsThingStatus(uid, "ERROR");

        /// <summary>Enables a Thing.</summary>
        public bool EnableThing(string thingUID)
        {
            try
            {
                _things.EnableThing(thingUID);
                Console.WriteLine($"Thing '{thingUID}' enabled successfully.");
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error enabling '{thingUID}': {e.Message}");
                return false;
            }
        }

        /// <summary>Disables a Thing.</summary>
        public bool DisableThing(string thingUID)
        {
            try
            {
                _things.DisableThing(thingUID);
                Console.WriteLine($"Thing '{thingUID}' disabled successfully.");
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error disabling '{thingUID}': {e.Message}");
                return false;
            }
        }
    }
}
