using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace StorageVision
{
    public enum OriginalPanelMode
    {
        /// <summary>Move the game's product panel (top right) to the screen center, under the crosshair.</summary>
        MoveToCenter,
        /// <summary>Hide the game's product panel completely.</summary>
        Hide,
        /// <summary>Leave the game's product panel where it is.</summary>
        Keep
    }

    public enum HighlightMode
    {
        /// <summary>Outline the storage box you are looking at.</summary>
        LookedAt,
        /// <summary>Outline the nearest box holding the next product of the online order (same target as the game's arrows).</summary>
        OrderTarget,
        /// <summary>Order target while picking an order; otherwise the looked-at box.</summary>
        Both
    }

    public enum OrderValueMode
    {
        /// <summary>What the customer pays on pickup (game formula, shown as min-max range).</summary>
        Payout,
        /// <summary>Market price (base price x tier inflation).</summary>
        Market,
        /// <summary>Your own shelf prices.</summary>
        ShelfPrice,
        /// <summary>All of the above.</summary>
        All
    }

    /// <summary>Display name for the in-game menu, attached as a ConfigDescription tag.</summary>
    internal sealed class MenuInfo
    {
        public readonly string Name;

        public MenuInfo(string name)
        {
            Name = name;
        }
    }

    internal static class ModConfig
    {
        public static ConfigFile File;

        /// <summary>Settings shown in the in-game menu, in display order.</summary>
        public static readonly List<ConfigEntryBase> MenuEntries = new List<ConfigEntryBase>();

        // General
        public static ConfigEntry<KeyboardShortcut> MenuKey;

        // HUD
        public static ConfigEntry<bool> HudEnabled;
        public static ConfigEntry<float> HudFontSize;
        public static ConfigEntry<float> HudOffsetX;
        public static ConfigEntry<float> HudOffsetY;
        public static ConfigEntry<Color> HudTextColor;
        public static ConfigEntry<float> HudBackgroundOpacity;
        public static ConfigEntry<float> HudBackgroundPadding;
        public static ConfigEntry<OriginalPanelMode> OriginalPanel;
        public static ConfigEntry<float> OriginalPanelScale;
        public static ConfigEntry<float> ProductTextScale;

        // Storage
        public static ConfigEntry<bool> StorageEnabled;
        public static ConfigEntry<bool> StorageShowPrices;

        // Highlight
        public static ConfigEntry<bool> HighlightEnabled;
        public static ConfigEntry<HighlightMode> HighlightModeEntry;
        public static ConfigEntry<Color> HighlightColor;
        public static ConfigEntry<float> HighlightWidth;
        public static ConfigEntry<bool> HighlightAlwaysOnTop;
        public static ConfigEntry<bool> HideOrderArrows;

        // Work table
        public static ConfigEntry<bool> WorkTableEnabled;
        public static ConfigEntry<OrderValueMode> WorkTableValueMode;
        public static ConfigEntry<int> WorkTableMaxItemLines;

        private static ConfigFile _cfg;

        public static void Bind(ConfigFile cfg)
        {
            File = cfg;
            _cfg = cfg;
            MenuKey = cfg.Bind("0. General", "MenuKey", new KeyboardShortcut(KeyCode.F8),
                "Key that opens the in-game settings menu.");

            const string hud = "1. Center HUD";
            HudEnabled = Bind(hud, "Enabled", true, "Show center info",
                "Turns on everything in the middle of the screen: the moved product panel, the background and the info text.");
            OriginalPanel = Bind(hud, "OriginalPanel", OriginalPanelMode.MoveToCenter, "Game's product panel",
                "The game's own panel with product image, name and brand (normally top right). " +
                "Move To Center = under the crosshair. Hide = remove it. Keep = leave it top right.");
            OriginalPanelScale = Bind(hud, "OriginalPanelScale", 1f, "Product panel size",
                "Size of the game's product panel (image + name) when it is moved to the center.", 0.3f, 3f);
            ProductTextScale = Bind(hud, "ProductTextScale", 1f, "Product name size",
                "Size of the product name and brand in the game's panel (e.g. \"Tropical Soda\" / \"Ropico\"), on top of the panel size. Only when the panel is moved to the center.", 0.5f, 3f);
            HudFontSize = Bind(hud, "FontSize", 22f, "Info text size",
                "Size of the extra info text from this mod (box amount, prices, packing table info).", 10f, 60f);
            HudTextColor = Bind(hud, "TextColor", Color.white, "Info text color", "Color of the extra info text.");
            HudBackgroundOpacity = Bind(hud, "BackgroundOpacity", 0.6f, "Background darkness",
                "How dark the background box behind the center info is. 0 = no background.", 0f, 1f);
            HudBackgroundPadding = Bind(hud, "BackgroundPadding", 6f, "Background padding",
                "Empty space between the content and the edge of the background box. Lower = tighter box.", 0f, 40f);
            HudOffsetX = Bind(hud, "OffsetX", 0f, "Horizontal position",
                "Moves the center info left (negative) or right (positive) from the crosshair.", -800f, 800f);
            HudOffsetY = Bind(hud, "OffsetY", -40f, "Vertical position",
                "Moves the center info up (positive) or down (negative). -40 = just below the crosshair.", -800f, 800f);

            const string storage = "2. Storage";
            StorageEnabled = Bind(storage, "Enabled", false, "Show box amount text",
                "Adds a line under the product panel with how many items are in the storage box you look at (e.g. \"Box: 20 / 20\"). The outline works without this.");
            StorageShowPrices = Bind(storage, "ShowPrices", true, "Include prices",
                "Adds your shelf price and the market price to that line.");

            const string highlight = "3. Highlight";
            HighlightEnabled = Bind(highlight, "Enabled", true, "Outline boxes",
                "Draws a colored outline around a box.");
            HighlightModeEntry = Bind(highlight, "Mode", HighlightMode.LookedAt, "Which box",
                "Looked At = the storage box under the crosshair. Order Target = while holding the order tray: the nearest box with the next product the online order needs. " +
                "Both = the order target while picking an order, otherwise the box you look at.");
            HighlightColor = Bind(highlight, "Color", Color.green, "Outline color", "Color of the outline.");
            HighlightWidth = Bind(highlight, "Width", 0.8f, "Outline thickness", "How thick the outline is.", 0.1f, 3f);
            HighlightAlwaysOnTop = Bind(highlight, "AlwaysOnTop", false, "Visible through objects",
                "Show the outline even where the box is hidden behind shelves or other boxes.");
            HideOrderArrows = Bind(highlight, "HideOrderArrows", false, "Hide the game's order arrows",
                "Hides the game's green arrows above shelves while you pick online orders. Useful with Order Target.");

            const string table = "4. Work table";
            WorkTableEnabled = Bind(table, "Enabled", true, "Show packing table info",
                "When you look at an online-order packing table: shows whether it has an order, what is left to pack and what it is worth.");
            WorkTableValueMode = Bind(table, "ValueMode", OrderValueMode.Payout, "Order value",
                "Payout = what the customer pays you at pickup (the game rolls 3.25-3.5x market price per item, so a range is shown). " +
                "Market = market price. Shelf Price = at your own prices. All = all three.");
            WorkTableMaxItemLines = Bind(table, "MaxItemLines", 8, "Products listed",
                "How many products of the order to list (0 = no list).", 0, 40);

            MenuEntries.Clear();
            MenuEntries.AddRange(new ConfigEntryBase[]
            {
                HudEnabled, OriginalPanel, OriginalPanelScale, ProductTextScale, HudFontSize, HudTextColor,
                HudBackgroundOpacity, HudBackgroundPadding, HudOffsetX, HudOffsetY,
                StorageEnabled, StorageShowPrices,
                HighlightEnabled, HighlightModeEntry, HighlightColor, HighlightWidth, HighlightAlwaysOnTop, HideOrderArrows,
                WorkTableEnabled, WorkTableValueMode, WorkTableMaxItemLines
            });
        }

        public static string DisplayName(ConfigEntryBase entry)
        {
            object[] tags = entry.Description.Tags;
            if (tags != null)
            {
                foreach (object tag in tags)
                {
                    if (tag is MenuInfo info)
                    {
                        return info.Name;
                    }
                }
            }
            return entry.Definition.Key;
        }

        private static ConfigEntry<T> Bind<T>(string section, string key, T value, string name, string description)
        {
            return _cfg.Bind(section, key, value, new ConfigDescription(description, null, new MenuInfo(name)));
        }

        private static ConfigEntry<float> Bind(string section, string key, float value, string name, string description, float min, float max)
        {
            return _cfg.Bind(section, key, value,
                new ConfigDescription(description, new AcceptableValueRange<float>(min, max), new MenuInfo(name)));
        }

        private static ConfigEntry<int> Bind(string section, string key, int value, string name, string description, int min, int max)
        {
            return _cfg.Bind(section, key, value,
                new ConfigDescription(description, new AcceptableValueRange<int>(min, max), new MenuInfo(name)));
        }
    }
}
