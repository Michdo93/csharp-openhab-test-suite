using System;
using System.Text.Json;
using OpenHABRestClient;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Tests openHAB sitemaps: existence and item references.
    /// Mirrors the Python <c>SitemapTester</c> class from openhab-test-suite.
    /// </summary>
    public class SitemapTester
    {
        private readonly Sitemaps _sitemaps;

        /// <summary>Creates a <see cref="SitemapTester"/> backed by the given client.</summary>
        public SitemapTester(OpenHABClient client) => _sitemaps = new Sitemaps(client);

        /// <summary>Checks whether a sitemap with the given name exists.</summary>
        public bool DoesSitemapExist(string sitemapName)
        {
            try
            {
                var doc = JsonDocument.Parse(_sitemaps.GetSitemaps());
                foreach (var s in doc.RootElement.EnumerateArray())
                    if (s.TryGetProperty("name", out var n) &&
                        n.GetString() == sitemapName) return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error reading sitemaps: {e.Message}");
            }
            return false;
        }

        /// <summary>Checks whether an item is referenced anywhere inside a sitemap.</summary>
        public bool DoesSitemapContainItem(string sitemapName, string itemName)
        {
            try
            {
                var doc = JsonDocument.Parse(_sitemaps.GetSitemap(sitemapName));
                return SearchForItem(doc.RootElement, itemName);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Error reading sitemap '{sitemapName}': {e.Message}");
                return false;
            }
        }

        private static bool SearchForItem(JsonElement node, string itemName)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("item", out var item) &&
                    item.TryGetProperty("name", out var n) &&
                    n.GetString() == itemName) return true;
                foreach (var prop in node.EnumerateObject())
                    if (SearchForItem(prop.Value, itemName)) return true;
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in node.EnumerateArray())
                    if (SearchForItem(el, itemName)) return true;
            }
            return false;
        }
    }
}
