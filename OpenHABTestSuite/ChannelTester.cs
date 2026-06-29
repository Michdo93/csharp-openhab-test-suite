using System;
using System.Text.Json;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Tests openHAB item-channel link relationships.
    /// Mirrors the Python <c>ChannelTester</c> class from openhab-test-suite.
    /// </summary>
    public class ChannelTester
    {
        private readonly Links _links;

        /// <summary>Creates a <see cref="ChannelTester"/> backed by the given client.</summary>
        public ChannelTester(OpenHABClient client) => _links = new Links(client);

        /// <summary>Checks whether an item is linked to a specific channel.</summary>
        public bool IsItemLinkedToChannel(string itemName, string channelUID)
        {
            try
            {
                var doc = JsonDocument.Parse(_links.GetLink(itemName, channelUID));
                return doc.RootElement.TryGetProperty("itemName", out var n) &&
                       n.GetString() == itemName;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error checking link '{itemName}' → '{channelUID}': {e.Message}");
                return false;
            }
        }

        /// <summary>Returns all channel links for a given item as a JSON array string.</summary>
        public string GetLinksForItem(string itemName)
        {
            try { return _links.GetLinks(itemName: itemName); }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error reading links for '{itemName}': {e.Message}");
                return "[]";
            }
        }

        /// <summary>Checks whether an item is linked to at least one channel.</summary>
        public bool IsItemLinkedToAnyChannel(string itemName)
        {
            try
            {
                var doc = JsonDocument.Parse(GetLinksForItem(itemName));
                return doc.RootElement.GetArrayLength() > 0;
            }
            catch { return false; }
        }

        /// <summary>Checks whether there are any orphaned links.</summary>
        public bool HasOrphanedLinks()
        {
            try
            {
                var doc = JsonDocument.Parse(_links.GetOrphanLinks());
                return doc.RootElement.GetArrayLength() > 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error reading orphan links: {e.Message}");
                return false;
            }
        }
    }
}
