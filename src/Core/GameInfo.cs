using UnityEngine;

namespace StorageVision.Core
{
    /// <summary>Small read-only helpers around the game's product data.</summary>
    internal static class GameInfo
    {
        public static bool IsValidProduct(int productId)
        {
            ProductListing listing = ProductListing.Instance;
            return listing != null && listing.productsData != null && productId >= 0 && productId < listing.productsData.Length;
        }

        public static string ProductName(int productId)
        {
            LocalizationManager loc = LocalizationManager.instance;
            return loc != null ? loc.GetLocalizationString("product" + productId) : "Product " + productId;
        }

        public static string ProductBrand(int productId)
        {
            return IsValidProduct(productId) ? ProductListing.Instance.productsData[productId].productBrand : "";
        }

        /// <summary>Market price per unit, as shown by the game's pricing gun (base price x tier inflation).</summary>
        public static float MarketPrice(int productId)
        {
            if (!IsValidProduct(productId))
            {
                return 0f;
            }
            ProductListing listing = ProductListing.Instance;
            ProductListing.ProductData data = listing.productsData[productId];
            float inflation = 1f;
            if (listing.tierInflation != null && data.productTier >= 0 && data.productTier < listing.tierInflation.Length)
            {
                inflation = listing.tierInflation[data.productTier];
            }
            return data.basePricePerUnit * inflation;
        }

        /// <summary>The player's own shelf price per unit.</summary>
        public static float ShelfPrice(int productId)
        {
            ProductListing listing = ProductListing.Instance;
            if (listing == null || listing.productPlayerPricing == null || productId < 0 || productId >= listing.productPlayerPricing.Length)
            {
                return 0f;
            }
            return listing.productPlayerPricing[productId];
        }

        public static int MaxItemsPerBox(int productId)
        {
            return IsValidProduct(productId) ? ProductListing.Instance.productsData[productId].maxItemsPerBox : 0;
        }

        public static string Price(float value)
        {
            ProductListing listing = ProductListing.Instance;
            if (listing != null)
            {
                return listing.ConvertFloatToTextPrice(value);
            }
            return "$" + (Mathf.Round(value * 100f) / 100f).ToString("0.00");
        }

        /// <summary>Hex string for TMP rich text color tags.</summary>
        public static string Hex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
