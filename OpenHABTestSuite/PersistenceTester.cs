using System;
using System.Text.Json;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Tests openHAB persistence services: item registration, data existence,
    /// and state verification.
    /// Mirrors the Python <c>PersistenceTester</c> class from openhab-test-suite.
    /// </summary>
    public class PersistenceTester
    {
        private readonly Persistence _persistence;

        /// <summary>Creates a <see cref="PersistenceTester"/> backed by the given client.</summary>
        public PersistenceTester(OpenHABClient client) =>
            _persistence = new Persistence(client);

        /// <summary>Checks whether an item is registered in the given persistence service.</summary>
        public bool IsItemPersisted(string serviceId, string itemName)
        {
            try
            {
                var doc  = JsonDocument.Parse(_persistence.GetItemsFromService(serviceId));
                foreach (var entry in doc.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object &&
                        entry.TryGetProperty("name", out var n) &&
                        n.GetString() == itemName) return true;
                    if (entry.ValueKind == JsonValueKind.String &&
                        entry.GetString() == itemName) return true;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error checking persistence for '{itemName}': {e.Message}");
            }
            return false;
        }

        /// <summary>Checks whether historical data exists for an item within a time range.</summary>
        public bool HasDataInRange(string serviceId, string itemName,
                                   string startTime, string endTime)
        {
            try
            {
                var doc     = JsonDocument.Parse(
                    _persistence.GetItemPersistenceData(itemName, serviceId,
                        startTime, endTime));
                if (doc.RootElement.TryGetProperty("data", out var data))
                    return data.GetArrayLength() > 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error reading persistence data for '{itemName}': {e.Message}");
            }
            return false;
        }

        /// <summary>Checks whether the most recently persisted value matches the expected state.</summary>
        public bool CheckLastPersistedState(string serviceId, string itemName,
                                            string expectedState)
        {
            try
            {
                var doc     = JsonDocument.Parse(
                    _persistence.GetItemPersistenceData(itemName, serviceId));
                if (!doc.RootElement.TryGetProperty("data", out var data)) return false;
                var entries = data.GetArrayLength();
                if (entries == 0) return false;
                var last    = data[entries - 1];
                return last.TryGetProperty("state", out var s) &&
                       s.GetString() == expectedState;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error reading last persisted state for '{itemName}': {e.Message}");
                return false;
            }
        }
    }
}
